# 🎥 Serverless Media Processor - Complete Demo Guide
[Project_Specification.md](Project_Specification.md)
## 🎯 Project Overview

This **Serverless Media Processor** demonstrates a complete AWS serverless architecture for image processing. It showcases how multiple AWS services work together to create a scalable, event-driven, and cost-effective solution.

### 🏗️ Architecture Flow
```
User → API Gateway → Upload Lambda → S3 Input Bucket
                                         ↓
                              EventBridge Rule
                                         ↓
                         Processing Lambda ← DynamoDB (metadata)
                                         ↓
                                S3 Output Bucket
[Project_Specification.md](Project_Specification.md)
User → API Gateway → Status Lambda ← DynamoDB (query status)
```

### 🎓 AWS Learning Objectives

This project demonstrates **8 core AWS services** working together:
1. **API Gateway** - Managed REST API service
2. **Lambda** - Serverless compute (3 functions)
3. **S3** - Object storage (input/output buckets)
4. **EventBridge** - Event-driven architecture
5. **DynamoDB** - NoSQL database for metadata
6. **CloudWatch** - Monitoring and logging
7. **IAM** - Security and permissions
8. **CDK** - Infrastructure as Code

## 🚀 Pre-Deployment Setup

### Prerequisites Check
```bash
# Verify .NET 8 SDK
dotnet --version  # Should show 8.x.x

# Verify AWS CLI
aws sts get-caller-identity  # Should show your account

# Verify CDK CLI
cdk --version  # Should show 2.x.x

# Check region (should be eu-north-1)
aws configure get region
```

## 📦 Build and Deploy Process

### Step 1: Build All Components
```bash
# Build Lambda handlers
dotnet build LambdaHandlers/LambdaHandlers.csproj

# Build Infrastructure
dotnet build Infrastructure/Infrastructure.csproj

# Publish Lambda handlers for deployment
dotnet publish LambdaHandlers/LambdaHandlers.csproj -c Release -o LambdaHandlers/bin/Release/net8.0/publish
```

### Step 2: CDK Bootstrap (First-time only)
```bash
cd Infrastructure
cdk bootstrap
```

### Step 3: Deploy Infrastructure
```bash
cd Infrastructure
cdk deploy LambdaApiStack-VGavric --require-approval never
```

**🎯 Demo Talking Points During Deployment:**
- **Infrastructure as Code**: All resources defined in C# code
- **Automated Dependencies**: CDK handles service connections automatically
- **Security**: IAM roles created with least-privilege access
- **Scalability**: All services auto-scale based on demand

### Deployment Creates These Resources:
- ✅ **3 Lambda Functions**: ImageUpload, MediaProcessor, StatusQuery
- ✅ **2 S3 Buckets**: Input and Output buckets
- ✅ **1 DynamoDB Table**: MediaProcessingJobs
- ✅ **1 API Gateway**: REST API with 2 endpoints
- ✅ **1 EventBridge Rule**: S3 → Lambda integration
- ✅ **IAM Roles**: Secure service-to-service communication
- ✅ **CloudWatch Log Groups**: Automatic logging setup

### Capture These Outputs:
```
ApiUrl: https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/
UploadEndpoint: https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/api/upload
StatusEndpoint: https://xxxxxxxxx.execute-api.eu-north-1.amazonaws.com/prod/api/status/{jobId}
InputBucket: media-processor-input-765891906457
OutputBucket: media-processor-output-765891906457
DynamoDBTable: MediaProcessingJobs
```

## 🧪 Testing the Complete Workflow

### 1. Upload Image Test

#### Create Test Image (Base64)
```bash
# Create a small test image file (or use existing)
echo "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==" > test_image_base64.txt
```

#### Upload via API Gateway
```bash
curl -X POST https://YOUR_API_URL/api/upload \
  -H "Content-Type: image/png" \
  -H "X-Filename: test-demo.png" \
  --data "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=="
```

**Expected Response:**
```json
{
  "JobId": "12345678-1234-1234-1234-123456789abc",
  "Status": "pending",
  "Message": "Image uploaded successfully. Processing will begin shortly.",
  "UploadedAt": "2026-05-01T19:00:00Z"
}
```

**🎯 Demo Talking Points:**
- **API Gateway**: Handles HTTP requests, validates input
- **Lambda Cold Start**: First invocation may be slower
- **S3 Integration**: Image stored securely in S3 bucket
- **DynamoDB**: Job metadata written immediately

### 2. Monitor Processing Status

```bash
# Query status immediately after upload
curl https://YOUR_API_URL/api/status/12345678-1234-1234-1234-123456789abc

# Status progression: pending → processing → completed (takes ~35 seconds)
```

**Status Responses:**

