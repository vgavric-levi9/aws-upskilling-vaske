using Amazon.DynamoDBv2.DataModel;

namespace LambdaHandlers.Models;

[DynamoDBTable("VaskeMediaProcessingJobs")]
public class ProcessingJob
{
    [DynamoDBHashKey]
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public string OriginalFileName { get; set; } = string.Empty;

    public string InputBucket { get; set; } = string.Empty;

    public string InputKey { get; set; } = string.Empty;

    public string OutputBucket { get; set; } = string.Empty;

    public string OutputKey { get; set; } = string.Empty;

    public long OriginalFileSize { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public DateTime? ProcessingStartedAt { get; set; }

    public DateTime? ProcessingCompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int? ProcessedWidth { get; set; }

    public int? ProcessedHeight { get; set; }

    public long? ProcessedFileSize { get; set; }
}