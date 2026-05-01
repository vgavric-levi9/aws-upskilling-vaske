# Vaske Media Processor - IAM Permissions Documentation

Ovaj dokument detaljno opisuje sve IAM permisije u sistemu, organizovane po fajlovima i resursima. Sve permisije su minimalne potrebne za funkcionalnost.

## 📁 Organizacija Permisija po Fajlovima

### 1. `Infrastructure/Permissions/S3Permissions.cs`

**Svrha**: Definiše minimalne S3 permisije za Lambda funkcije

#### `CreateLambdaS3Policy(inputBucketArn, outputBucketArn)`
```csharp
Actions:
  - s3:GetObject          // Čitanje objekata iz input bucket-a
  - s3:PutObject          // Pisanje objekata u input/output bucket-e  
  - s3:DeleteObject       // Brisanje objekata (cleanup)
  - s3:GetObjectVersion   // Verzioniranje objekata

Resources:
  - {inputBucketArn}/*    // Svi objekti u input bucket-u
  - {outputBucketArn}/*   // Svi objekti u output bucket-u
```

#### `CreatePresignedUrlPolicy(outputBucketArn)`
```csharp
Actions:
  - s3:GetObject          // Potrebno za generisanje presigned URL-a
  - s3:GetObjectVersion   // Verzioniranje za presigned URL

Resources:
  - {outputBucketArn}/*   // Samo output bucket objekti
```

### 2. `Infrastructure/Permissions/DynamoDbPermissions.cs`

**Svrha**: Definiše minimalne DynamoDB permisije po Lambda funkciji

#### `CreateUploadLambdaPolicy(tableArn)` - for ImageUpload
```csharp
Actions:
  - dynamodb:PutItem      // Create new job
  - dynamodb:UpdateItem   // Required for DynamoDB SaveAsync operation
  - dynamodb:DescribeTable // Required for DynamoDB Context

Resources: 
  - {tableArn}            // Only VaskeMediaProcessingJobs table
```

#### `CreateProcessorLambdaPolicy(tableArn)` - za MediaProcessor
```csharp
Actions:
  - dynamodb:GetItem      // Čitanje postojećeg job-a
  - dynamodb:UpdateItem   // Ažuriranje job status-a
  - dynamodb:PutItem      // Fallback za SaveAsync operacije
  - dynamodb:DescribeTable // Potrebno za DynamoDB Context

Resources:
  - {tableArn}            // Samo VaskeMediaProcessingJobs tabela
```

#### `CreateStatusQueryLambdaPolicy(tableArn)` - za StatusQuery
```csharp
Actions:
  - dynamodb:GetItem      // Čitanje job status-a
  - dynamodb:DescribeTable // Potrebno za DynamoDB Context

Resources:
  - {tableArn}            // Samo VaskeMediaProcessingJobs tabela
```

### 3. `Infrastructure/Permissions/LambdaRoles.cs`

**Svrha**: Kreira specijalizovane IAM role sa minimalnim permisijama

#### `CreateImageUploadRole()` - VaskeMediaProcessor-ImageUploadRole
```csharp
ManagedPolicies:
  - AWSLambdaBasicExecutionRole // CloudWatch Logs pristup

InlinePolicies:
  - S3: PutObject samo za input bucket
  - DynamoDB: PutItem i DescribeTable samo za jobs tabelu
```

#### `CreateMediaProcessorRole()` - VaskeMediaProcessor-ProcessorRole
```csharp
ManagedPolicies:
  - AWSLambdaBasicExecutionRole // CloudWatch Logs pristup

InlinePolicies:
  - S3: GetObject/PutObject/DeleteObject za input/output bucket-e
  - DynamoDB: GetItem/UpdateItem/PutItem/DescribeTable za jobs tabelu
```

#### `CreateStatusQueryRole()` - VaskeMediaProcessor-StatusQueryRole
```csharp
ManagedPolicies:
  - AWSLambdaBasicExecutionRole // CloudWatch Logs pristup

InlinePolicies:
  - S3: GetObject samo za output bucket (presigned URLs)
  - DynamoDB: GetItem i DescribeTable samo za jobs tabelu
```

## 🔐 Resurs-Level Permisije

