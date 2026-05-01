# Vaske Media Processor - CI/CD Implementation Guide

## 🎯 Šta je Implementirano

Kompletna CI/CD arhitektura bazirana na AWS-native servisima sa fokusom na kvalitet, sigurnost i automatizaciju.

## 🏗️ CI/CD Pipeline Architecture

```
📁 Local Source Code (Manual Package)
     ↓ (PowerShell script)
📤 S3 Artifacts Bucket (Source Upload) 
     ↓ (manual trigger)
🔄 CodePipeline: VaskeMediaProcessor-CICD
     ↓
🧪 CodeBuild: VaskeMediaProcessor-TestAndBuild
     ├─ dotnet restore & build
     ├─ dotnet test (MUST PASS!)
     └─ dotnet publish
     ↓ (artifacts)
🚀 CodeBuild: VaskeMediaProcessor-Deploy
     ├─ cdk diff
     └─ cdk deploy VaskeMediaProcessor-App
```

### Dodatni Pipeline za Cleanup
```
🗑️ CodePipeline: VaskeMediaProcessor-Destroy (manual only)
     ↓
💥 CodeBuild: VaskeMediaProcessor-Destroy
     └─ cdk destroy --force
```

## 📋 Pipeline Stages

### 1. **Source Stage**
- **Input**: S3 artifacts bucket (manual upload)
- **Trigger**: Manual via PowerShell script
- **Output**: Source code for build

### 2. **Test & Build Stage** 
- **BuildSpec**: `buildspec.yml`
- **Faze**:
  - `install`: .NET 8 + Node.js 20 + CDK
  - `pre_build`: **dotnet test** (Pipeline FAILS ako testovi fail)
  - `build`: dotnet build & publish
  - `post_build`: Priprema artifakata
- **Kritično**: Testovi su obavezni - bez passing testova nema deployment-a

### 3. **Deploy Stage**
- **BuildSpec**: `buildspec.deploy.yml` 
- **Faze**:
  - CDK diff preview
  - CDK deploy sa --require-approval never
  - Potvrda uspešnog deployment-a

## 🔐 IAM Permisije (Minimalne Potrebne)

### CodeBuild Projects

#### Test & Build Project
```yaml
Permisije: NIE AWS API pozivi
Razlog: Samo build, test i priprema artifakata
```

#### Deploy Project  
```yaml
ManagedPolicy: AdministratorAccess
Razlog: CDK deploy mora kreirati/ažurirati AWS resurse
Sigurnost: Ograničeno na deployment context
```

#### Destroy Project
```yaml
ManagedPolicy: AdministratorAccess  
Razlog: CDK destroy mora obrisati sve resurse
Sigurnost: Manual trigger only
```

### CodePipeline
```yaml
S3: Read/Write za artifact bucket
CodeCommit: Read za source kod  
CodeBuild: Start/Stop build projekata
```

## 🚀 Deployment Instructions (No CodeCommit Required!)

### Step 1: Deploy CI/CD Infrastructure
```bash
cd CI-CD/Pipeline
dotnet build
cdk deploy VaskeMediaProcessor-Pipeline --require-approval never
```

### Step 2: Deploy Application via Pipeline
```powershell
cd CI-CD/Pipeline

# Deploy the application
.\deploy-pipeline.ps1 deploy

# Check pipeline status
.\deploy-pipeline.ps1 status
```

### Step 3: Automatic Processing
Pipeline executes these stages:
1. ✅ Package & upload source code to S3
2. ✅ Run unit tests (MUST PASS) 
3. ✅ Build application
4. ✅ Deploy infrastructure
5. ✅ Deploy Lambda functions

## 📊 Monitoring CI/CD

### Pipeline Status
```bash
# AWS Console URLs (nakon deployment-a):
https://eu-north-1.console.aws.amazon.com/codesuite/codepipeline/pipelines/VaskeMediaProcessor-CICD/view
```

