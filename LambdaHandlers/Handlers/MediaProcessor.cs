using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using LambdaHandlers.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace LambdaHandlers.Handlers;

public class MediaProcessor
{
    private readonly IAmazonS3 _s3Client;
    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly string _outputBucket;

    public MediaProcessor() : this(new AmazonS3Client(), new DynamoDBContext(new AmazonDynamoDBClient()))
    {
    }

    public MediaProcessor(IAmazonS3 s3Client, IDynamoDBContext dynamoDbContext)
    {
        _s3Client = s3Client;
        _dynamoDbContext = dynamoDbContext;
        _outputBucket = Environment.GetEnvironmentVariable("OUTPUT_BUCKET") ?? "media-processor-output-bucket";
    }

    public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
    {
        foreach (var record in s3Event.Records)
        {
            await ProcessRecord(record, context);
        }
    }

    private async Task ProcessRecord(S3Event.S3EventNotificationRecord record, ILambdaContext context)
    {
        var bucketName = record.S3.Bucket.Name;
        var objectKey = record.S3.Object.Key;
        
        context.Logger.LogInformation($"Processing S3 event for object: {bucketName}/{objectKey}");

        var jobId = ExtractJobIdFromKey(objectKey);
        if (string.IsNullOrEmpty(jobId))
        {
            context.Logger.LogError($"Could not extract JobId from S3 key: {objectKey}");
            return;
        }

        try
        {
            var processingJob = await _dynamoDbContext.LoadAsync<ProcessingJob>(jobId);
            if (processingJob == null)
            {
                context.Logger.LogError($"Processing job not found for JobId: {jobId}");
                return;
            }

            processingJob.Status = "processing";
            processingJob.ProcessingStartedAt = DateTime.UtcNow;
            await _dynamoDbContext.SaveAsync(processingJob);

            context.Logger.LogInformation($"Starting processing for JobId: {jobId}");

            await ProcessImage(bucketName, objectKey, processingJob, context);

            processingJob.Status = "completed";
            processingJob.ProcessingCompletedAt = DateTime.UtcNow;
            await _dynamoDbContext.SaveAsync(processingJob);

            context.Logger.LogInformation($"Processing completed successfully for JobId: {jobId}");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error processing JobId {jobId}: {ex.Message}");
            context.Logger.LogError($"Stack trace: {ex.StackTrace}");

            try
            {
                var failedJob = await _dynamoDbContext.LoadAsync<ProcessingJob>(jobId);
                if (failedJob != null)
                {
                    failedJob.Status = "failed";
                    failedJob.ErrorMessage = ex.Message;
                    failedJob.ProcessingCompletedAt = DateTime.UtcNow;
                    await _dynamoDbContext.SaveAsync(failedJob);
                }
            }
            catch (Exception saveEx)
            {
                context.Logger.LogError($"Failed to update job status to failed: {saveEx.Message}");
            }
        }
    }

    private async Task ProcessImage(string inputBucket, string inputKey, ProcessingJob job, ILambdaContext context)
    {
        context.Logger.LogInformation("Downloading image from S3...");
        
        var getRequest = new GetObjectRequest
        {
            BucketName = inputBucket,
            Key = inputKey
        };

        using var response = await _s3Client.GetObjectAsync(getRequest);
        using var inputStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(inputStream);
        inputStream.Position = 0;

        context.Logger.LogInformation("Processing image...");

        var demoDelaySeconds = int.TryParse(Environment.GetEnvironmentVariable("PROCESSING_DEMO_DELAY_SECONDS"), out var s) ? s : 0;
        if (demoDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(demoDelaySeconds));
        }

        using var image = await Image.LoadAsync(inputStream);
        
        var originalWidth = image.Width;
        var originalHeight = image.Height;
        
        image.Mutate(x => x.Resize(800, 600));
        
        var outputKey = $"processed/{job.JobId}/resized_{job.OriginalFileName}";
        
        using var outputStream = new MemoryStream();
        await image.SaveAsJpegAsync(outputStream);
        outputStream.Position = 0;

        context.Logger.LogInformation($"Uploading processed image to S3: {_outputBucket}/{outputKey}");
        
        var putRequest = new PutObjectRequest
        {
            BucketName = _outputBucket,
            Key = outputKey,
            InputStream = outputStream,
            ContentType = "image/jpeg",
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };

        putRequest.Metadata.Add("original-width", originalWidth.ToString());
        putRequest.Metadata.Add("original-height", originalHeight.ToString());
        putRequest.Metadata.Add("processed-width", image.Width.ToString());
        putRequest.Metadata.Add("processed-height", image.Height.ToString());
        putRequest.Metadata.Add("processed-by", "MediaProcessor");
        putRequest.Metadata.Add("processed-timestamp", DateTime.UtcNow.ToString("O"));
        putRequest.Metadata.Add("job-id", job.JobId);

        var processedFileSize = outputStream.Length;  // Get length before S3 upload
        
        await _s3Client.PutObjectAsync(putRequest);

        job.OutputBucket = _outputBucket;
        job.OutputKey = outputKey;
        job.ProcessedWidth = image.Width;
        job.ProcessedHeight = image.Height;
        job.ProcessedFileSize = processedFileSize;

        context.Logger.LogInformation($"Image processing completed. Original: {originalWidth}x{originalHeight}, Processed: {image.Width}x{image.Height}");
    }

    private static string ExtractJobIdFromKey(string s3Key)
    {
        var parts = s3Key.Split('/');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }
}