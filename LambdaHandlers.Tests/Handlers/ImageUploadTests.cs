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

public class ImageUploadTests
{
    private readonly Mock<IAmazonS3> _mockS3;
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDB;
    private readonly Mock<DynamoDBContext> _mockDynamoContext;
    private readonly ImageUpload _handler;

    public ImageUploadTests()
    {
        _mockS3 = new Mock<IAmazonS3>();
        _mockDynamoDB = new Mock<IAmazonDynamoDB>();
        _mockDynamoContext = new Mock<DynamoDBContext>(_mockDynamoDB.Object);
        _handler = new ImageUpload(_mockS3.Object, _mockDynamoDB.Object);
    }

    [Fact]
    public async Task FunctionHandler_ValidImageUpload_ReturnsSuccess()
    {
        // Arrange
        var validBase64Image = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF }); // Mock JPEG header
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/api/upload",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "image/jpeg" },
                { "x-filename", "test.jpg" }
            },
            Body = validBase64Image
        };
        var context = new LambdaTestContext();

        _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
               .ReturnsAsync(new PutObjectResponse());

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        
        var responseBody = JsonSerializer.Deserialize<UploadResponse>(response.Body);
        Assert.NotNull(responseBody);
        Assert.NotEmpty(responseBody.JobId);
        Assert.Equal("pending", responseBody.Status);
        Assert.Contains("uploaded successfully", responseBody.Message);

        _mockS3.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/api/upload",
            Body = ""
        };
        var context = new LambdaTestContext();

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(400, response.StatusCode);
        
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(response.Body);
        Assert.NotNull(errorResponse);
        Assert.Equal("BadRequest", errorResponse.Error);
        Assert.Equal("Request body is required", errorResponse.Message);
    }

    [Fact]
    public async Task FunctionHandler_InvalidContentType_ReturnsBadRequest()
    {
        // Arrange
        var validBase64Image = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF });
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/api/upload",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" }
            },
            Body = validBase64Image
        };
        var context = new LambdaTestContext();

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(400, response.StatusCode);
        
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(response.Body);
        Assert.NotNull(errorResponse);
        Assert.Equal("BadRequest", errorResponse.Error);
        Assert.Contains("Invalid content type", errorResponse.Message);
    }

    [Fact]
    public async Task FunctionHandler_InvalidBase64_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/api/upload",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "image/jpeg" }
            },
            Body = "invalid-base64-data"
        };
        var context = new LambdaTestContext();

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(400, response.StatusCode);
        
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(response.Body);
        Assert.NotNull(errorResponse);
        Assert.Equal("BadRequest", errorResponse.Error);
        Assert.Contains("Invalid base64 image data", errorResponse.Message);
    }

    [Fact]
    public async Task FunctionHandler_S3Exception_ReturnsInternalError()
    {
        // Arrange
        var validBase64Image = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF });
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/api/upload",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "image/jpeg" }
            },
            Body = validBase64Image
        };
        var context = new LambdaTestContext();

        _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
               .ThrowsAsync(new AmazonS3Exception("S3 Error"));

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(500, response.StatusCode);
        
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(response.Body);
        Assert.NotNull(errorResponse);
        Assert.Equal("InternalError", errorResponse.Error);
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/jpg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/gif", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/pdf", false)]
    public async Task FunctionHandler_ContentTypeValidation_ReturnsExpectedResult(string contentType, bool shouldSucceed)
    {
        // Arrange
        var validBase64Image = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF });
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/api/upload",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", contentType }
            },
            Body = validBase64Image
        };
        var context = new LambdaTestContext();

        if (shouldSucceed)
        {
            _mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                   .ReturnsAsync(new PutObjectResponse());
        }

        // Act
        var response = await _handler.FunctionHandler(request, context);

        // Assert
        if (shouldSucceed)
        {
            Assert.Equal(200, response.StatusCode);
        }
        else
        {
            Assert.Equal(400, response.StatusCode);
        }
    }
}