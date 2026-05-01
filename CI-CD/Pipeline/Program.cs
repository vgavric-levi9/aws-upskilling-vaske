using Amazon.CDK;
using Pipeline;

var app = new App();

var env = new Amazon.CDK.Environment
{
    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
    Region = "eu-north-1"  // Updated to match main infrastructure region
};

// Vaske Media Processor CI/CD Pipeline Stack
var pipelineStack = new PipelineStack(app, "VaskeMediaProcessor-Pipeline", new StackProps
{
    Env = env,
    Description = "Vaske Media Processor CI/CD Pipeline - Complete automation with testing",
    Tags = new Dictionary<string, string>
    {
        ["Project"] = "VaskeMediaProcessor-CICD",
        ["Owner"] = "Vaske", 
        ["Environment"] = "Demo",
        ["Purpose"] = "AWSLearning-Pipeline"
    }
});

app.Synth();
