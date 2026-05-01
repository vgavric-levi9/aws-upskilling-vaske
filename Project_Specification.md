# Project Specification: Serverless Media Processor

## Project Overview

This is a **learning project** designed to help you gain hands-on experience with AWS services and deployment processes. The primary objective is to understand how different AWS services work together, not to solve complex problems or build production-ready applications.

**Important**: The focus of this project is on:
- Learning AWS services and their use cases
- Understanding Infrastructure as Code with AWS CDK
- Gaining experience with deployment processes
- Following AWS best practices

**Complexity is not the goal** - simplicity and learning are. Keep your solutions straightforward and focused on proper AWS service integration rather than complex business logic.

## Project Workflow

The Serverless Media Processor follows this workflow:

1. **API Gateway**: User sends image as payload in API request (e.g., `POST /api/upload`)
2. **Upload Lambda**: Lambda handler invoked through API Gateway receives the image payload and uploads it to S3 input bucket
3. **S3**: Receives image upload from the Lambda handler and stores it in the "input" bucket
4. **EventBridge**: Detects the S3 upload event and triggers the processing Lambda function
5. **Processing Lambda**: 
   - Processes the media (resize, transform)
   - Artificially prolongs processing to 30-40 seconds for demonstration purposes
   - Stores metadata in DynamoDB
6. **DynamoDB**: Stores processing metadata (status, timestamps, file info, processing results)
7. **API Gateway**: Provides REST API endpoints for users to query individual media processing statuses (e.g., `GET /api/status/{jobId}`)
8. **CloudWatch**: Comprehensive monitoring and observability throughout the workflow

## AWS Services to Cover and Their Roles

### Core Services

- **S3 (Simple Storage Service)**
  - **Input bucket**: Receives image uploads from Lambda handler (uploaded via API Gateway)
  - **Output bucket**: Stores processed images after transformation
  - Learn bucket configuration, permissions, and lifecycle policies

- **EventBridge**
  - Detects S3 upload events and triggers processing Lambda function
  - Learn event-driven architecture and event routing patterns
  - Understand event rules and targets

- **Lambda**
  - **Upload Lambda**: Receives image payload from API Gateway and uploads to S3 input bucket
  - **Processing Lambda**: Serverless function for media processing (resize, transform)
  - Integrates with DynamoDB to store metadata
  - Learn Lambda configuration, environment variables, and IAM roles

- **DynamoDB**
  - Stores processing metadata including:
    - Processing status (pending, processing, completed, failed)
    - Timestamps (upload time, processing start, completion time)
    - File information (original filename, size, type)
    - Processing results (output file location, dimensions)
  - Learn table design, primary keys, and query patterns

- **API Gateway**
  - **Upload endpoint**: `POST /api/upload` - Receives image payload and invokes upload Lambda handler
  - **Status endpoint**: `GET /api/status/{jobId}` - Queries processing status (invokes status Lambda that reads from DynamoDB)
  - Learn API design, integration with Lambda/DynamoDB, request/response handling, and API documentation

- **CloudWatch**
  - Comprehensive monitoring and observability:
    - **Lambda function logs and metrics**: Duration, errors, invocations, memory usage
    - **DynamoDB metrics**: Read/write capacity, throttling, item counts
    - **S3 metrics**: Bucket size, number of objects, requests
    - **API Gateway logs and metrics**: Latency, 4xx/5xx errors, request counts
    - **Custom metrics from Lambda**: Processing time, file sizes, processing status
    - **CloudWatch Dashboards**: Visualize all metrics together
    - **CloudWatch Alarms**: Monitor failed processing, high error rates, DynamoDB throttling
    - **CloudWatch Logs Insights**: Query and analyze logs across services
    - **CloudWatch Log Groups**: Organize logs by service for better management

## Project Structure Requirements

Your project should follow this directory structure:

```
YourProjectName/
├── Infrastructure/               # CDK infrastructure project
│   ├── Infrastructure.csproj
│   ├── Program.cs               # CDK app entry point (with unique stack names)
│   └── InfrastructureStack.cs   # Main infrastructure stack
├── LambdaHandlers/              # API/Lambda handlers project
│   ├── LambdaHandlers.csproj
│   ├── Handlers/                # Lambda handler classes
│   │   ├── ImageUpload.cs      # Upload Lambda handler (receives image from API Gateway, uploads to S3)
│   │   ├── MediaProcessor.cs   # Processing Lambda handler (processes images from S3)
│   │   └── StatusQuery.cs       # Status query Lambda handler (queries DynamoDB via API Gateway)
│   └── Models/                  # Data models
├── LambdaHandlers.Tests/        # Unit tests project
│   ├── LambdaHandlers.Tests.csproj
│   └── Handlers/                # Test classes
├── CI-CD/                       # CI/CD pipeline infrastructure (optional)
│   └── Pipeline/                # CDK project for CodePipeline
│       ├── Pipeline.csproj
│       ├── Program.cs
│       └── PipelineStack.cs
├── docs/                        # Documentation directory
└── YourProjectName.sln          # Solution file
```

### Technology Stack

