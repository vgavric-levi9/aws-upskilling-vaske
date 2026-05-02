# 🎯 Serverless Media Processor

A comprehensive **AWS learning project** demonstrating modern cloud architecture patterns with .NET 8, implementing a complete serverless image processing workflow using AWS services and Infrastructure as Code.

## 📋 Project Overview

This project is designed as a **hands-on learning experience** for AWS services integration, focusing on:
- **Serverless Architecture**: Event-driven processing with AWS Lambda
- **Infrastructure as Code**: AWS CDK with C# for reproducible deployments  
- **CI/CD Best Practices**: Automated testing and deployment pipelines
- **Security**: IAM roles with least-privilege principles
- **Observability**: CloudWatch monitoring and logging

> **Learning Focus**: Understanding AWS service integrations rather than complex business logic

## 🏗️ Architecture

The application implements a complete image processing workflow:

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   User/Client   │ ──▶│   API Gateway    │ ──▶│ Upload Lambda   │
└─────────────────┘    │  (REST API)      │    │ (ImageUpload)   │
                       │                  │    └─────────┬───────┘
                       │ • POST /api/upload              │
                       │ • GET /api/status/{jobId}       ▼
                       └──────────┬───────────────┬─────────────────┐
                                  │               │      S3 Input   │
                                  │               │     Bucket      │
                                  │               └─────────┬───────┘
                                  ▼                         │
                       ┌──────────────────┐                │ S3 Event
                       │ Status Lambda    │                │ Notification
                       │ (StatusQuery)    │                ▼
                       └─────────┬────────┘    ┌─────────────────┐
                                 │             │ Processor Lambda│
                                 │             │ (MediaProcessor)│
                                 │             └─────────┬───────┘
                                 │                       │
                                 ▼                       ▼
                       ┌─────────────────┐    ┌─────────────────┐
                       │   DynamoDB      │    │  S3 Output      │
                       │ (Job Tracking)  │    │   Bucket        │
                       └─────────────────┘    └─────────────────┘
                                │                       │
                                └───── CloudWatch ──────┘
                                     (Monitoring)
```

### 🔄 Workflow Details

1. **Upload**: User sends base64-encoded image to `POST /api/upload`
2. **Storage**: Upload Lambda stores image in S3 input bucket and creates job record
3. **Trigger**: S3 event notification automatically triggers Media Processor Lambda
4. **Processing**: Image is resized (800x600) and stored in S3 output bucket
5. **Tracking**: Job status is updated in DynamoDB throughout the process
6. **Query**: Users can check processing status via `GET /api/status/{jobId}`
7. **Monitoring**: All operations are logged and monitored via CloudWatch

## 🏗️ Components & Responsibilities

### AWS Services

| Service | Purpose | Configuration |
|---------|---------|---------------|
| **API Gateway** | REST API endpoints for upload/status queries | CORS enabled, API documentation |
| **Lambda Functions** | Serverless compute for image processing | .NET 8 runtime, specialized IAM roles |
| **S3 Buckets** | Object storage for images | Input/output buckets with encryption |
| **DynamoDB** | Job metadata and status tracking | Pay-per-request billing, no PITR |
| **CloudWatch** | Monitoring, logging, and observability | Log groups, dashboards, alarms |
| **CodePipeline** | Automated CI/CD deployment | GitHub integration with CodeStar |

### Lambda Functions

#### 🔼 ImageUpload Lambda
- **Handler**: `LambdaHandlers.Handlers.ImageUpload::FunctionHandler`
- **Trigger**: API Gateway POST requests
- **Responsibilities**:
  - Validate and decode base64 image data
  - Upload image to S3 input bucket
  - Create processing job record in DynamoDB
- **Permissions**: S3 PutObject (input bucket), DynamoDB PutItem
- **Timeout**: 1 minute | **Memory**: 512MB

#### ⚙️ MediaProcessor Lambda  
- **Handler**: `LambdaHandlers.Handlers.MediaProcessor::FunctionHandler`
- **Trigger**: S3 event notifications (object creation)
- **Responsibilities**:
  - Process images (resize to 800x600)
  - Store processed image in S3 output bucket
  - Update job status and metadata in DynamoDB
- **Permissions**: S3 GetObject (input), S3 PutObject (output), DynamoDB GetItem/UpdateItem
- **Timeout**: 5 minutes | **Memory**: 1024MB

#### 🔍 StatusQuery Lambda
- **Handler**: `LambdaHandlers.Handlers.StatusQuery::FunctionHandler`  
- **Trigger**: API Gateway GET requests
- **Responsibilities**:
  - Query job status from DynamoDB
  - Generate presigned URLs for processed images
  - Return processing status and metadata
- **Permissions**: DynamoDB GetItem, S3 GetObject (presigned URLs)
- **Timeout**: 30 seconds | **Memory**: 256MB

## 🔐 Security & Permissions

### IAM Roles (Least-Privilege Principle)

Each Lambda function has a dedicated IAM role with minimal required permissions:

#### ImageUpload Role
```
✅ Basic Lambda execution
✅ S3:PutObject (input bucket only)
✅ DynamoDB:PutItem (jobs table only)
❌ No access to output bucket or other tables
```

#### MediaProcessor Role  
```
✅ Basic Lambda execution
✅ S3:GetObject (input bucket)
✅ S3:PutObject (output bucket)
✅ DynamoDB:GetItem, UpdateItem (jobs table)
❌ No access to other AWS services
```

#### StatusQuery Role
```
✅ Basic Lambda execution  
✅ DynamoDB:GetItem (jobs table, read-only)
✅ S3:GetObject (output bucket, presigned URLs only)
❌ No write permissions anywhere
```

### Network Security
- **S3 Buckets**: Server-side encryption (AES256), public access blocked
- **API Gateway**: CORS configured for web client access
- **DynamoDB**: Encrypted at rest, VPC endpoints available

## 🚀 Deployment Guide

### Prerequisites

1. **.NET 8 SDK**: `dotnet --version` should show 8.x
2. **AWS CLI**: Configured with appropriate credentials
3. **CDK Bootstrap**: One-time setup for CDK deployments
4. **CodeStar Connection**: GitHub integration (already configured)

### 🔧 One-Time Setup

```powershell
# 1. CDK Bootstrap (first time only)
$env:CDK_DEFAULT_ACCOUNT = "765891906457"
$env:CDK_DEFAULT_REGION = "eu-north-1" 
cdk bootstrap aws://765891906457/eu-north-1 --qualifier vgavric

