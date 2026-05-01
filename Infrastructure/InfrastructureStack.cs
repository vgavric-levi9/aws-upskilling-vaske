using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace Infrastructure;

public class InfrastructureStack : Stack
{
    public InfrastructureStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        // Create Lambda function with .NET 8 runtime
        var helloHandler = new Function(this, "HelloHandler", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "LambdaHandlers::LambdaHandlers.Handlers.HelloHandler::FunctionHandler",
            Code = Code.FromAsset("../LambdaHandlers/bin/Release/net8.0/publish"),
            FunctionName = "HelloHandler"
        });

        // Create REST API Gateway
        var api = new RestApi(this, "LambdaApi", new RestApiProps
        {
            RestApiName = "Lambda API",
            Description = "API Gateway for Lambda handlers"
        });

        // Create API Gateway resource and method
        var helloResource = api.Root.AddResource("api").AddResource("hello");
        helloResource.AddMethod("GET", new LambdaIntegration(helloHandler));

        // Create API Gateway documentation parts for the hello endpoint
        var apiDoc = new CfnDocumentationPart(this, "ApiDocumentation", new CfnDocumentationPartProps
        {
            Location = new CfnDocumentationPart.LocationProperty
            {
                Type = "API"
            },
            RestApiId = api.RestApiId,
            Properties = "{\"description\":\"Lambda API - API Gateway for Lambda handlers\"}"
        });

        var methodDoc = new CfnDocumentationPart(this, "HelloMethodDocumentation", new CfnDocumentationPartProps
        {
            Location = new CfnDocumentationPart.LocationProperty
            {
                Type = "METHOD",
                Path = "/api/hello",
                Method = "GET"
            },
            RestApiId = api.RestApiId,
            Properties = "{\"summary\":\"Hello endpoint\",\"description\":\"Returns a hello message\"}"
        });

        // Create documentation version (must be created after documentation parts exist)
        // Using node.addDependency to ensure proper ordering
        var documentationVersion = new CfnDocumentationVersion(this, "ApiDocumentationVersion", new CfnDocumentationVersionProps
        {
            DocumentationVersion = "1.0.0",
            RestApiId = api.RestApiId
        });
        
        // Ensure documentation parts are created before the version
        documentationVersion.Node.AddDependency(apiDoc);
        documentationVersion.Node.AddDependency(methodDoc);

        // Create deployment - this will automatically create/update the default stage
        var deployment = new Deployment(this, "ApiDeployment", new DeploymentProps
        {
            Api = api
        });
        
        // Add a stage (only if it doesn't exist)
        // Note: RestApi creates a default stage, so we'll use that or create a new one
        deployment.Node.AddDependency(apiDoc);
        deployment.Node.AddDependency(methodDoc);

        // Output API Gateway URL
        new CfnOutput(this, "ApiUrl", new CfnOutputProps
        {
            Value = api.Url,
            Description = "API Gateway endpoint URL"
        });
    }
}
