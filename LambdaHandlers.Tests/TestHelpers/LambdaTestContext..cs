using Amazon.Lambda.Core;

namespace LambdaHandlers.Tests.TestHelpers;

public class LambdaTestContext : ILambdaContext
{
    public string AwsRequestId { get; } = Guid.NewGuid().ToString();
    public IClientContext ClientContext { get; } = null!;
    public string FunctionName { get; } = "TestFunction";
    public string FunctionVersion { get; } = "1";
    public ICognitoIdentity Identity { get; } = null!;
    public string InvokedFunctionArn { get; } = "arn:aws:lambda:us-east-1:123456789012:function:TestFunction";
    public ILambdaLogger Logger { get; } = new TestLambdaLogger();
    public string LogGroupName { get; } = "/aws/lambda/TestFunction";
    public string LogStreamName { get; } = Guid.NewGuid().ToString();
    public int MemoryLimitInMB { get; } = 512;
    public TimeSpan RemainingTime { get; } = TimeSpan.FromMinutes(5);
}

public class TestLambdaLogger : ILambdaLogger
{
    public void Log(string message)
    {
        // Test logger - can be extended to capture logs if needed
    }

    public void LogLine(string message)
    {
        // Test logger - can be extended to capture logs if needed
    }
}