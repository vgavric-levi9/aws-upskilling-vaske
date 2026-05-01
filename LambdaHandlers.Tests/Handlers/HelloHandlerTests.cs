using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using LambdaHandlers.Handlers;
using LambdaHandlers.Models;
using LambdaHandlers.Tests.TestHelpers;
using Xunit;

namespace LambdaHandlers.Tests.Handlers;

public class HelloHandlerTests
{
    [Fact]
    public void FunctionHandler_ReturnsSuccessResponse()
    {
        // Arrange
        var handler = new HelloHandler();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = "/api/hello"
        };
        var context = new LambdaTestContext();

        // Act
        var response = handler.FunctionHandler(request, context);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        Assert.NotNull(response.Body);
        
        var body = JsonSerializer.Deserialize<Response>(response.Body);
        Assert.NotNull(body);
        Assert.Equal("Hello from AWS Lambda!", body.Message);
    }

    [Fact]
    public void FunctionHandler_ReturnsValidJson()
    {
        // Arrange
        var handler = new HelloHandler();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = "/api/hello"
        };
        var context = new LambdaTestContext();

        // Act
        var response = handler.FunctionHandler(request, context);

        // Assert
        Assert.NotNull(response.Body);
        var body = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Body);
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("Message"));
        Assert.Equal("Hello from AWS Lambda!", body["Message"]);
    }

    [Fact]
    public void FunctionHandler_ReturnsCorrectHeaders()
    {
        // Arrange
        var handler = new HelloHandler();
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = "/api/hello"
        };
        var context = new LambdaTestContext();

        // Act
        var response = handler.FunctionHandler(request, context);

        // Assert
        Assert.NotNull(response.Headers);
        Assert.True(response.Headers.ContainsKey("Content-Type"));
        Assert.Equal("application/json", response.Headers["Content-Type"]);
    }
}
