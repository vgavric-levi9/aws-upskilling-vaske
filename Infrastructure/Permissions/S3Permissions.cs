using Amazon.CDK.AWS.IAM;

namespace Infrastructure.Permissions;

public static class S3Permissions
{
    /// <summary>
    /// Minimal S3 permissions for Lambda functions - only required actions for input/output buckets
    /// </summary>
    public static PolicyStatement CreateLambdaS3Policy(string inputBucketArn, string outputBucketArn)
    {
        return new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "s3:GetObject",           // Read objects from input bucket
                "s3:PutObject",           // Write objects to input/output buckets  
                "s3:DeleteObject",        // Delete objects (cleanup)
                "s3:GetObjectVersion"     // Object versioning
            },
            Resources = new[]
            {
                $"{inputBucketArn}/*",    // All objects in input bucket
                $"{outputBucketArn}/*"    // All objects in output bucket
            }
        });
    }

    /// <summary>
    /// S3 permissions for presigned URL generation - read access only
    /// </summary>
    public static PolicyStatement CreatePresignedUrlPolicy(string outputBucketArn)
    {
        return new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "s3:GetObject",           // Required for presigned URL generation
                "s3:GetObjectVersion"     // Versioning for presigned URL
            },
            Resources = new[]
            {
                $"{outputBucketArn}/*"    // Output bucket objects only
            }
        });
    }
}