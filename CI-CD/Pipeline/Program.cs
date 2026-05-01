using Amazon.CDK;
using Pipeline;
var app = new App();

var env = new Amazon.CDK.Environment
{
    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
    Region = "eu-west-1"
};

new PipelineStack(app, "AWS-Pipeline-VGavric", new StackProps
{
    Env = env
});

app.Synth();
