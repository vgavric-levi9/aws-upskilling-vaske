# AWS Lambda API with CDK

A lightweight AWS Lambda API project using .NET 8, API Gateway, and AWS CDK for infrastructure as code.

## Architecture

```
API Gateway REST API
└── /api/hello → HelloHandler Lambda (simple function handler)
```

- Each endpoint has its own dedicated Lambda function
- Simple Lambda handlers without ASP.NET Core framework
- API Gateway REST API with built-in documentation
- Infrastructure defined using AWS CDK (C#)

## Project Structure

```
upskiling_test/
├── LambdaHandlers/               # Lambda handlers project
│   ├── Handlers/
│   │   └── HelloHandler.cs      # Lambda handler for /api/hello
│   └── Models/
│       └── Response.cs          # Simple response model
│
├── LambdaHandlers.Tests/         # Unit tests project
│   ├── Handlers/
│   │   └── HelloHandlerTests.cs # Unit tests for HelloHandler
│   └── TestHelpers/
│       └── LambdaTestContext.cs # Mock Lambda context helper
│
└── Infrastructure/               # CDK project
    ├── InfrastructureStack.cs   # Main stack with API Gateway + Lambda
    ├── PipelineStack.cs          # CI/CD pipeline stack
    ├── Program.cs                # CDK app entry point
    └── cdk.json                  # CDK configuration
├── buildspec.yml                 # CodeBuild build specification
```

## Prerequisites

- .NET 8 SDK
- AWS CLI configured with appropriate credentials
- AWS CDK CLI (optional, for deployment)

## Building the Project

### Build Lambda Handlers

```bash
dotnet build LambdaHandlers/LambdaHandlers.csproj
```

### Run Unit Tests

```bash
dotnet test LambdaHandlers.Tests/LambdaHandlers.Tests.csproj
```

### Build CDK Infrastructure

```bash
dotnet build Infrastructure/Infrastructure.csproj
```

## Deployment

### Prerequisites for Deployment

1. **AWS CLI Configuration**: Ensure your AWS credentials are configured:
   ```bash
   aws configure
   ```

2. **CDK Bootstrap** (first time only):
   ```bash
   cd Infrastructure
   cdk bootstrap
   ```

### Manual Deployment

1. Build the Lambda handlers project first:
   ```bash
   dotnet build LambdaHandlers/LambdaHandlers.csproj
   dotnet publish LambdaHandlers/LambdaHandlers.csproj -c Release -o LambdaHandlers/bin/Release/net8.0/publish
   ```

2. Deploy using CDK:
   ```bash
   cd Infrastructure
   cdk deploy LambdaApiStack
   ```

### CI/CD Pipeline Deployment

The project includes an AWS CodePipeline setup for automated CI/CD.

#### Prerequisites for CI/CD

1. **CodeCommit Repository**: Ensure your CodeCommit repository `Upskilling2026Test` exists and contains your code on the `master` branch.

2. **Deploy Pipeline Stack**:
   ```bash
   cd Infrastructure
   cdk deploy PipelineStack
   ```

   The pipeline is configured to:
    - Source from CodeCommit repository: `Upskilling2026Test`
    - Monitor branch: `master`
    - Automatically trigger on commits to `master`

#### Pipeline Stages

The CI/CD pipeline consists of three stages:

1. **Source**: Pulls code from CodeCommit repository (`Upskilling2026Test`, branch `master`)
2. **Build**:
    - Builds Lambda handlers and Infrastructure projects
    - Runs unit tests
    - Publishes Lambda handlers
3. **Deploy**:
    - Deploys the CDK stack using `cdk deploy`
    - Updates Lambda functions and API Gateway

#### Pipeline Configuration

- **Build Project**: `UpskillingTestBuild` (uses `buildspec.yml`)
- **Deploy Project**: `UpskillingTestCdkDeploy` (CDK deployment)
- **Artifact Bucket**: Automatically created S3 bucket for pipeline artifacts

#### Modifying the Pipeline

To change the source repository or branch, edit `Infrastructure/PipelineStack.cs`:

```csharp
Repository = Repository.FromRepositoryName(
    this,
    "SourceRepo",
    "your-repo-name"  // Change repository name
),
Branch = "master"  // Change branch name
```

#### Buildspec Configuration

The `buildspec.yml` file in the root directory defines the build process:
- Installs .NET 8 and Node.js 20
- Restores NuGet packages
- Builds all projects
- Runs unit tests
- Publishes Lambda handlers

### After Deployment

The CDK stack will output the API Gateway URL. You can test the endpoint:

```bash
curl https://<api-id>.execute-api.<region>.amazonaws.com/prod/api/hello
```

Expected response:
```json
{
  "Message": "Hello from AWS Lambda!"
}
```

## API Documentation

API Gateway documentation is available in the AWS Console:

1. Navigate to API Gateway in AWS Console
2. Select your API
3. Go to "Documentation" section
4. View the OpenAPI specification

The documentation includes:
- Endpoint: `GET /api/hello`
- Response schema
- Example responses

## Testing

### Unit Tests

Run unit tests locally:
```bash
dotnet test
```

Tests verify:
- Handler returns 200 status code
- Response contains correct JSON structure
- Headers are set correctly

### Integration Testing

After deployment, test the API endpoint:
```bash
curl https://<api-url>/api/hello
```

## Verification Steps

1. **Before Deployment**:
    - Run unit tests: `dotnet test`
    - Verify all tests pass
    - Build Lambda handlers: `dotnet build LambdaHandlers/LambdaHandlers.csproj`

2. **After Deployment**:
    - Test `/api/hello` endpoint returns JSON response
    - Verify response status code is 200
    - Access API Gateway documentation in console
    - Check Lambda logs in CloudWatch
    - Verify API Gateway logs and metrics

## Cleanup

To remove all resources:

```bash
cd Infrastructure
cdk destroy
```

## Project Details

### Lambda Handler

The `HelloHandler` is a simple Lambda function that:
- Receives `APIGatewayProxyRequest`
- Returns `APIGatewayProxyResponse` with JSON body
- Uses minimal dependencies for fast cold starts

### CDK Stack

The `InfrastructureStack` creates:
- Lambda function with .NET runtime
- REST API Gateway
- API Gateway route: `GET /api/hello`
- API Gateway documentation with OpenAPI spec
- IAM roles and permissions

## Troubleshooting

### Build Issues

- Ensure .NET 8 SDK is installed: `dotnet --version`
- Restore packages: `dotnet restore`

### Deployment Issues

- Verify AWS credentials: `aws sts get-caller-identity`
- Check CDK bootstrap: `cdk bootstrap` (first time only)
- Ensure Lambda code is built before deployment

### Runtime Issues

- Check CloudWatch Logs for Lambda function
- Verify API Gateway logs
- Check IAM permissions for Lambda execution

## Next Steps

- Add more endpoints by creating additional Lambda handlers
- Add request validation
- Add CORS configuration if needed
- Add API Gateway throttling/rate limiting
- Add custom domain name