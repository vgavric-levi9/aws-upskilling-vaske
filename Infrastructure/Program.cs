using Amazon.CDK;
using Infrastructure;

var app = new App();

var env = new Amazon.CDK.Environment
{
    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
    Region = "eu-north-1"
};

// Application stack
var infrastructureStack = new InfrastructureStack(app, "LambdaApiStack-VGavric", new StackProps
{
    Env = env
});

app.Synth();