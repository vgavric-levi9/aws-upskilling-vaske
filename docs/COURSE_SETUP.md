# Course Setup Guide

This guide will walk you through setting up your development environment for the AWS Upskilling 2026 course.

## Table of Contents

- [Prerequisites](#prerequisites)
- [AWS Credentials Configuration](#aws-credentials-configuration)
- [AWS CDK Installation](#aws-cdk-installation)
- [Creating New Project Solution](#creating-new-project-solution)
- [Source Control](#source-control)
- [Verification Steps](#verification-steps)

## Prerequisites

Before starting, ensure you have the following installed:

- **.NET 8 SDK** - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
- **Node.js LTS** - Required for AWS CDK CLI (download from [https://nodejs.org/](https://nodejs.org/))
- **AWS CLI** - Download from [https://aws.amazon.com/cli/](https://aws.amazon.com/cli/)

Verify installations:

```powershell
dotnet --version
node --version
npm --version
aws --version
```

## AWS Credentials Configuration

Since we use AD (Active Directory) login connected to AWS, you'll receive temporary credentials that need to be configured in your AWS credentials file.

### Step 1: Obtain Credentials from Login Page

1. Log in to the AWS Access Portal at [https://d-936717a86c.awsapps.com/start/#/?tab=accounts](https://d-936717a86c.awsapps.com/start/#/?tab=accounts) using your AD credentials
2. After successful authentication, you'll see the AWS Access Portal with two main options:

   ![AWS Access Portal](img/aws_access_portal.png)

    - **AWSAdministratorAccess** - This option will lead you to the AWS Console (web interface). This is not what you need for command-line access.
    - **Access Keys** - This option will show you your temporary credentials (access keys) that should be added to the `.aws/credentials` file. **Click on this option.**

3. After clicking "Access Keys", you'll see your temporary credentials displayed. Here's an example of how the credentials are shown:

   ![AWS Credentials Display](img/aws_credentials.png)

4. You'll receive the following information:
    - **Profile name** (e.g., `student-profile` or similar)
    - **aws_access_key_id**
    - **aws_secret_access_key**
    - **aws_session_token**

### Step 2: Configure AWS Credentials File

1. Navigate to your AWS credentials directory:
    - **Windows**: `%USERPROFILE%\.aws\credentials`
    - **Linux/Mac**: `~/.aws/credentials`

2. If the `.aws` directory doesn't exist, create it:
   ```powershell
   # Windows PowerShell
   New-Item -ItemType Directory -Path "$env:USERPROFILE\.aws" -Force
   ```

3. Open or create the `credentials` file and add your credentials with the profile renamed to `default`:

   ```ini
   [default]
   aws_access_key_id = YOUR_ACCESS_KEY_ID
   aws_secret_access_key = YOUR_SECRET_ACCESS_KEY
   aws_session_token = YOUR_SESSION_TOKEN
   ```

   **Important**: The profile must be named `[default]` (not the original profile name from the login page).

### Step 3: Configure AWS Region

1. Navigate to your AWS config directory:
    - **Windows**: `%USERPROFILE%\.aws\config`
    - **Linux/Mac**: `~/.aws/config`

2. Create or edit the `config` file and set the default region:

   ```ini
   [default]
   region = eu-west-1
   ```

   **Important**: The `eu-west-1` region is mandatory for this course.

### Step 4: Verify AWS Credentials

Test your AWS credentials configuration:

```powershell
aws sts get-caller-identity
```

This should return your AWS account information if credentials are configured correctly.

## AWS CDK Installation

AWS CDK CLI is required for deploying infrastructure.

### Step 1: Install Node.js

1. Download Node.js LTS version from [https://nodejs.org/](https://nodejs.org/)
2. Run the installer and follow the setup wizard
3. Verify installation by opening a new terminal/PowerShell window and running:
   ```powershell
   node --version
   npm --version
   ```

### Step 2: Install AWS CDK CLI

Once Node.js is installed, install CDK globally:

```powershell
npm install -g aws-cdk
```

### Step 3: Verify CDK Installation

Verify CDK is installed:

```powershell
cdk --version
```

### Alternative: Using Chocolatey (Windows)

If you have Chocolatey package manager installed:

```powershell
choco install nodejs
npm install -g aws-cdk
```

### Note

- CDK CLI is only needed for deployment commands (`cdk deploy`, `cdk synth`, etc.)
- You can still build and synthesize your CDK project using `dotnet run` without the CLI
- However, `cdk deploy` and `cdk bootstrap` require the CLI

## Creating New Project Solution

This section will guide you through creating a new .NET 8 solution with the required project structure.

### Step 1: Create Solution Directory and File

1. Create a new directory for your project:
   ```powershell
   mkdir YourProjectName
   cd YourProjectName
   ```

2. Create a new solution file:
   ```powershell
   dotnet new sln -n YourProjectName
   ```

### Step 2: Create Infrastructure Project (CDK)

1. Create the Infrastructure project:
   ```powershell
   dotnet new console -n Infrastructure -f net8.0
   ```

2. Add the Infrastructure project to the solution:
   ```powershell
   dotnet sln add Infrastructure/Infrastructure.csproj
   ```

3. Navigate to the Infrastructure directory and add CDK packages:
   ```powershell
   cd Infrastructure
   dotnet add package Amazon.CDK.Lib --version 2.140.0
   dotnet add package Constructs --version 10.3.0
   cd ..
   ```

4. Update `Infrastructure/Program.cs` with CDK app structure. **Important**: Use unique stack names to avoid conflicts when deploying to a shared AWS account. Include your initials or a unique identifier:

   ```csharp
   using Amazon.CDK;
   using Infrastructure;

   var app = new App();

   var env = new Amazon.CDK.Environment
   {
       Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
       Region = "eu-west-1"  // Must be eu-west-1
   };

   // Application stack - USE UNIQUE NAME (e.g., FirstInitial + LastName or Initials)
   new InfrastructureStack(app, "LambdaApiStack-SDjordjevic", new StackProps
   {
       Env = env
   });

   app.Synth();
   ```

   **Important**: Stack names must be unique across all students deploying to the shared AWS account. Use a naming convention like:
    - `LambdaApiStack-SDjordjevic` (First initial + Last name)
    - `LambdaApiStack-SD` (Initials only)
    - Example: If your name is John Doe, use `LambdaApiStack-JDoe` or `LambdaApiStack-JD`

### Step 3: Create Lambda Handlers Project (API)

1. Create the LambdaHandlers project:
   ```powershell
   dotnet new classlib -n LambdaHandlers -f net8.0
   ```

2. Add the LambdaHandlers project to the solution:
   ```powershell
   dotnet sln add LambdaHandlers/LambdaHandlers.csproj
   ```

3. Navigate to the LambdaHandlers directory and add Lambda packages:
   ```powershell
   cd LambdaHandlers
   dotnet add package Amazon.Lambda.APIGatewayEvents --version 2.7.3
   dotnet add package Amazon.Lambda.Core --version 2.8.1
   dotnet add package Amazon.Lambda.Serialization.SystemTextJson --version 2.4.5
   cd ..
   ```

4. Update `LambdaHandlers/LambdaHandlers.csproj` to include runtime configuration:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
       <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
       <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
     </PropertyGroup>
     <!-- Package references will be added automatically by dotnet add package -->
   </Project>
   ```

### Step 4: Create Test Project

1. Create the test project:
   ```powershell
   dotnet new xunit -n LambdaHandlers.Tests -f net8.0
   ```

2. Add the test project to the solution:
   ```powershell
   dotnet sln add LambdaHandlers.Tests/LambdaHandlers.Tests.csproj
   ```

3. Add project reference from test project to LambdaHandlers:
   ```powershell
   cd LambdaHandlers.Tests
   dotnet add reference ../LambdaHandlers/LambdaHandlers.csproj
   cd ..
   ```

### Step 5: Create Documentation Directory

Create a `docs` directory for project documentation:

```powershell
mkdir docs
```

### Final Project Structure

Your project should have the following structure:

```
YourProjectName/
├── Infrastructure/               # CDK infrastructure project
│   ├── Infrastructure.csproj
│   ├── Program.cs               # CDK app entry point (with unique stack names)
│   └── InfrastructureStack.cs  # Main infrastructure stack
├── LambdaHandlers/              # API/Lambda handlers project
│   ├── LambdaHandlers.csproj
│   ├── Handlers/                # Lambda handler classes
│   └── Models/                  # Data models
├── LambdaHandlers.Tests/        # Unit tests project
│   ├── LambdaHandlers.Tests.csproj
│   ├── Handlers/                # Test classes
│   └── TestHelpers/             # Test helper utilities
├── CI-CD/                       # CI/CD pipeline infrastructure (optional, for Option 2a)
│   └── Pipeline/                # CDK project for CodePipeline
│       ├── Pipeline.csproj
│       ├── Program.cs           # CDK app entry point
│       └── PipelineStack.cs     # CodePipeline stack definition
├── docs/                        # Documentation directory
└── YourProjectName.sln         # Solution file
```

### Step 6: Build the Solution

Verify that everything is set up correctly by building the solution:

```powershell
dotnet build
```

All projects should build successfully without errors.

## Source Control

After creating your project, you'll need to set up source control. There are two main options available:

### Option 1: Levi9 GitLab Repository with CI/CD Pipeline

This option uses Levi9's GitLab instance with a pre-defined CI/CD pipeline.

1. **Create a repository in Levi9 GitLab**:
    - Navigate to your Levi9 GitLab instance
    - Create a new repository for your project
    - Initialize the repository with your project code

2. **CI/CD Pipeline**:
    - The GitLab repository includes a pre-configured CI/CD pipeline
    - The pipeline will automatically build, test, and deploy your application
    - Configure pipeline variables as needed (AWS credentials, region, etc.)

3. **Clone and push your code**:
   ```powershell
   git init
   git remote add origin <your-gitlab-repository-url>
   git add .
   git commit -m "Initial commit"
   git push -u origin main
   ```

### Option 2: AWS CodeCommit Repository with AWS Pipeline

This option uses AWS-native services for source control and CI/CD.

#### 2a. AWS CodeCommit with AWS CodePipeline

1. **Create a CodeCommit repository**:
    - Navigate to AWS Console → CodeCommit
    - Create a new repository (e.g., `YourProjectName`)
    - Note the repository URL

2. **Set up Git credentials for CodeCommit**:
    - In CodeCommit, go to "Settings" → "HTTPS Git credentials"
    - Generate credentials and save them securely

3. **Push your code to CodeCommit**:
   ```powershell
   git init
   git remote add origin <codecommit-repository-url>
   git add .
   git commit -m "Initial commit"
   git push -u origin main
   ```

4. **Create CI/CD directory and CDK stack for CodePipeline**:
    - Create a separate `CI-CD` directory in your project root:
      ```powershell
      mkdir CI-CD
      cd CI-CD
      ```

    - Create a new CDK project for the pipeline:
      ```powershell
      dotnet new console -n Pipeline -f net8.0
      cd Pipeline
      dotnet add package Amazon.CDK.Lib --version 2.140.0
      dotnet add package Constructs --version 10.3.0
      ```

    - Create a `PipelineStack.cs` file that defines your CodePipeline infrastructure
    - Update `Program.cs` to deploy the pipeline stack with a unique name:
      ```csharp
      using Amazon.CDK;
      using Pipeline;
 
      var app = new App();
 
      var env = new Amazon.CDK.Environment
      {
          Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
          Region = "eu-west-1"
      };
 
      new PipelineStack(app, "AWS-Pipeline-SDjordjevic", new StackProps
      {
          Env = env
      });
 
      app.Synth();
      ```

   **Important**: Use the naming convention `AWS-Pipeline-SDjordjevic` (First initial + Last name) or `AWS-Pipeline-SD` (Initials only) to ensure uniqueness.

5. **Deploy the CodePipeline stack**:
   ```powershell
   cd CI-CD/Pipeline
   cdk deploy AWS-Pipeline-SDjordjevic
   ```

   This will create the AWS CodePipeline infrastructure that sources from your CodeCommit repository and includes build and deploy stages.

#### 2b. AWS CodeCommit with AWS CodeBuild

1. **Follow steps 1-3 from Option 2a** to set up CodeCommit and push your code

2. **Create AWS CodeBuild project**:
    - Navigate to AWS Console → CodeBuild
    - Create a new build project
    - Configure source: Select your CodeCommit repository
    - Configure build environment: Use managed image with .NET 8 runtime
    - Add buildspec.yml to your project root (see [PROJECT_README.md](PROJECT_README.md) for example)

3. **Build specification**:
    - The `buildspec.yml` file defines your build process
    - It should include steps to restore, build, test, and publish your .NET projects

4. **Run builds**:
    - You can trigger builds manually from CodeBuild console
    - Or integrate with CodePipeline for automated builds on commits

### Choosing Between Options

- **Option 1 (GitLab)**: Best if you're already using Levi9 GitLab and want a familiar GitLab CI/CD experience
- **Option 2a (CodeCommit + CodePipeline)**: Best for a fully AWS-native CI/CD solution with visual pipeline management
- **Option 2b (CodeCommit + CodeBuild)**: Best for more granular control over build processes and custom build configurations

## Verification Steps

Before proceeding with the course, verify that your environment is properly configured:

### 1. Verify AWS Credentials

```powershell
aws sts get-caller-identity
```

Expected output: Your AWS account ID, user ARN, and user ID.

### 2. Verify AWS Region

```powershell
aws configure get region
```

Expected output: `eu-west-1`

### 3. Verify CDK Installation

```powershell
cdk --version
```

Expected output: CDK version number (e.g., `2.x.x`)

### 4. Verify .NET 8 SDK

```powershell
dotnet --version
```

Expected output: Version starting with `8.` (e.g., `8.0.xxx`)

### 5. Verify Solution Builds

```powershell
dotnet build
```

Expected output: `Build succeeded.` with no errors.

### 6. CDK Bootstrap (Not Required for This Course)

**CDK Bootstrap is NOT needed for this course** - it has already been run during course preparation. The AWS account and `eu-west-1` region are already bootstrapped and ready for CDK deployments.

CDK Bootstrap is only needed in the following scenarios:
- When using a new AWS region on an existing account (first time deploying to that region)
- When opening a new AWS account altogether (first time setting up CDK in that account)

If you need to bootstrap in the future, you can run:
```powershell
cd Infrastructure
cdk bootstrap
cd ..
```

This sets up the necessary resources for CDK deployments in the specified region.

## Next Steps

Once setup is complete, you can:

1. Review the [Project Documentation](PROJECT_README.md) for detailed information about the project architecture
2. Start developing Lambda handlers in the `LambdaHandlers` project
3. Deploy your infrastructure using `cdk deploy` (see [PROJECT_README.md](PROJECT_README.md) for deployment instructions)

## Troubleshooting

### AWS Credentials Issues

- **Error**: "Unable to locate credentials"
    - Verify the credentials file exists at the correct path
    - Ensure the profile is named `[default]` (not the original profile name)
    - Check that all three credential values are present and correct

- **Error**: "The security token included in the request is expired"
    - AD login credentials are temporary. You'll need to obtain new credentials from the login page

### CDK Issues

- **Error**: "CDK CLI not found"
    - Verify Node.js is installed: `node --version`
    - Reinstall CDK: `npm install -g aws-cdk`

### Build Issues

- **Error**: "Project reference not found"
    - Ensure all projects are added to the solution: `dotnet sln list`
    - Verify project references: `dotnet list reference` in the test project

- **Error**: "Package not found"
    - Restore packages: `dotnet restore`

## Additional Resources

- [AWS CDK Documentation](https://docs.aws.amazon.com/cdk/)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/)
- [AWS Lambda .NET Documentation](https://docs.aws.amazon.com/lambda/latest/dg/lambda-dotnet.html)