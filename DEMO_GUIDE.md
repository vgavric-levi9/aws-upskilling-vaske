# AWS Lambda API Demo Guide

## 🎯 Demo Overview

This project demonstrates a modern serverless API built on AWS using Infrastructure as Code principles. Perfect for showcasing AWS fundamentals, serverless architecture, and DevOps best practices.

## 🏗️ Architecture Overview

### AWS Services Demonstrated

1. **AWS Lambda** - Serverless compute service
   - .NET 8 runtime
   - Event-driven execution
   - Automatic scaling
   - Pay-per-execution model

2. **API Gateway** - Managed API service
   - RESTful API endpoints
   - Built-in documentation
   - Request/response transformation
   - Built-in security features

3. **AWS CDK** - Infrastructure as Code
   - Type-safe infrastructure definition
   - Automatic CloudFormation generation
   - Resource dependency management
   - Cross-stack references

4. **CodePipeline + CodeBuild** - CI/CD Pipeline
   - Automated builds and deployments
   - Multi-stage pipeline (Source → Build → Deploy)
   - Integrated testing

5. **IAM** - Identity and Access Management
   - Least-privilege access
   - Service-to-service authentication
   - Execution roles

### Architecture Diagram
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Internet      │    │   API Gateway   │    │  Lambda Function│
│   (Client)      ├────┤   (REST API)    ├────┤   HelloHandler  │
│                 │    │                 │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                ↓
                       ┌─────────────────┐
                       │  CloudWatch     │
                       │  (Logs)         │
                       └─────────────────┘
```

## 🚀 Pre-Deployment Setup

### Prerequisites Verification
```bash
# Verify .NET 8 SDK
dotnet --version

# Verify AWS CLI configuration
aws sts get-caller-identity

# Verify CDK CLI
cdk --version

# Check AWS region (should be eu-north-1)
aws configure get region
```

## 📦 Build and Test Process

### 1. Local Testing
```bash
# Build Lambda handlers
dotnet build LambdaHandlers/LambdaHandlers.csproj

# Run unit tests with coverage
dotnet test LambdaHandlers.Tests/LambdaHandlers.Tests.csproj --verbosity normal

# Build Infrastructure
dotnet build Infrastructure/Infrastructure.csproj
```

### 2. Prepare for Deployment
```bash
# Publish Lambda handlers
dotnet publish LambdaHandlers/LambdaHandlers.csproj -c Release -o LambdaHandlers/bin/Release/net8.0/publish

# Verify CDK synthesis
cd Infrastructure
cdk synth
```

## 🌟 Deployment Demonstration

### Method 1: Manual Deployment (Recommended for Demo)

#### Step 1: Bootstrap CDK (First-time only)
```bash
cd Infrastructure
cdk bootstrap
```
**Demo Note**: Explain that bootstrap creates the necessary S3 bucket and IAM roles for CDK deployments.

#### Step 2: Deploy the Stack
```bash
cdk deploy LambdaApiStack-VGavric --require-approval never
```

**What happens during deployment:**
1. CDK packages the Lambda code
2. Uploads to S3 bootstrap bucket
3. Creates CloudFormation stack
4. Provisions Lambda function with IAM role
5. Creates API Gateway with proper integrations
6. Sets up API documentation
7. Configures permissions between services

#### Step 3: Capture Output
The deployment outputs the API Gateway URL:
```
Outputs:
LambdaApiStack-VGavric.ApiUrl = https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/
```

### Method 2: CI/CD Pipeline Deployment

#### Deploy Pipeline Stack
```bash
cdk deploy PipelineStack
```

**Pipeline Stages:**
1. **Source**: Pulls from CodeCommit repository
2. **Build**: Runs tests, builds, and publishes
3. **Deploy**: Deploys via CDK

## 🧪 Testing the Deployed API

### Test the Hello Endpoint
```bash
# Replace with your actual API Gateway URL
curl https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/api/hello

# Expected Response:
{
  "Message": "Hello from AWS Lambda!"
}
```

### Advanced Testing
```bash
# Test with verbose output
curl -v https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/api/hello

# Test response headers
curl -I https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/api/hello