**Pending:**
```json
{
  "JobId": "12345678-1234-1234-1234-123456789abc",
  "Status": "pending",
  "OriginalFileName": "test-demo.png",
  "UploadedAt": "2026-05-01T19:00:00Z"
}
```

**Processing:**
```json
{
  "JobId": "12345678-1234-1234-1234-123456789abc", 
  "Status": "processing",
  "OriginalFileName": "test-demo.png",
  "UploadedAt": "2026-05-01T19:00:00Z",
  "ProcessingStartedAt": "2026-05-01T19:00:05Z"
}
```

**Completed:**
```json
{
  "JobId": "12345678-1234-1234-1234-123456789abc",
  "Status": "completed", 
  "OriginalFileName": "test-demo.png",
  "UploadedAt": "2026-05-01T19:00:00Z",
  "ProcessingStartedAt": "2026-05-01T19:00:05Z",
  "ProcessingCompletedAt": "2026-05-01T19:00:40Z",
  "Result": {
    "Width": 800,
    "Height": 600,
    "FileSize": 147250,
    "OutputUrl": "https://s3.amazonaws.com/presigned-url-to-processed-image"
  }
}
```

## 🔍 AWS Console Deep Dive

### 1. Lambda Functions Analysis
**Navigate to Lambda Console →**

#### **ImageUploadHandler**
- **Runtime**: .NET 8
- **Memory**: 512 MB
- **Timeout**: 1 minute
- **Environment Variables**: INPUT_BUCKET, DYNAMODB_TABLE
- **Permissions**: S3 PutObject, DynamoDB PutItem

#### **MediaProcessorHandler**
- **Runtime**: .NET 8  
- **Memory**: 1024 MB (image processing needs more memory)
- **Timeout**: 5 minutes (processing takes ~35 seconds)
- **Environment Variables**: OUTPUT_BUCKET, DYNAMODB_TABLE
- **Permissions**: S3 GetObject/PutObject, DynamoDB GetItem/UpdateItem

#### **StatusQueryHandler**
- **Runtime**: .NET 8
- **Memory**: 256 MB (lightweight queries)
- **Timeout**: 30 seconds
- **Environment Variables**: DYNAMODB_TABLE
- **Permissions**: DynamoDB GetItem, S3 GetPreSignedURL

**🎯 Demo Talking Points:**
- **Right-sizing**: Different memory allocations based on function needs
- **Environment Configuration**: No hardcoded values
- **Security**: Each function has minimal required permissions
- **Observability**: CloudWatch integration automatic

### 2. S3 Bucket Configuration
**Navigate to S3 Console →**

#### **Input Bucket** (`media-processor-input-{AccountId}`)
- **EventBridge Enabled**: ✅ (triggers processing)
- **CORS Configured**: ✅ (web upload support)
- **Lifecycle**: Auto-delete objects on stack destruction
- **Security**: Only Lambda upload function can write

#### **Output Bucket** (`media-processor-output-{AccountId}`)
- **Public Access**: Blocked (secure by default)
- **Presigned URLs**: Generated for secure temporary access
- **Organization**: `/processed/{jobId}/` structure

**🎯 Demo Talking Points:**
- **Event Integration**: S3 automatically triggers processing
- **Security**: No public access, controlled via IAM
- **Cost Optimization**: Lifecycle policies for cleanup
- **Organization**: Structured storage pattern

### 3. DynamoDB Table Deep Dive
**Navigate to DynamoDB Console → Tables →**

#### **MediaProcessingJobs Table**
- **Partition Key**: JobId (String)
- **Billing Mode**: On-Demand (pay per request)
- **Point-in-Time Recovery**: Disabled (demo environment)

**Sample Record:**
```json
{
  "JobId": "12345678-1234-1234-1234-123456789abc",
  "Status": "completed",
  "OriginalFileName": "test-demo.png", 
  "InputBucket": "media-processor-input-765891906457",
  "InputKey": "12345678-1234-1234-1234-123456789abc/test-demo.png",
  "OutputBucket": "media-processor-output-765891906457",
  "OutputKey": "processed/12345678-1234-1234-1234-123456789abc/resized_test-demo.png",
  "OriginalFileSize": 12345,
  "ProcessedFileSize": 147250,
  "ProcessedWidth": 800,
  "ProcessedHeight": 600,
  "ContentType": "image/png",
  "UploadedAt": "2026-05-01T19:00:00.000Z",
  "ProcessingStartedAt": "2026-05-01T19:00:05.000Z",
  "ProcessingCompletedAt": "2026-05-01T19:00:40.000Z"
}
```

**🎯 Demo Talking Points:**
- **Single Table Design**: One table for all job metadata
- **Scalability**: On-demand billing scales automatically
- **Performance**: Single-digit millisecond response times
- **Flexibility**: NoSQL allows easy schema evolution

### 4. EventBridge Rules
**Navigate to EventBridge Console → Rules →**

