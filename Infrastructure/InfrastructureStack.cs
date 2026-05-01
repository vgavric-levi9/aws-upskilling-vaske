using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Constructs;

namespace Infrastructure;

public class InfrastructureStack : Stack
{
    public InfrastructureStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // Create S3 buckets
        var inputBucket = new Bucket(this, "MediaProcessorInputBucket", new BucketProps
        {
            BucketName = $"media-processor-input-{Account}",
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
            Versioned = false,
            Cors = new[]
            {
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.POST, HttpMethods.GET },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" }
                }
            }
        });

        var outputBucket = new Bucket(this, "MediaProcessorOutputBucket", new BucketProps
        {
            BucketName = $"media-processor-output-{Account}",
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
            Versioned = false,
            Cors = new[]
            {
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" }
                }
            }
        });

        // Create DynamoDB table
        var processingTable = new Table(this, "MediaProcessingJobs", new TableProps
        {
            TableName = "MediaProcessingJobs",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "JobId",
                Type = AttributeType.STRING
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY,
            PointInTimeRecovery = false
        });

        // Create Lambda execution role with necessary permissions
        var lambdaRole = new Role(this, "MediaProcessorLambdaRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            },
            InlinePolicies = new Dictionary<string, PolicyDocument>
            {
                ["MediaProcessorPolicy"] = new PolicyDocument(new PolicyDocumentProps
                {
                    Statements = new[]
                    {
                        new PolicyStatement(new PolicyStatementProps
                        {
                            Effect = Effect.ALLOW,
                            Actions = new[]
                            {
                                "s3:GetObject",
                                "s3:PutObject",
                                "s3:DeleteObject",
                                "s3:GetObjectVersion"
                            },
                            Resources = new[]
                            {
                                $"{inputBucket.BucketArn}/*",
                                $"{outputBucket.BucketArn}/*"
                            }
                        }),
                        new PolicyStatement(new PolicyStatementProps
                        {
                            Effect = Effect.ALLOW,
                            Actions = new[]
                            {
                                "dynamodb:GetItem",
                                "dynamodb:PutItem",
                                "dynamodb:UpdateItem",
                                "dynamodb:Query",
                                "dynamodb:Scan",
                                "dynamodb:DescribeTable"
                            },
                            Resources = new[] { processingTable.TableArn }
                        })
                    }
                })
            }
        });

        // Create Lambda functions
        var uploadHandler = new Function(this, "ImageUploadHandler", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "LambdaHandlers::LambdaHandlers.Handlers.ImageUpload::FunctionHandler",
            Code = Code.FromAsset("../LambdaHandlers/bin/Release/net8.0/publish"),
            FunctionName = "ImageUploadHandler",
            Role = lambdaRole,
            Timeout = Duration.Minutes(1),
            MemorySize = 512,
            Environment = new Dictionary<string, string>
            {
                ["INPUT_BUCKET"] = inputBucket.BucketName,
                ["DYNAMODB_TABLE"] = processingTable.TableName
            },
            LogRetention = RetentionDays.ONE_WEEK
        });

        var processorHandler = new Function(this, "MediaProcessorHandler", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "LambdaHandlers::LambdaHandlers.Handlers.MediaProcessor::FunctionHandler",
            Code = Code.FromAsset("../LambdaHandlers/bin/Release/net8.0/publish"),
            FunctionName = "MediaProcessorHandler",
            Role = lambdaRole,
            Timeout = Duration.Minutes(5),
            MemorySize = 1024,
            Environment = new Dictionary<string, string>
            {
                ["OUTPUT_BUCKET"] = outputBucket.BucketName,
                ["DYNAMODB_TABLE"] = processingTable.TableName
            },
            LogRetention = RetentionDays.ONE_WEEK
        });

        var statusHandler = new Function(this, "StatusQueryHandler", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "LambdaHandlers::LambdaHandlers.Handlers.StatusQuery::FunctionHandler",
            Code = Code.FromAsset("../LambdaHandlers/bin/Release/net8.0/publish"),
            FunctionName = "StatusQueryHandler",
            Role = lambdaRole,
            Timeout = Duration.Seconds(30),
            MemorySize = 256,
            Environment = new Dictionary<string, string>
            {
                ["DYNAMODB_TABLE"] = processingTable.TableName
            },
            LogRetention = RetentionDays.ONE_WEEK
        });

        // Add S3 → Lambda notification (more reliable than EventBridge)
        inputBucket.AddEventNotification(
            EventType.OBJECT_CREATED,
            new LambdaDestination(processorHandler)
        );

        // Create REST API Gateway
        var api = new RestApi(this, "MediaProcessorApi", new RestApiProps
        {
            RestApiName = "Media Processor API",
            Description = "API Gateway for Serverless Media Processor",
            DefaultCorsPreflightOptions = new CorsOptions
            {
                AllowOrigins = Cors.ALL_ORIGINS,
                AllowMethods = Cors.ALL_METHODS,
                AllowHeaders = new[] { "Content-Type", "X-Amz-Date", "Authorization", "X-Api-Key", "X-Amz-Security-Token", "X-Filename" }
            }
        });

        // Create API Gateway resources and methods
        var apiResource = api.Root.AddResource("api");
        
        // Upload endpoint: POST /api/upload
        var uploadResource = apiResource.AddResource("upload");
        uploadResource.AddMethod("POST", new LambdaIntegration(uploadHandler), new MethodOptions
        {
            RequestParameters = new Dictionary<string, bool>
            {
                ["method.request.header.Content-Type"] = false,
                ["method.request.header.X-Filename"] = false
            }
        });

        // Status endpoint: GET /api/status/{jobId}
        var statusResource = apiResource.AddResource("status");
        var jobIdResource = statusResource.AddResource("{jobId}");
        jobIdResource.AddMethod("GET", new LambdaIntegration(statusHandler));

        // Create API documentation
        var apiDoc = new CfnDocumentationPart(this, "ApiDocumentation", new CfnDocumentationPartProps
        {
            Location = new CfnDocumentationPart.LocationProperty { Type = "API" },
            RestApiId = api.RestApiId,
            Properties = "{\"description\":\"Serverless Media Processor API - Upload images and track processing status\"}"
        });

        var uploadDoc = new CfnDocumentationPart(this, "UploadMethodDoc", new CfnDocumentationPartProps
        {
            Location = new CfnDocumentationPart.LocationProperty
            {
                Type = "METHOD",
                Path = "/api/upload",
                Method = "POST"
            },
            RestApiId = api.RestApiId,
            Properties = "{\"summary\":\"Upload Image\",\"description\":\"Upload an image for processing. Send base64-encoded image data in request body.\"}"
        });

        var statusDoc = new CfnDocumentationPart(this, "StatusMethodDoc", new CfnDocumentationPartProps
        {
            Location = new CfnDocumentationPart.LocationProperty
            {
                Type = "METHOD",
                Path = "/api/status/{jobId}",
                Method = "GET"
            },
            RestApiId = api.RestApiId,
            Properties = "{\"summary\":\"Query Processing Status\",\"description\":\"Get the processing status and results for a job by JobId.\"}"
        });

        // Create documentation version
        var docVersion = new CfnDocumentationVersion(this, "ApiDocVersion", new CfnDocumentationVersionProps
        {
            DocumentationVersion = "1.0.0",
            RestApiId = api.RestApiId
        });
        docVersion.Node.AddDependency(apiDoc);
        docVersion.Node.AddDependency(uploadDoc);
        docVersion.Node.AddDependency(statusDoc);

        // CloudWatch Dashboard
        var dashboard = new Amazon.CDK.AWS.CloudWatch.Dashboard(this, "MediaProcessorDashboard", new Amazon.CDK.AWS.CloudWatch.DashboardProps
        {
            DashboardName = "MediaProcessor-Monitoring"
        });

        // Output important information
        new CfnOutput(this, "ApiUrl", new CfnOutputProps
        {
            Value = api.Url,
            Description = "Media Processor API Gateway URL"
        });

        new CfnOutput(this, "InputBucket", new CfnOutputProps
        {
            Value = inputBucket.BucketName,
            Description = "S3 Input Bucket for image uploads"
        });

        new CfnOutput(this, "OutputBucket", new CfnOutputProps
        {
            Value = outputBucket.BucketName,
            Description = "S3 Output Bucket for processed images"
        });

        new CfnOutput(this, "DynamoDBTable", new CfnOutputProps
        {
            Value = processingTable.TableName,
            Description = "DynamoDB table for processing jobs"
        });

        new CfnOutput(this, "UploadEndpoint", new CfnOutputProps
        {
            Value = $"{api.Url}api/upload",
            Description = "Upload endpoint URL"
        });

        new CfnOutput(this, "StatusEndpoint", new CfnOutputProps
        {
            Value = $"{api.Url}api/status/{{jobId}}",
            Description = "Status query endpoint URL template"
        });
    }
}