### Lambda Funkcije

| Lambda | Role | S3 Permisije | DynamoDB Permisije |
|--------|------|-------------|-------------------|
| **VaskeImageUploadHandler** | VaskeMediaProcessor-ImageUploadRole | PutObject → input bucket | PutItem, UpdateItem, DescribeTable |
| **VaskeMediaProcessorHandler** | VaskeMediaProcessor-ProcessorRole | Get/Put/Delete → input+output | Get/Update/Put/DescribeTable |
| **VaskeStatusQueryHandler** | VaskeMediaProcessor-StatusQueryRole | GetObject → output only | GetItem, DescribeTable |

### S3 Buckets

| Bucket | Naziv | Permisije |
|--------|-------|-----------|
| **Input** | vaske-media-processor-input-{account} | Upload Lambda: PutObject<br/>Processor Lambda: GetObject/DeleteObject |
| **Output** | vaske-media-processor-output-{account} | Processor Lambda: PutObject<br/>Status Lambda: GetObject (presigned) |

### DynamoDB

| Tabela | Naziv | Partition Key | Permisije po Lambda |
|--------|-------|---------------|-------------------|
| **Processing Jobs** | VaskeMediaProcessingJobs | JobId (String) | Upload: PutItem/UpdateItem<br/>Processor: Get/Update/Put<br/>Status: GetItem |

### API Gateway

| Endpoint | Lambda | HTTP Method | CORS |
|----------|--------|-------------|------|
| `/api/upload` | VaskeImageUploadHandler | POST | Enabled |
| `/api/status/{jobId}` | VaskeStatusQueryHandler | GET | Enabled |

## 🚀 CI/CD Pipeline Permisije

### CodeBuild Projects

#### VaskeMediaProcessor-TestAndBuild
```csharp
Permisije: Nema AWS API pozive - samo build i test
```

#### VaskeMediaProcessor-Deploy
```csharp
ManagedPolicy: AdministratorAccess 
Razlog: CDK deployment potreban pun pristup za kreiranje resursa
```

#### VaskeMediaProcessor-Destroy
```csharp
ManagedPolicy: AdministratorAccess
Razlog: CDK destroy potreban pun pristup za brisanje resursa
```

### CodePipeline

#### VaskeMediaProcessor-CICD
```csharp
S3: ArtifactBucket pristup
CodeCommit: Polling za source kod
CodeBuild: Pokretanje build projekata
```

## ⚠️ Sigurnosni Principi

### 1. Principle of Least Privilege
- Svaka Lambda ima samo permisije potrebne za svoju funkciju
- Nema cross-service pristupa gde nije potreban
- DynamoDB pristup ograničen na specifičnu tabelu

### 2. Resource-Level Restrictions
- S3 permisije su ograničene na specifične bucket-e
- DynamoDB permisije su ograničene na specifičnu tabelu
- Nema wildcard (*) pristupa

### 3. Action-Level Restrictions
- Samo potrebne DynamoDB akcije (GetItem vs Scan)
- Specifični S3 akcije po use case-u
- Nema Admin permisija za Lambda-e

### 4. Role Separation
- Svaka Lambda ima svoju dedikovanú IAM role
- Nema deljenih "super-role" između funkcija
- CI/CD ima posebne role odvojene od aplikacije

## 🔍 Kako Proveriti Permisije

### AWS Console
1. IAM → Roles → Pronađi role sa "Vaske" prefiksom
2. CloudFormation → Stack → Resources → Pogledaj IAM roles
3. Lambda → Function → Configuration → Permissions

### AWS CLI
```bash
# Lista svih Vaske resursa
aws iam list-roles --query "Roles[?contains(RoleName, 'Vaske')]"

# Detalji specifične role
aws iam get-role --role-name VaskeMediaProcessor-ImageUploadRole

# Lista policy dokumenata
aws iam list-attached-role-policies --role-name VaskeMediaProcessor-ImageUploadRole
```

### CDK CLI
```bash
cd Infrastructure
cdk diff VaskeMediaProcessor-App  # Prikaži permisije pre deploy-a
```

---

**Napomena**: Svi AWS resursi sadrže "vaske" u nazivu za lako prepoznavanje i organizaciju.