#### **MediaProcessor-S3Upload Rule**
- **Event Source**: aws.s3
- **Event Type**: Object Created
- **Target**: MediaProcessorHandler Lambda
- **Filter**: Only input bucket events

**Event Pattern:**
```json
{
  "source": ["aws.s3"],
  "detail-type": ["Object Created"],
  "detail": {
    "bucket": {
      "name": ["media-processor-input-765891906457"]
    }
  }
}
```

**🎯 Demo Talking Points:**
- **Decoupled Architecture**: S3 and Lambda don't know about each other
- **Event-Driven**: Processing starts automatically on upload
- **Scalability**: Can handle thousands of concurrent uploads
- **Reliability**: AWS manages event delivery

### 5. API Gateway Configuration
**Navigate to API Gateway Console →**

#### **Endpoints Structure:**
```
/api
├── /upload (POST)
│   ├── Integration: Lambda (ImageUploadHandler)
│   ├── Headers: Content-Type, X-Filename
│   └── CORS: Enabled
└── /status
    └── /{jobId} (GET)
        ├── Integration: Lambda (StatusQueryHandler)
        └── Path Parameter: jobId
```

#### **Test API Gateway Directly:**
1. Go to API Gateway Console
2. Select your API → Resources
3. Click on POST /api/upload → TEST
4. Add headers and body
5. Execute test

**🎯 Demo Talking Points:**
- **Managed Service**: No servers to manage
- **Built-in Features**: Throttling, caching, monitoring
- **Integration**: Direct Lambda proxy integration
- **Documentation**: Auto-generated OpenAPI spec

### 6. CloudWatch Monitoring
**Navigate to CloudWatch Console →**

#### **Log Groups:**
- `/aws/lambda/ImageUploadHandler`
- `/aws/lambda/MediaProcessorHandler` 
- `/aws/lambda/StatusQueryHandler`
- `/aws/apigateway/welcome`

#### **Key Metrics to Show:**
1. **Lambda Metrics:**
   - Invocations (request count)
   - Duration (execution time)
   - Errors (failure rate)
   - Cold starts

2. **API Gateway Metrics:**
   - Count (total requests)
   - Latency (response times)
   - 4XXError (client errors)
   - 5XXError (server errors)

3. **DynamoDB Metrics:**
   - ConsumedReadCapacityUnits
   - ConsumedWriteCapacityUnits
   - ItemCount
   - ThrottledRequests

**🎯 Demo Talking Points:**
- **Real-time Monitoring**: Live metrics and logs
- **Troubleshooting**: Detailed error tracking
- **Performance**: Identify bottlenecks and optimization opportunities
- **Alerting**: Can set up alarms for critical metrics

## 💰 Cost Analysis Demo

### **Serverless Cost Benefits:**
```
Traditional Server (24/7):
- EC2 t3.medium: $30/month
- Application Load Balancer: $16/month  
- RDS db.t3.micro: $13/month
Total: ~$59/month minimum

Serverless (Demo Usage):
- Lambda (1000 requests): $0.20
- API Gateway (1000 requests): $3.50
- DynamoDB (On-demand, light usage): $0.25
- S3 (1GB storage + requests): $0.25
Total: ~$4.20/month for same usage
```

**🎯 Demo Talking Points:**
- **Pay-per-Use**: Only pay when processing images
- **No Idle Costs**: Zero cost when not in use
- **Auto-scaling**: Handles traffic spikes without over-provisioning
- **Managed Services**: No patching, maintenance, or monitoring costs

## 🛡️ Security Deep Dive

### **Security Features Demonstrated:**

1. **IAM Roles**: Each Lambda has minimal required permissions
2. **VPC**: Can be enabled for network isolation
3. **Encryption**: 
   - S3 server-side encryption (AES-256)
   - DynamoDB encryption at rest
   - HTTPS for all API calls
4. **Access Control**:
   - S3 presigned URLs for temporary access
   - API Gateway can add authentication/authorization
   - No hardcoded credentials

**🎯 Demo Talking Points:**
- **Least Privilege**: Each service has only necessary permissions
- **Defense in Depth**: Multiple layers of security
- **Compliance**: Meets enterprise security requirements
- **Auditability**: All actions logged in CloudTrail

## 🧹 Stack Cleanup and Cost Management

### **Destroy Infrastructure:**
```bash
cd Infrastructure
cdk destroy LambdaApiStack-VGavric --force
```

### **Verify Complete Cleanup:**
1. **CloudFormation Console**: Check stack is deleted
2. **S3 Console**: Buckets should be removed (auto-delete enabled)  
3. **DynamoDB Console**: Table should be removed
4. **Lambda Console**: Functions should be removed
5. **API Gateway Console**: API should be removed

