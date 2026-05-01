using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.S3;
using Amazon.S3.Model;
using LambdaHandlers.Handlers;
using LambdaHandlers.Models;
using LambdaHandlers.Tests.TestHelpers;
using Moq;
using Xunit;

namespace LambdaHandlers.Tests.Handlers;

public class StatusQueryTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDB;
    private readonly Mock<DynamoDBContext> _mockDynamoContext;
    private readonly Mock<IAmazonS3> _mockS3;
    private readonly StatusQuery _handler;

    public StatusQueryTests()
    {
        _mockDynamoDB = new Mock<IAmazonDynamoDB>();
        _mockDynamoContext = new Mock<DynamoDBContext>(_mockDynamoDB.Object);
        _mockS3 = new Mock<IAmazonS3>();
        _handler = new StatusQuery(_mockDynamoDB.Object, _mockS3.Object);
    }

    [Fact]
    public async Task FunctionHandler_ExistingJob_ReturnsJobStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = $"/api/status/{jobId}",
            PathParameters = new Dictionary<string, string> { { "jobId", jobId } }
        };
        var context = new LambdaTestContext();

        var processingJob = new ProcessingJob
        {
            JobId = jobId,
            Status = "processing",
            OriginalFileName = "test.jpg",
            UploadedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.Headers["Content-Type"]);

        var statusResponse = JsonSerializer.Deserialize<StatusResponse>(response.Body);
        Assert.NotNull(statusResponse);
        Assert.Equal(jobId, statusResponse.JobId);
        Assert.Equal("processing", statusResponse.Status);
        Assert.Equal("test.jpg", statusResponse.OriginalFileName);
    }

    [Fact]
    public async Task FunctionHandler_CompletedJob_ReturnsJobStatusWithResult()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = $"/api/status/{jobId}",
            PathParameters = new Dictionary<string, string> { { "jobId", jobId } }
        };
        var context = new LambdaTestContext();

        var processingJob = new ProcessingJob
        {
            JobId = jobId,
            Status = "completed",
            OriginalFileName = "test.jpg",
            UploadedAt = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-7),
            ProcessingCompletedAt = DateTime.UtcNow.AddMinutes(-2),
            OutputBucket = "output-bucket",
            OutputKey = "processed/test.jpg",
            ProcessedWidth = 800,
            ProcessedHeight = 600,
            ProcessedFileSize = 150000
        };

        _mockS3.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
               .ReturnsAsync("https://s3.amazonaws.com/presigned-url");

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(200, response.StatusCode);

        var statusResponse = JsonSerializer.Deserialize<StatusResponse>(response.Body);
        Assert.NotNull(statusResponse);
        Assert.Equal("completed", statusResponse.Status);
        Assert.NotNull(statusResponse.Result);
        Assert.Equal(800, statusResponse.Result.Width);
        Assert.Equal(600, statusResponse.Result.Height);
        Assert.Equal(150000, statusResponse.Result.FileSize);
        Assert.NotNull(statusResponse.Result.OutputUrl);
    }

    [Fact]
    public async Task FunctionHandler_NonExistentJob_ReturnsNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = $"/api/status/{jobId}",
            PathParameters = new Dictionary<string, string> { { "jobId", jobId } }
        };
        var context = new LambdaTestContext();

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(404, response.StatusCode);

        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(response.Body);
        Assert.NotNull(errorResponse);
        Assert.Equal("NotFound", errorResponse.Error);
        Assert.Equal("Job not found", errorResponse.Message);
        Assert.Equal(jobId, errorResponse.JobId);
    }

    [Fact]
    public async Task FunctionHandler_MissingJobId_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = "/api/status/",
            PathParameters = new Dictionary<string, string>()
        };
        var context = new LambdaTestContext();

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(400, response.StatusCode);

        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(response.Body);
        Assert.NotNull(errorResponse);
        Assert.Equal("BadRequest", errorResponse.Error);
        Assert.Equal("JobId is required", errorResponse.Message);
    }

    [Fact]
    public async Task FunctionHandler_FailedJob_ReturnsJobStatusWithError()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = $"/api/status/{jobId}",
            PathParameters = new Dictionary<string, string> { { "jobId", jobId } }
        };
        var context = new LambdaTestContext();

        var processingJob = new ProcessingJob
        {
            JobId = jobId,
            Status = "failed",
            OriginalFileName = "test.jpg",
            UploadedAt = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-7),
            ProcessingCompletedAt = DateTime.UtcNow.AddMinutes(-5),
            ErrorMessage = "Image processing failed due to invalid format"
        };

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(200, response.StatusCode);

        var statusResponse = JsonSerializer.Deserialize<StatusResponse>(response.Body);
        Assert.NotNull(statusResponse);
        Assert.Equal("failed", statusResponse.Status);
        Assert.Equal("Image processing failed due to invalid format", statusResponse.ErrorMessage);
        Assert.Null(statusResponse.Result);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("processing")]
    [InlineData("completed")]
    [InlineData("failed")]
    public async Task FunctionHandler_DifferentStatuses_ReturnsCorrectStatus(string status)
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = $"/api/status/{jobId}",
            PathParameters = new Dictionary<string, string> { { "jobId", jobId } }
        };
        var context = new LambdaTestContext();

        var processingJob = new ProcessingJob
        {
            JobId = jobId,
            Status = status,
            OriginalFileName = "test.jpg",
            UploadedAt = DateTime.UtcNow
        };

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(200, response.StatusCode);

        var statusResponse = JsonSerializer.Deserialize<StatusResponse>(response.Body);
        Assert.NotNull(statusResponse);
        Assert.Equal(status, statusResponse.Status);
    }
}