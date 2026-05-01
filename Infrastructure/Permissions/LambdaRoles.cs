using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace Infrastructure.Permissions;

public static class LambdaRoles
{
    /// <summary>
    /// Create IAM role for ImageUpload Lambda function with minimal permissions
    /// </summary>
    public static Role CreateImageUploadRole(Construct scope, string inputBucketArn, string tableArn)
    {
        return new Role(scope, "VaskeImageUploadLambdaRole", new RoleProps
        {
            RoleName = "VaskeMediaProcessor-ImageUploadRole",
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            Description = "Minimal permissions for Vaske Image Upload Lambda - S3 upload and DynamoDB write",
            ManagedPolicies = new[]
            {
                // Basic Lambda execution role
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            },
            InlinePolicies = new Dictionary<string, PolicyDocument>
            {
                ["VaskeImageUploadPolicy"] = new PolicyDocument(new PolicyDocumentProps
                {
                    Statements = new[]
                    {
                        // S3 permissions only for input bucket
                        new PolicyStatement(new PolicyStatementProps
                        {
                            Effect = Effect.ALLOW,
                            Actions = new[] { "s3:PutObject" },
                            Resources = new[] { $"{inputBucketArn}/*" }
                        }),
                        // DynamoDB permissions only for writing jobs
                        DynamoDbPermissions.CreateUploadLambdaPolicy(tableArn)
                    }
                })
            }
        });
    }

    /// <summary>
    /// Create IAM role for MediaProcessor Lambda function with permissions for S3 and DynamoDB
    /// </summary>
    public static Role CreateMediaProcessorRole(Construct scope, string inputBucketArn, string outputBucketArn, string tableArn)
    {
        return new Role(scope, "VaskeMediaProcessorLambdaRole", new RoleProps
        {
            RoleName = "VaskeMediaProcessor-ProcessorRole",
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            Description = "Minimal permissions for Vaske Media Processor Lambda - S3 read/write and DynamoDB update",
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            },
            InlinePolicies = new Dictionary<string, PolicyDocument>
            {
                ["VaskeMediaProcessorPolicy"] = new PolicyDocument(new PolicyDocumentProps
                {
                    Statements = new[]
                    {
                        // S3 permissions for input/output buckets
                        S3Permissions.CreateLambdaS3Policy(inputBucketArn, outputBucketArn),
                        // DynamoDB permissions for reading and updating jobs
                        DynamoDbPermissions.CreateProcessorLambdaPolicy(tableArn)
                    }
                })
            }
        });
    }

    /// <summary>
    /// Create IAM role for StatusQuery Lambda function with read-only permissions
    /// </summary>
    public static Role CreateStatusQueryRole(Construct scope, string outputBucketArn, string tableArn)
    {
        return new Role(scope, "VaskeStatusQueryLambdaRole", new RoleProps
        {
            RoleName = "VaskeMediaProcessor-StatusQueryRole",
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            Description = "Minimal permissions for Vaske Status Query Lambda - DynamoDB read and S3 presigned URLs",
            ManagedPolicies = new[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            },
            InlinePolicies = new Dictionary<string, PolicyDocument>
            {
                ["VaskeStatusQueryPolicy"] = new PolicyDocument(new PolicyDocumentProps
                {
                    Statements = new[]
                    {
                        // S3 permissions only for presigned URL generation
                        S3Permissions.CreatePresignedUrlPolicy(outputBucketArn),
                        // DynamoDB permissions only for reading
                        DynamoDbPermissions.CreateStatusQueryLambdaPolicy(tableArn)
                    }
                })
            }
        });
    }
}