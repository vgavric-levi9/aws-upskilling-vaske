# Vaske Media Processor - Manual Pipeline Deployment Script
# This script packages your source code and triggers the CI/CD pipeline

param(
    [Parameter(Mandatory=$false)]
    [string]$Action = "deploy"  # deploy, destroy, status
)

$ErrorActionPreference = "Stop"

# Configuration  
$SOURCE_DIR = "../../"  # Root of aws-upskilling-vaske project
$TEMP_DIR = "temp-source"
$ZIP_FILE = "source-code.zip"
$S3_KEY = "source/source-code.zip"

Write-Host "Vaske Media Processor Pipeline Control" -ForegroundColor Cyan
Write-Host "Action: $Action" -ForegroundColor Yellow

# Function to get pipeline bucket name
function Get-PipelineBucket {
    try {
        $stackOutput = aws cloudformation describe-stacks --stack-name "VaskeMediaProcessor-Pipeline" --query "Stacks[0].Outputs[?OutputKey=='VaskeArtifactsBucket'].OutputValue" --output text 2>$null
        if ($LASTEXITCODE -eq 0 -and $stackOutput) {
            return $stackOutput.Trim()
        }
    }
    catch {
        Write-Warning "Could not get bucket name from CloudFormation. Pipeline may not be deployed yet."
    }
    return $null
}

# Function to package source code
function Package-Source {
    Write-Host "Packaging source code..." -ForegroundColor Green
    
    # Get absolute paths
    $currentDir = Get-Location
    $sourceDir = Resolve-Path $SOURCE_DIR
    
    Write-Host "Current directory: $currentDir" -ForegroundColor Yellow
    Write-Host "Source directory: $sourceDir" -ForegroundColor Yellow
    
    # Verify source directories exist
    $requiredDirs = @("LambdaHandlers", "Infrastructure", "LambdaHandlers.Tests")
    foreach ($dir in $requiredDirs) {
        $fullPath = Join-Path $sourceDir $dir
        if (-not (Test-Path $fullPath)) {
            throw "Required directory not found: $fullPath"
        }
        Write-Host "Found: $fullPath" -ForegroundColor Green
    }
    
    # Clean temp directory
    if (Test-Path $TEMP_DIR) {
        Remove-Item -Recurse -Force $TEMP_DIR
    }
    New-Item -ItemType Directory -Path $TEMP_DIR | Out-Null
    
    # Copy source files (exclude unnecessary files)
    Write-Host "Copying source files..." -ForegroundColor Green
    
    # Copy main project directories
    Copy-Item -Path (Join-Path $sourceDir "LambdaHandlers") -Destination "$TEMP_DIR/LambdaHandlers" -Recurse -Force
    Copy-Item -Path (Join-Path $sourceDir "Infrastructure") -Destination "$TEMP_DIR/Infrastructure" -Recurse -Force
    Copy-Item -Path (Join-Path $sourceDir "LambdaHandlers.Tests") -Destination "$TEMP_DIR/LambdaHandlers.Tests" -Recurse -Force
    
    # Copy additional files
    Get-ChildItem -Path $sourceDir -Filter "*.md" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $TEMP_DIR -Force
    }
    Get-ChildItem -Path $sourceDir -Filter "test-*.ps1" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $TEMP_DIR -Force
    }
    
    # Clean unnecessary files
    $excludePatterns = @("bin", "obj", "*.user", "*.suo", ".vs", "node_modules", "cdk.out")
    Get-ChildItem -Path $TEMP_DIR -Recurse | Where-Object {
        $excludePatterns -contains $_.Name -or $_.Name -like "*.user" -or $_.Name -like "*.suo"
    } | Remove-Item -Recurse -Force
    
    # Show what we're about to package
    Write-Host "Package contents:" -ForegroundColor Yellow
    Get-ChildItem -Path $TEMP_DIR | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Cyan }
    
    # Verify critical files exist
    $criticalFiles = @(
        "LambdaHandlers/LambdaHandlers.csproj",
        "Infrastructure/Infrastructure.csproj", 
        "LambdaHandlers.Tests/LambdaHandlers.Tests.csproj"
    )
    foreach ($file in $criticalFiles) {
        $fullPath = Join-Path $TEMP_DIR $file
        if (Test-Path $fullPath) {
            Write-Host "Verified: $file" -ForegroundColor Green
        } else {
            throw "Critical file missing: $file"
        }
    }
    
    # Create zip file
    if (Test-Path $ZIP_FILE) {
        Remove-Item $ZIP_FILE -Force
    }
    
    Compress-Archive -Path "$TEMP_DIR/*" -DestinationPath $ZIP_FILE -CompressionLevel Optimal
    
    # Clean temp directory
    Remove-Item -Recurse -Force $TEMP_DIR
    
    $fileSize = [math]::Round((Get-Item $ZIP_FILE).Length / 1MB, 2)
    Write-Host "Source packaged: $ZIP_FILE ($fileSize MB)" -ForegroundColor Green
}

