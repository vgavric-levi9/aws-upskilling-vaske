using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using LambdaHandlers.Models;

namespace LambdaHandlers.Handlers;

public class StatusQuery
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDBContext _dynamoDbContext;
    private readonly IAmazonS3 _s3Client;

    public StatusQuery() : this(new AmazonDynamoDBClient(), new AmazonS3Client())
    {
    }

    public StatusQuery(IAmazonDynamoDB dynamoDbClient, IAmazonS3 s3Client)
    {
        _dynamoDbClient = dynamoDbClient;
        _dynamoDbContext = new DynamoDBContext(_dynamoDbClient);
        _s3Client = s3Client;
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation($"Processing status query. Method: {request.HttpMethod}, Path: {request.Path}");

            var jobId = ExtractJobIdFromPath(request.PathParameters);
            if (string.IsNullOrEmpty(jobId))
            {
                return CreateErrorResponse(400, "JobId is required", string.Empty);
            }

            context.Logger.LogInformation($"Querying status for JobId: {jobId}");

            var processingJob = await _dynamoDbContext.LoadAsync<ProcessingJob>(jobId);
            if (processingJob == null)
            {
                return CreateErrorResponse(404, "Job not found", jobId);
            }

            var response = await CreateStatusResponse(processingJob, context);

            context.Logger.LogInformation($"Status query completed for JobId: {jobId}, Status: {processingJob.Status}");

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(response),
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" }
                }
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error processing status query: {ex.Message}");
            context.Logger.LogError($"Stack trace: {ex.StackTrace}");
            return CreateErrorResponse(500, "Internal server error occurred while querying status", string.Empty);
        }
    }

    private async Task<StatusResponse> CreateStatusResponse(ProcessingJob job, ILambdaContext context)
    {
        var response = new StatusResponse
        {
            JobId = job.JobId,
            Status = job.Status,
            OriginalFileName = job.OriginalFileName,
            UploadedAt = job.UploadedAt,
            ProcessingStartedAt = job.ProcessingStartedAt,
            ProcessingCompletedAt = job.ProcessingCompletedAt,
            ErrorMessage = job.ErrorMessage
        };

        if (job.Status == "completed" && !string.IsNullOrEmpty(job.OutputBucket) && !string.IsNullOrEmpty(job.OutputKey))
        {
            try
            {
                var outputUrl = await GeneratePresignedUrl(job.OutputBucket, job.OutputKey, context);
                
                response.Result = new ProcessingResult
                {
                    Width = job.ProcessedWidth,
                    Height = job.ProcessedHeight,
                    FileSize = job.ProcessedFileSize,
                    OutputUrl = outputUrl
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning($"Could not generate presigned URL for {job.OutputBucket}/{job.OutputKey}: {ex.Message}");
            }
        }

        return response;
    }

    private async Task<string> GeneratePresignedUrl(string bucket, string key, ILambdaContext context)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddHours(1)
            };

            var url = await _s3Client.GetPreSignedURLAsync(request);
            context.Logger.LogInformation($"Generated presigned URL for {bucket}/{key}");
            return url;
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Failed to generate presigned URL: {ex.Message}");
            return $"s3://{bucket}/{key}";
        }
    }

    private static string ExtractJobIdFromPath(IDictionary<string, string>? pathParameters)
    {
        if (pathParameters?.TryGetValue("jobId", out var jobId) == true && !string.IsNullOrEmpty(jobId))
        {
            return jobId;
        }
        
        return string.Empty;
    }

    private static APIGatewayProxyResponse CreateErrorResponse(int statusCode, string message, string jobId)
    {
        var errorResponse = new ErrorResponse
        {
            Error = statusCode >= 500 ? "InternalError" : statusCode == 404 ? "NotFound" : "BadRequest",
            Message = message,
            JobId = jobId
        };

        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Body = JsonSerializer.Serialize(errorResponse),
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
    }
}