# 2. Deploy CI/CD Pipeline (creates automated deployment)
cd "CI-CD\Pipeline"
dotnet build
cdk deploy VaskeMediaProcessor-Pipeline
```

### 🚀 Automated Deployment (Recommended)

After pipeline setup, deployment is **fully automated**:

```bash
# Any push to 'main' branch triggers automatic deployment
git add .
git commit -m "deploy changes"
git push origin main

# Pipeline stages:
# 1. Source: GitHub → CodePipeline  
# 2. TestAndBuild: Unit tests (must pass) + build
# 3. Deploy: CDK deployment of infrastructure
```

### 🔄 Manual Deployment (Alternative)

```powershell
# Build Lambda handlers
cd LambdaHandlers  
dotnet publish -c Release

# Deploy infrastructure
cd ..\Infrastructure
cdk deploy VaskeMediaProcessor-App
```

### 🗑️ Cleanup/Destroy

```powershell
# Option 1: Use destroy pipeline (recommended)
# AWS Console → CodePipeline → VaskeMediaProcessor-Destroy → Release Change

# Option 2: Manual cleanup
cd Infrastructure
cdk destroy VaskeMediaProcessor-App

# Option 3: Remove everything including pipeline
cd ..\CI-CD\Pipeline  
cdk destroy VaskeMediaProcessor-Pipeline
```

## 📊 Monitoring & Observability

### CloudWatch Integration

- **Log Groups**: Separate log groups for each Lambda function with 1-week retention
- **Metrics**: Automatic AWS service metrics (Lambda duration, API Gateway latency, DynamoDB operations)
- **Dashboard**: `VaskeMediaProcessor-Monitoring` with key metrics visualization
- **Alarms**: Can be configured for error rates, processing failures, and performance thresholds

### Key Metrics to Monitor

| Metric | Service | Threshold |
|--------|---------|-----------|  
| Lambda Duration | All Functions | < timeout values |
| API Gateway 4xx/5xx | REST API | < 5% error rate |
| DynamoDB Throttling | Processing Table | 0 throttled requests |
| S3 Upload Failures | Buckets | 0 failed uploads |

## 🧪 Testing

### Unit Tests Coverage

```bash
# Run all unit tests  
dotnet test

# Test coverage includes:
# ✅ ImageUpload: Validation, S3 upload, DynamoDB operations
# ✅ MediaProcessor: Image processing, error handling  
# ✅ StatusQuery: Job retrieval, presigned URL generation
```

### Integration Testing

```bash
# Upload test image
curl -X POST https://<api-url>/api/upload \
  -H "Content-Type: image/jpeg" \
  -H "X-Filename: test.jpg" \
  -d "<base64-encoded-image>"

