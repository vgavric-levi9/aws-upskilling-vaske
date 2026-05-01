using Amazon.CDK.AWS.IAM;

namespace Infrastructure.Permissions;

public static class DynamoDbPermissions
{
    /// <summary>
    /// Minimal DynamoDB permissions for ImageUpload Lambda - writing new jobs (SaveAsync needs both Put and Update)
    /// </summary>
    public static PolicyStatement CreateUploadLambdaPolicy(string tableArn)
    {
        return new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "dynamodb:PutItem",       // Create new job
                "dynamodb:UpdateItem",    // Required for DynamoDB SaveAsync operation
                "dynamodb:DescribeTable"  // Required for DynamoDB Context
            },
            Resources = new[] { tableArn }
        });
    }

    /// <summary>
    /// Minimal DynamoDB permissions for MediaProcessor Lambda - reading and updating jobs
    /// </summary>
    public static PolicyStatement CreateProcessorLambdaPolicy(string tableArn)
    {
        return new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "dynamodb:GetItem",       // Read existing job
                "dynamodb:UpdateItem",    // Update job status
                "dynamodb:PutItem",       // Fallback for SaveAsync operations
                "dynamodb:DescribeTable"  // Required for DynamoDB Context
            },
            Resources = new[] { tableArn }
        });
    }

    /// <summary>
    /// Minimal DynamoDB permissions for StatusQuery Lambda - reading jobs only
    /// </summary>
    public static PolicyStatement CreateStatusQueryLambdaPolicy(string tableArn)
    {
        return new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "dynamodb:GetItem",       // Read job status
                "dynamodb:DescribeTable"  // Required for DynamoDB Context
            },
            Resources = new[] { tableArn }
        });
    }
}