**🎯 Demo Talking Points:**
- **Infrastructure as Code**: Easy to recreate identical environments
- **Cost Control**: Complete resource cleanup prevents charges
- **Reproducibility**: Can deploy/destroy multiple times
- **Version Control**: Infrastructure changes tracked in Git

## 🎪 Demo Script (25-minute presentation)

### **Introduction (3 minutes)**
1. **Problem Statement**: "How do we build scalable image processing without managing servers?"
2. **Solution Overview**: Show architecture diagram
3. **AWS Services**: Highlight 8 core services working together

### **Architecture Walkthrough (5 minutes)**
1. **Event-Driven Flow**: Upload → Process → Store → Query
2. **Service Integration**: How services communicate
3. **Scaling**: Automatic based on demand
4. **Cost Model**: Pay-per-use vs. always-on servers

### **Live Deployment (7 minutes)**
1. **Infrastructure as Code**: Show CDK deployment
2. **Resource Creation**: Watch CloudFormation progress
3. **Service Dependencies**: Explain automatic IAM setup
4. **Cost Awareness**: Highlight resource tagging

### **Live Testing (5 minutes)**
1. **Upload Image**: Demonstrate API call
2. **Monitor Processing**: Show status changes
3. **View Results**: Download processed image
4. **Real-time Logs**: Show CloudWatch integration

### **AWS Console Tour (3 minutes)**
1. **Lambda Functions**: Configuration and monitoring
2. **S3 Buckets**: Event integration and security
3. **DynamoDB**: Data structure and performance
4. **CloudWatch**: Metrics and troubleshooting

### **Cleanup and Q&A (2 minutes)**
1. **Infrastructure Destruction**: Show CDK destroy
2. **Cost Management**: Emphasize cleanup importance
3. **Questions**: Address AWS service questions

## 🔗 Next Steps and Extensions

### **Immediate Improvements:**
1. **Add Authentication**: API Gateway + Cognito
2. **File Validation**: Size limits and type checking
3. **Error Handling**: Retry logic and dead letter queues
4. **Monitoring**: CloudWatch alarms and dashboards

### **Advanced Features:**
1. **Multi-format Support**: JPEG, PNG, GIF, WebP
2. **Batch Processing**: SQS queues for large volumes
3. **CDN Integration**: CloudFront for global distribution
4. **Machine Learning**: Rekognition for content analysis

### **Production Readiness:**
1. **Environment Separation**: Dev/staging/prod stacks
2. **Security Hardening**: VPC, WAF, advanced IAM
3. **Disaster Recovery**: Cross-region replication
4. **Performance Optimization**: Reserved capacity, caching

---

## 📝 Troubleshooting Guide

### **Common Issues:**

#### **Deployment Fails:**
```bash
# Check CDK bootstrap
cdk doctor

# Verify AWS credentials
aws sts get-caller-identity

# Check Lambda code build
dotnet publish LambdaHandlers/LambdaHandlers.csproj -c Release
```

#### **Lambda Timeout:**
- Check CloudWatch logs for memory/CPU usage
- Increase memory allocation if needed
- Verify image processing library performance

#### **API Gateway 5XX Errors:**
- Check Lambda function logs
- Verify IAM permissions
- Test Lambda function directly

#### **DynamoDB Issues:**
- Check read/write capacity metrics
- Verify IAM permissions for Lambda
- Review partition key usage patterns

### **Useful Debug Commands:**
```bash
# View CDK differences before deploy
cdk diff

# Get CloudFormation outputs
aws cloudformation describe-stacks --stack-name LambdaApiStack-VGavric

# Test Lambda function directly
aws lambda invoke --function-name ImageUploadHandler response.json

# Query DynamoDB directly
aws dynamodb scan --table-name MediaProcessingJobs
```

---

## 🎓 Key Learning Outcomes

### **AWS Service Integration:**
- How EventBridge enables loose coupling
- IAM role-based service-to-service communication  
- S3 event notifications and processing triggers
- API Gateway Lambda proxy integration patterns

### **Serverless Architecture Benefits:**
- **Scalability**: Automatic scaling without configuration
- **Cost Efficiency**: Pay-per-execution pricing model
- **Operational Excellence**: No server management required
- **Development Velocity**: Focus on business logic, not infrastructure

### **Infrastructure as Code:**
- **Reproducibility**: Identical environments every time
- **Version Control**: Infrastructure changes tracked
- **Documentation**: Code serves as living documentation
- **Collaboration**: Team-friendly infrastructure management

### **Production Considerations:**
- **Monitoring**: Built-in observability with CloudWatch
- **Security**: Defense-in-depth security model
- **Cost Management**: Understanding serverless pricing
- **Troubleshooting**: Distributed system debugging techniques

This demo effectively showcases modern cloud-native development practices while highlighting the power and simplicity of AWS serverless services. The focus on learning AWS services rather than complex coding makes it perfect for upskilling scenarios! 🚀