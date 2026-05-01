using Amazon.CDK;
using Infrastructure;

var app = new App();

var env = new Amazon.CDK.Environment
{
    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
    Region = "eu-north-1"
};

// Vaske Media Processor Application Stack (main infrastructure only)
var mediaProcessorStack = new InfrastructureStack(app, "VaskeMediaProcessor-App", new StackProps
{
    Env = env,
    Description = "Vaske Serverless Media Processor - Main application stack",
    Tags = new Dictionary<string, string>
    {
        ["Project"] = "VaskeMediaProcessor",
        ["Owner"] = "Vaske",
        ["Environment"] = "Demo",
        ["Purpose"] = "AWSLearning"
    }
});

// CI/CD Pipeline is now in separate CI-CD/Pipeline project

app.Synth();