# Check processing status  
curl https://<api-url>/api/status/{jobId}
```

## 📁 Project Structure

```
aws-upskilling-vaske/
├── 🏗️ Infrastructure/              # CDK infrastructure definitions
│   ├── InfrastructureStack.cs      # Main AWS resources (API, Lambda, S3, DynamoDB)
│   ├── Permissions/                # IAM roles and policies
│   │   ├── LambdaRoles.cs         # Specialized Lambda IAM roles  
│   │   ├── S3Permissions.cs       # S3 bucket policies
│   │   └── DynamoDbPermissions.cs # DynamoDB access policies
│   └── Program.cs                 # CDK app entry point
│
├── ⚡ LambdaHandlers/              # Lambda function implementations  
│   ├── Handlers/                  # Lambda handler classes
│   │   ├── ImageUpload.cs         # Upload and S3 storage
│   │   ├── MediaProcessor.cs      # Image processing and resizing
│   │   └── StatusQuery.cs         # Job status queries
│   └── Models/                    # Data transfer objects
│       ├── ProcessingJob.cs       # DynamoDB entity model
│       └── Response.cs            # API response models
│
├── 🧪 LambdaHandlers.Tests/        # Comprehensive unit tests
│   ├── Handlers/                  # Test classes for each handler
│   └── TestHelpers/               # Mocking and test utilities
│
├── 🚀 CI-CD/                       # DevOps and deployment automation
│   └── Pipeline/                  # CodePipeline CDK stack  
│       ├── PipelineStack.cs       # CI/CD pipeline definition
│       └── Program.cs             # Pipeline deployment entry point
│
├── 📚 docs/                        # Project documentation
│   ├── CI_CD_GUIDE.md            # Detailed CI/CD setup guide
│   ├── COURSE_SETUP.md           # Development environment setup  
│   └── PROJECT_README.md         # Technical implementation details
│
└── 🔧 Configuration Files
    ├── buildspec.yml              # CodeBuild build specification
    ├── *.sln                      # Visual Studio solution
    └── cdk.json                   # CDK configuration
```

## 📡 API Reference

### Upload Endpoint
```
POST /api/upload
Content-Type: image/jpeg|png|gif  
X-Filename: image.jpg (optional)
Body: <base64-encoded-image-data>

Response:
{
  "jobId": "uuid",
  "status": "pending", 
  "message": "Image uploaded successfully",
  "uploadedAt": "2026-05-02T19:02:00Z"
}
```

### Status Query Endpoint  
```
GET /api/status/{jobId}

Response:
{
  "jobId": "uuid",
  "status": "completed|pending|processing|failed",
  "originalFileName": "image.jpg",
  "processedFileSize": 245760,
  "processedWidth": 800,
  "processedHeight": 600, 
  "presignedUrl": "https://s3.../processed-image.jpg",
  "uploadedAt": "2026-05-02T19:02:00Z",
  "processingCompletedAt": "2026-05-02T19:02:30Z"
}
```

## 🎓 Learning Outcomes

This project demonstrates mastery of:

- **☁️ AWS Services**: API Gateway, Lambda, S3, DynamoDB, CloudWatch integration
- **🏗️ Infrastructure as Code**: AWS CDK with C#, reusable constructs
- **🔐 Security**: IAM roles, least-privilege access, resource-based policies  
- **⚙️ DevOps**: CI/CD pipelines, automated testing, GitHub integration
- **📊 Observability**: CloudWatch logs, metrics, and monitoring best practices
- **🧪 Testing**: Unit testing with mocking, integration testing strategies

## 🛠️ Technology Stack

- **.NET 8**: Lambda runtime and CDK infrastructure
- **AWS CDK**: Infrastructure as Code with C#
- **ImageSharp**: Image processing library
- **xUnit**: Unit testing framework with Moq
- **AWS SDK**: Service integrations (S3, DynamoDB, Lambda)
- **GitHub**: Source control with CodeStar Connections
- **CodePipeline**: Automated CI/CD deployment

## 🤝 Contributing

This is a learning project. To make changes:

1. Create feature branch: `git checkout -b feature/enhancement`
2. Make changes and test: `dotnet test`
3. Commit with clear messages: `git commit -m "add: new feature"`
4. Push to trigger pipeline: `git push origin main`
5. Monitor deployment in AWS Console

## 📞 Support & Resources

- **AWS CDK Documentation**: https://docs.aws.amazon.com/cdk/
- **Project Issues**: Use GitHub Issues for questions
- **AWS Best Practices**: Follow AWS Well-Architected Framework principles

---

**🎯 Remember**: This is a **learning project** focused on AWS service integration and deployment automation rather than complex business logic. Keep enhancements simple and educational!