# Function to upload to S3 and trigger pipeline
function Trigger-Pipeline {
    param([string]$BucketName, [string]$PipelineName)
    
    Write-Host "Uploading to S3: s3://$BucketName/$S3_KEY" -ForegroundColor Green
    aws s3 cp $ZIP_FILE "s3://$BucketName/$S3_KEY"
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to upload source to S3"
    }
    
    Write-Host "Starting pipeline: $PipelineName" -ForegroundColor Green
    aws codepipeline start-pipeline-execution --name $PipelineName | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Pipeline started successfully!" -ForegroundColor Green
        Write-Host "View pipeline: https://eu-north-1.console.aws.amazon.com/codesuite/codepipeline/pipelines/$PipelineName/view" -ForegroundColor Cyan
    } else {
        throw "Failed to start pipeline"
    }
}

# Function to show pipeline status
function Show-PipelineStatus {
    param([string]$PipelineName)
    
    Write-Host "Pipeline Status: $PipelineName" -ForegroundColor Green
    aws codepipeline get-pipeline-state --name $PipelineName --query "stageStates[*].{Stage:stageName,Status:latestExecution.status}" --output table
}

# Main execution
try {
    switch ($Action.ToLower()) {
        "deploy" {
            Write-Host "Deploying Vaske Media Processor via Pipeline" -ForegroundColor Cyan
            
            $bucketName = Get-PipelineBucket
            if (-not $bucketName) {
                Write-Host "Pipeline not deployed. Deploy it first:" -ForegroundColor Red
                Write-Host "   cd CI-CD/Pipeline" -ForegroundColor Yellow
                Write-Host "   cdk deploy VaskeMediaProcessor-Pipeline --require-approval never" -ForegroundColor Yellow
                exit 1
            }
            
            Package-Source
            Trigger-Pipeline -BucketName $bucketName -PipelineName "VaskeMediaProcessor-CICD"
        }
        
        "destroy" {
            Write-Host "Destroying Vaske Media Processor via Pipeline" -ForegroundColor Red
            
            $bucketName = Get-PipelineBucket
            if (-not $bucketName) {
                Write-Host "Pipeline not deployed." -ForegroundColor Red
                exit 1
            }
            
            Package-Source
            # Upload and trigger destroy pipeline
            aws s3 cp $ZIP_FILE "s3://$bucketName/$S3_KEY"
            aws codepipeline start-pipeline-execution --name "VaskeMediaProcessor-Destroy" | Out-Null
            
            Write-Host "Destroy pipeline started! This will DELETE all resources." -ForegroundColor Red
            Write-Host "View pipeline: https://eu-north-1.console.aws.amazon.com/codesuite/codepipeline/pipelines/VaskeMediaProcessor-Destroy/view" -ForegroundColor Cyan
        }
        
        "status" {
            Show-PipelineStatus -PipelineName "VaskeMediaProcessor-CICD"
            Write-Host ""
            Show-PipelineStatus -PipelineName "VaskeMediaProcessor-Destroy"
        }
        
        default {
            Write-Host "Invalid action. Use: deploy, destroy, or status" -ForegroundColor Red
            exit 1
        }
    }
    
    # Clean up zip file
    if (Test-Path $ZIP_FILE) {
        Remove-Item $ZIP_FILE -Force
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}