### Build Logs
```bash
# CloudWatch Log Groups:
- /aws/codebuild/VaskeMediaProcessor-TestAndBuild
- /aws/codebuild/VaskeMediaProcessor-Deploy  
- /aws/codebuild/VaskeMediaProcessor-Destroy
```

### Test Results
- CodeBuild Reports: VisualStudio TRX format
- Automatski test reports u AWS Console

## 🔄 Development Workflow

### Happy Path
1. **Developer** makes code changes locally
2. **Run Script** → `.\deploy-pipeline.ps1 deploy`
3. **Package & Upload** → Source code to S3
4. **Tests Run** → Must pass for deployment to continue
5. **Build & Publish** → Lambda packages
6. **CDK Deploy** → Live on AWS
7. **Done** → API immediately available

### Failure Scenarios
- **Test Failure** → Pipeline stops, NO deployment
- **Build Failure** → Pipeline stops, NO deployment  
- **Deploy Failure** → CloudFormation rollback

## 🛠️ Manual Operations

### Deploy via Pipeline (Recommended)
```powershell
cd CI-CD/Pipeline
.\deploy-pipeline.ps1 deploy
```

### Deploy Directly (Bypass Pipeline)
```bash
cd Infrastructure
dotnet publish ../LambdaHandlers/LambdaHandlers.csproj -c Release -o ../LambdaHandlers/bin/Release/net8.0/publish
cdk deploy VaskeMediaProcessor-App --require-approval never
```

### Destroy via Pipeline
```powershell
cd CI-CD/Pipeline
.\deploy-pipeline.ps1 destroy
```

### Emergency Destroy (Direct)
```bash
cd Infrastructure  
cdk destroy VaskeMediaProcessor-App --force
```

### Check Pipeline Status
```powershell
cd CI-CD/Pipeline
.\deploy-pipeline.ps1 status
```

## 📁 Buildspec Files Explained

### `buildspec.yml` - Main Build
- **Svrha**: Test + Build + Package
- **Key**: `on-failure: ABORT` za testove
- **Artifacts**: Sve potrebno za deployment

### `buildspec.deploy.yml` - Deployment  
- **Svrha**: CDK deployment
- **Key**: `cdk diff` preview + `cdk deploy`
- **Safety**: --require-approval never za automation

### `buildspec.destroy.yml` - Cleanup
- **Svrha**: Complete infrastructure removal
- **Key**: 10s delay + `cdk destroy --force` 
- **Safety**: Manual trigger only

## 🔍 Troubleshooting

### Pipeline Fails na Testovima
```bash
# Pokreni testove lokalno:
dotnet test LambdaHandlers.Tests/ --verbosity normal
```

### Pipeline Fails na Deploy-u
```bash
# Proveri CDK diff:
cd Infrastructure
cdk diff VaskeMediaProcessor-App
```

### Lambda Update Issues
```bash
# Force Lambda republish:
dotnet publish ../LambdaHandlers/LambdaHandlers.csproj -c Release -o ../LambdaHandlers/bin/Release/net8.0/publish --force
```

## 💰 Cost Optimization

### CodeBuild Pricing
- **Test & Build**: `ComputeType.SMALL` (~$0.005/min)
- **Deploy**: `ComputeType.MEDIUM` (~$0.01/min) 
- **Destroy**: `ComputeType.SMALL` (~$0.005/min)

### Storage Costs
- **Artifact Bucket**: S3 Standard, auto-delete enabled
- **CloudWatch Logs**: 1 week retention

## 🎯 Success Metrics

### Pipeline Health
- ✅ Automated testing on every commit
- ✅ Zero-downtime deployments  
- ✅ Consistent environment provisioning
- ✅ Audit trail via CloudWatch
- ✅ Infrastructure as Code (CDK)

### Quality Gates
- ✅ Unit tests must pass
- ✅ Build must succeed  
- ✅ Infrastructure validation via CDK
- ✅ Automated rollback on failure

---

**🚀 Result**: Push to master → Automatic high-quality deployment sa kompletnim testiranjem!