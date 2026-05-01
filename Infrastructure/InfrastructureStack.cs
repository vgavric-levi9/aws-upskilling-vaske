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
using Infrastructure.Permissions;

namespace Infrastructure;

public class InfrastructureStack : Stack
{
    public InfrastructureStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // Create S3 buckets with "vaske" naming
        var inputBucket = new Bucket(this, "VaskeMediaProcessorInputBucket", new BucketProps
        {
            BucketName = $"vaske-media-processor-input-{Account}",
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

        var outputBucket = new Bucket(this, "VaskeMediaProcessorOutputBucket", new BucketProps
        {
            BucketName = $"vaske-media-processor-output-{Account}",
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

        // Create DynamoDB table with "vaske" naming
        var processingTable = new Table(this, "VaskeMediaProcessingJobs", new TableProps
        {
            TableName = "VaskeMediaProcessingJobs",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "JobId",
                Type = AttributeType.STRING
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY,
            PointInTimeRecovery = false
        });

        // Create specialized IAM roles with minimal permissions
        var imageUploadRole = LambdaRoles.CreateImageUploadRole(this, inputBucket.BucketArn, processingTable.TableArn);
        var mediaProcessorRole = LambdaRoles.CreateMediaProcessorRole(this, inputBucket.BucketArn, outputBucket.BucketArn, processingTable.TableArn);
        var statusQueryRole = LambdaRoles.CreateStatusQueryRole(this, outputBucket.BucketArn, processingTable.TableArn);

        // Create Lambda functions with "vaske" naming and specialized roles
        var uploadHandler = new Function(this, "VaskeImageUploadHandler", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "LambdaHandlers::LambdaHandlers.Handlers.ImageUpload::FunctionHandler",
            Code = Code.FromAsset("../LambdaHandlers/bin/Release/net8.0/publish"),
            FunctionName = "VaskeImageUploadHandler",
            Role = imageUploadRole,
            Timeout = Duration.Minutes(1),
            MemorySize = 512,
            Environment = new Dictionary<string, string>
            {
                ["INPUT_BUCKET"] = inputBucket.BucketName,
                ["DYNAMODB_TABLE"] = processingTable.TableName
            },
            LogRetention = RetentionDays.ONE_WEEK
        });

        var processorHandler = new Function(this, "VaskeMediaProcessorHandler", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "LambdaHandlers::LambdaHandlers.Handlers.MediaProcessor::FunctionHandler",
            Code = Code.FromAsset("../LambdaHandlers/bin/Release/net8.0/publish"),
            FunctionName = "VaskeMediaProcessorHandler",
            Role = mediaProcessorRole,
            Timeout = Duration.Minutes(5),
            MemorySize = 1024,
            Environment = new Dictionary<string, string>
            {
                ["OUTPUT_BUCKET"] = outputBucket.BucketName,
                ["DYNAMODB_TABLE"] = processingTable.TableName
            },
            LogRetention = RetentionDays.ONE_WEEK
        });

        var statusHandler = new Function(this, "VaskeStatusQueryHandler", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "LambdaHandlers::LambdaHandlers.Handlers.StatusQuery::FunctionHandler",
            Code = Code.FromAsset("../LambdaHandlers/bin/Release/net8.0/publish"),
            FunctionName = "VaskeStatusQueryHandler",
            Role = statusQueryRole,
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

        // Create REST API Gateway with "vaske" naming
        var api = new RestApi(this, "VaskeMediaProcessorApi", new RestApiProps
        {
            RestApiName = "Vaske Media Processor API",
            Description = "API Gateway for Vaske Serverless Media Processor",
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

        // CloudWatch Dashboard with "vaske" naming
        var dashboard = new Amazon.CDK.AWS.CloudWatch.Dashboard(this, "VaskeMediaProcessorDashboard", new Amazon.CDK.AWS.CloudWatch.DashboardProps
        {
            DashboardName = "VaskeMediaProcessor-Monitoring"
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
