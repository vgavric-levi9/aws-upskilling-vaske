namespace LambdaHandlers.Models;

public class UploadResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class StatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public ProcessingResult? Result { get; set; }
}

public class ProcessingResult
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? FileSize { get; set; }
    public string? OutputUrl { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
}