# Test invalid endpoints
curl https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/api/invalid
```

## 🔍 AWS Console Exploration

### 1. Lambda Function
Navigate to AWS Lambda Console:
- **Function**: `HelloHandler`
- **Runtime**: .NET 8
- **Handler**: `LambdaHandlers::LambdaHandlers.Handlers.HelloHandler::FunctionHandler`
- **Memory**: 128 MB (default)
- **Timeout**: 30 seconds (default)

**Demo Points:**
- Show the function code (read-only view)
- Explain the execution role
- Show environment variables
- Demonstrate testing within console

### 2. API Gateway
Navigate to API Gateway Console:
- **API Name**: `Lambda API`
- **Type**: REST API
- **Stage**: `prod`

**Demo Points:**
- Show the resource tree (`/api/hello`)
- Explain method configuration (GET)
- Show integration type (Lambda Proxy)
- Test within console
- View API documentation

### 3. CloudWatch Logs
Navigate to CloudWatch Logs:
- **Log Group**: `/aws/lambda/HelloHandler`

**Demo Points:**
- Show real-time logs during API calls
- Explain log retention
- Demonstrate log insights for troubleshooting

### 4. CloudFormation Stack
Navigate to CloudFormation Console:
- **Stack Name**: `LambdaApiStack-VGavric`

**Demo Points:**
- Show generated resources
- Explain stack events
- Show drift detection
- Demonstrate rollback capabilities

## 📊 Performance and Monitoring

### CloudWatch Metrics to Show:
1. **Lambda Metrics**:
   - Invocations
   - Duration
   - Errors
   - Cold starts

2. **API Gateway Metrics**:
   - Count of requests
   - Latency (integration and API)
   - 4XX and 5XX errors

### Cost Analysis:
- **Lambda**: Pay per request + execution time
- **API Gateway**: Pay per API call
- **CloudWatch**: Pay for log storage and metrics
- **Data Transfer**: Minimal for this demo

## 🛡️ Security Features Demonstrated

1. **IAM Execution Role**: Lambda has minimum required permissions
2. **API Gateway Authorization**: Can be extended with API keys, OAuth, etc.
3. **VPC**: Lambda can be placed in VPC for network isolation (not implemented in this demo)
4. **Encryption**: Data encrypted in transit via HTTPS

## 🧹 Cleanup Process

### Method 1: Destroy via CDK
```bash
cd Infrastructure
cdk destroy LambdaApiStack-VGavric --force
```

### Method 2: Delete via CloudFormation Console
1. Navigate to CloudFormation Console
2. Select `LambdaApiStack-VGavric` stack
3. Click "Delete"
4. Confirm deletion

**Important**: Verify all resources are deleted to avoid charges.

## 🎭 Demo Script

### Introduction (2 minutes)
1. **Project Overview**: "Today I'll demonstrate a serverless API built entirely on AWS using modern .NET development practices"
2. **Architecture**: Show architecture diagram and explain each component
3. **Business Value**: Emphasize scalability, cost-effectiveness, and developer productivity

### Code Walkthrough (3 minutes)
1. **Lambda Handler**: Show the simple C# function
2. **Unit Tests**: Demonstrate comprehensive testing
3. **Infrastructure as Code**: Highlight CDK benefits over manual setup

### Build and Deployment (5 minutes)
1. **Local Testing**: Run tests to show green build
2. **CDK Synthesis**: Show infrastructure preview
3. **Deployment**: Execute `cdk deploy` and explain what's happening
4. **AWS Console**: Navigate through created resources

### Live Testing (3 minutes)
1. **API Testing**: Make live API calls
2. **Monitoring**: Show real-time logs in CloudWatch
3. **Performance**: Highlight response times and scalability

### AWS Learning Points (5 minutes)
1. **Serverless Benefits**: No server management, automatic scaling
2. **Cost Model**: Pay only for actual usage
3. **Integration**: How services work together seamlessly
4. **DevOps**: Infrastructure as Code and CI/CD benefits

### Cleanup (2 minutes)
1. **Resource Destruction**: Execute `cdk destroy`
2. **Cost Management**: Emphasize importance of cleanup
3. **Verification**: Check CloudFormation for complete removal

## 💡 Key Learning Takeaways

### AWS Concepts Demonstrated:
- **Serverless Computing**: Function-as-a-Service model
- **API Management**: Gateway patterns and best practices
- **Infrastructure as Code**: Declarative infrastructure
- **Event-Driven Architecture**: Loose coupling between components
- **Managed Services**: Focus on business logic, not infrastructure

### DevOps Practices:
- **Unit Testing**: Comprehensive test coverage
- **CI/CD**: Automated build and deployment
- **Infrastructure Versioning**: Treat infrastructure as code
- **Monitoring**: Built-in observability

### Cost Optimization:
- **Pay-per-Use**: No idle capacity costs
- **Auto-scaling**: Handle traffic spikes automatically
- **Managed Services**: Reduce operational overhead

## 🔗 Next Steps and Extensions

### Immediate Extensions:
1. Add more Lambda functions (POST, PUT, DELETE)
2. Add request validation
3. Implement API authentication
4. Add CORS configuration

### Advanced Features:
1. Add DynamoDB for data persistence
2. Implement API versioning
3. Add custom domain name
4. Set up blue/green deployments
5. Add API throttling and rate limiting

### Production Readiness:
1. Add environment-specific configurations
2. Implement proper error handling
3. Add comprehensive monitoring and alerting
4. Set up backup and disaster recovery

---

## 📝 Troubleshooting Guide

### Common Issues:

1. **CDK Bootstrap Not Run**
   - Error: "No bootstrap stack found"
   - Solution: Run `cdk bootstrap`

2. **Lambda Code Not Published**
   - Error: Lambda deployment fails
   - Solution: Run `dotnet publish` first

3. **Permission Issues**
   - Error: Access denied
   - Solution: Check AWS credentials and IAM permissions

4. **Region Mismatch**
   - Error: Resource not found
   - Solution: Verify AWS CLI region matches CDK region (eu-north-1)

### Useful Commands:
```bash
# View CDK differences before deploy
cdk diff

# List all CDK stacks
cdk list

# View CDK app configuration
cdk doctor

# Clean CDK cache
cdk destroy --all
```

This demo showcases modern AWS development practices while highlighting the power and simplicity of serverless architectures. The focus on Infrastructure as Code and comprehensive testing demonstrates professional-grade development practices essential for enterprise AWS environments.