- **.NET 8** - For Lambda functions and CDK infrastructure
- **AWS CDK (C#)** - For Infrastructure as Code
- **Source Control**: Choose between Levi9 GitLab or AWS CodeCommit (see [COURSE_SETUP.md](docs/COURSE_SETUP.md))

## Implementation Guidelines

### Infrastructure as Code

- **CDK best practices**: Use CDK constructs properly, avoid hardcoded values
- **Stack organization**: Organize your infrastructure logically
- **Unique naming**: Use unique stack names (e.g., `LambdaApiStack-SDjordjevic`) to avoid conflicts
- **Documentation**: Comment your CDK code to explain resource creation

### Testing

- **Unit tests**: Write unit tests for Lambda handlers
- **Integration testing**: Test AWS service integrations where possible
- **Manual testing**: Test the complete workflow end-to-end

## Deliverables

Your project should include:

1. **API Gateway**
   - Upload endpoint (`POST /api/upload`) that receives image payload
   - Status query endpoint (`GET /api/status/{jobId}`)
   - Integration with Lambda handlers
   - API documentation

2. **Lambda Functions**
   - **Upload Lambda**: Receives image from API Gateway and uploads to S3
   - **Processing Lambda**: Processes media from S3
   - **Status Query Lambda**: Queries DynamoDB for processing status
   - Proper error handling and logging

3. **S3 Buckets**
   - Input bucket for image uploads (from Lambda handler)
   - Output bucket for processed images
   - Proper bucket policies and IAM permissions for Lambda access

4. **EventBridge Rule**
   - Rule to detect S3 upload events
   - Target configuration to trigger Lambda function

5. **DynamoDB Table**
   - Table design for storing processing metadata
   - Appropriate primary key and indexes

6. **CloudWatch Configuration**
   - Log groups for Lambda and API Gateway
   - Basic dashboards for monitoring
   - Alarms for critical metrics

7. **CDK Infrastructure Code**
   - Well-structured CDK code
   - Proper use of CDK constructs
   - Comments and documentation

8. **CI/CD Pipeline Setup**
   - Source control repository
   - CI/CD pipeline configuration
   - Automated build and deployment

9. **Documentation**
    - README with project overview
    - Setup instructions
    - Architecture diagram (optional but recommended)

## Evaluation Criteria

Your project will be evaluated based on:

1. **Proper Use of AWS Services**
   - Correct configuration of each service
   - Appropriate use of service features
   - Integration between services

2. **Correct Infrastructure Setup**
   - CDK code structure and organization
   - Infrastructure deploys successfully
   - All services are properly connected

3. **Deployment Process Understanding**
   - Ability to deploy infrastructure using CDK
   - Understanding of deployment process
   - Troubleshooting deployment issues

4. **Code Quality and Documentation**
   - Clean, readable code
   - Proper error handling
   - Documentation of setup and usage

## Optional Enhancements (If You Want to Improve the App)

Once you have the core workflow working, consider these optional enhancements to learn additional AWS services:

### ECS Fargate and VPC Networking

Add a containerized admin dashboard and networking infrastructure:
- **ECS Fargate**: Run a containerized admin dashboard application to display processing status
  - Container deployment, task definitions, and service configuration
  - Displays real-time processing status from DynamoDB
- **VPC Networking**: Network configuration for ECS Fargate tasks
  - Subnets (public and private)
  - Security groups
  - Internet gateway and/or NAT gateway
  - Route tables
  - Learn network isolation, security, and connectivity patterns

**Learning Value**: Understand containerized applications, container orchestration, and network architecture in AWS.

### Application Load Balancer (ALB)

Add load balancing for ECS Fargate tasks (requires ECS Fargate setup first):
- Distribute traffic across multiple ECS tasks
- Health checks and automatic failover
- SSL/TLS termination
- Integration with ECS service for dynamic target registration

**Learning Value**: Understand load balancing concepts, health checks, and high availability patterns.

### CloudWatch Advanced Features

Enhance your monitoring setup:
- **CloudWatch Dashboards**: Create custom dashboards to visualize all metrics together (Lambda, DynamoDB, S3, API Gateway, and optionally ECS)
- **CloudWatch Alarms**: Set up alarms for critical metrics (failed processing, high error rates, DynamoDB throttling, Lambda timeouts)
- **CloudWatch Logs Insights**: Use Logs Insights to query and analyze logs across services
- **CloudWatch Log Groups**: Organize logs by service (Lambda, API Gateway, and optionally ECS) for better management

**Learning Value**: Learn comprehensive observability and monitoring practices in AWS.

### SNS and SQS for User Notifications

Implement user notifications when processing is complete:
- **SNS (Simple Notification Service)**: Send notifications to users when processing is complete
- **SQS (Simple Queue Service)**: Queue notification messages for reliable delivery
- **Integration Pattern**: Processing Lambda publishes to SNS topic → SNS sends to SQS queue → Notification service processes queue and sends emails/SMS

**Learning Value**: Learn pub/sub patterns, message queuing, and decoupled architecture.

## Getting Started

1. Review the [Course Setup Guide](docs/COURSE_SETUP.md) for environment setup
2. Review the [Project Documentation](docs/PROJECT_README.md) for technical details
3. Start with the core workflow:
   - API Gateway → Upload Lambda → S3 (image upload)
   - S3 → EventBridge → Processing Lambda → DynamoDB (processing)
   - API Gateway → Status Lambda → DynamoDB (status queries)
4. Implement monitoring with CloudWatch
5. Add optional enhancements (ECS Fargate, VPC, ALB, SNS/SQS) as you progress

## Additional Resources

- [AWS CDK Documentation](https://docs.aws.amazon.com/cdk/)
- [AWS Lambda Documentation](https://docs.aws.amazon.com/lambda/)
- [AWS API Gateway Documentation](https://docs.aws.amazon.com/apigateway/)
- [AWS ECS Documentation](https://docs.aws.amazon.com/ecs/)
- [AWS CloudWatch Documentation](https://docs.aws.amazon.com/cloudwatch/)

---

**Remember**: This is a learning project. Focus on understanding AWS services and deployment processes rather than building complex solutions. Keep it simple, keep it educational!
