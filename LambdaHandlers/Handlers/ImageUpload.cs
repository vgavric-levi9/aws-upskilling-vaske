using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using LambdaHandlers.Models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace LambdaHandlers.Handlers;

public class ImageUpload
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDBContext _dynamoDbContext;
    private readonly string _inputBucket;

    public ImageUpload() : this(new AmazonS3Client(), new AmazonDynamoDBClient())
    {
    }

    public ImageUpload(IAmazonS3 s3Client, IAmazonDynamoDB dynamoDbClient)
    {
        _s3Client = s3Client;
        _dynamoDbClient = dynamoDbClient;
        _dynamoDbContext = new DynamoDBContext(_dynamoDbClient);
        _inputBucket = Environment.GetEnvironmentVariable("INPUT_BUCKET") ?? "media-processor-input-bucket";
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation($"Processing upload request. Method: {request.HttpMethod}, Path: {request.Path}");

            if (string.IsNullOrEmpty(request.Body))
            {
                return CreateErrorResponse(400, "Request body is required");
            }

            var jobId = Guid.NewGuid().ToString();
            var fileName = ExtractFileNameFromHeaders(request.Headers);
            var contentType = ExtractContentType(request.Headers);

            if (!IsValidImageType(contentType))
            {
                return CreateErrorResponse(400, "Invalid content type. Only JPEG, PNG, and GIF images are supported");
            }

            byte[] imageData;
            try
            {
                imageData = Convert.FromBase64String(request.Body);
            }
            catch (FormatException)
            {
                return CreateErrorResponse(400, "Invalid base64 image data");
            }

            var s3Key = $"{jobId}/{fileName}";
            
            await UploadToS3(s3Key, imageData, contentType, context);

            var processingJob = new ProcessingJob
            {
                JobId = jobId,
                Status = "pending",
                OriginalFileName = fileName,
                InputBucket = _inputBucket,
                InputKey = s3Key,
                OriginalFileSize = imageData.Length,
                ContentType = contentType,
                UploadedAt = DateTime.UtcNow
            };

            await _dynamoDbContext.SaveAsync(processingJob);

            var response = new UploadResponse
            {
                JobId = jobId,
                Status = "pending",
                Message = "Image uploaded successfully. Processing will begin shortly.",
                UploadedAt = processingJob.UploadedAt
            };

            context.Logger.LogInformation($"Upload completed successfully. JobId: {jobId}");

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
            context.Logger.LogError($"Error processing upload: {ex.Message}");
            context.Logger.LogError($"Stack trace: {ex.StackTrace}");
            return CreateErrorResponse(500, "Internal server error occurred during upload");
        }
    }

    private async Task UploadToS3(string key, byte[] imageData, string contentType, ILambdaContext context)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = _inputBucket,
            Key = key,
            InputStream = new MemoryStream(imageData),
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };
        
        putRequest.Metadata.Add("uploaded-by", "ImageUploadHandler");
        putRequest.Metadata.Add("upload-timestamp", DateTime.UtcNow.ToString("O"));

        context.Logger.LogInformation($"Uploading to S3: {_inputBucket}/{key}");
        await _s3Client.PutObjectAsync(putRequest);
    }

    private static string ExtractFileNameFromHeaders(IDictionary<string, string> headers)
    {
        if (headers?.TryGetValue("x-filename", out var fileName) == true && !string.IsNullOrEmpty(fileName))
        {
            return SanitizeFileName(fileName);
        }
        
        return $"image_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg";
    }

    private static string ExtractContentType(IDictionary<string, string> headers)
    {
        if (headers?.TryGetValue("Content-Type", out var contentType) == true && !string.IsNullOrEmpty(contentType))
        {
            return contentType.Split(';')[0].Trim().ToLowerInvariant();
        }
        
        return "image/jpeg";
    }

    private static bool IsValidImageType(string contentType)
    {
        var validTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
        return validTypes.Contains(contentType.ToLowerInvariant());
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrEmpty(sanitized) ? $"image_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg" : sanitized;
    }

    private static APIGatewayProxyResponse CreateErrorResponse(int statusCode, string message)
    {
        var errorResponse = new ErrorResponse
        {
            Error = statusCode >= 500 ? "InternalError" : "BadRequest",
            Message = message
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