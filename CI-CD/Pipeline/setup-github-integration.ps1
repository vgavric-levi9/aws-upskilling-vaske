# Vaske Media Processor - GitHub Integration Setup Script
# This script helps set up GitHub integration for the CI/CD pipeline

param(
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken,
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubOwner,
    
    [Parameter(Mandatory=$true)]  
    [string]$GitHubRepo,
    
    [Parameter(Mandatory=$false)]
    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"

Write-Host "Setting up GitHub integration for Vaske Media Processor CI/CD Pipeline" -ForegroundColor Cyan
Write-Host "GitHub Owner: $GitHubOwner" -ForegroundColor Yellow
Write-Host "GitHub Repo: $GitHubRepo" -ForegroundColor Yellow
Write-Host "Branch: $Branch" -ForegroundColor Yellow

# Function to update PipelineStack.cs with actual GitHub values
function Update-PipelineConfiguration {
    $pipelineFile = "PipelineStack.cs"
    
    if (-not (Test-Path $pipelineFile)) {
        throw "PipelineStack.cs not found. Make sure you're in the CI-CD/Pipeline directory."
    }
    
    Write-Host "Updating pipeline configuration..." -ForegroundColor Green
    
    # Read the file content
    $content = Get-Content $pipelineFile -Raw
    
    # Update GitHub configuration
    $content = $content -replace 'private const string GITHUB_OWNER = "YOUR_GITHUB_USERNAME";', "private const string GITHUB_OWNER = `"$GitHubOwner`";"
    $content = $content -replace 'private const string GITHUB_REPO = "aws-upskilling-vaske";', "private const string GITHUB_REPO = `"$GitHubRepo`";"
    $content = $content -replace 'private const string GITHUB_BRANCH = "main";', "private const string GITHUB_BRANCH = `"$Branch`";"
    
    # Write back to file
    Set-Content -Path $pipelineFile -Value $content -Encoding UTF8
    
    Write-Host "Pipeline configuration updated successfully!" -ForegroundColor Green
}

# Function to store GitHub token in AWS Secrets Manager
function Store-GitHubToken {
    Write-Host "Storing GitHub token in AWS Secrets Manager..." -ForegroundColor Green
    
    # Check if secret already exists
    $secretExists = $false
    try {
        aws secretsmanager describe-secret --secret-id "github-token" 2>$null | Out-Null
        $secretExists = $true
        Write-Host "Secret 'github-token' already exists. Updating..." -ForegroundColor Yellow
    }
    catch {
        Write-Host "Creating new secret 'github-token'..." -ForegroundColor Green
    }
    
    if ($secretExists) {
        # Update existing secret
        aws secretsmanager update-secret --secret-id "github-token" --secret-string $GitHubToken
    } else {
        # Create new secret
        aws secretsmanager create-secret --name "github-token" --description "GitHub Personal Access Token for Vaske Media Processor CI/CD" --secret-string $GitHubToken
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GitHub token stored successfully!" -ForegroundColor Green
    } else {
        throw "Failed to store GitHub token in Secrets Manager"
    }
}

# Function to validate GitHub token
function Test-GitHubToken {
    Write-Host "Validating GitHub token..." -ForegroundColor Green
    
    $headers = @{
        "Authorization" = "token $GitHubToken"
        "User-Agent" = "VaskeMediaProcessor-Setup"
    }
    
    try {
        $response = Invoke-RestMethod -Uri "https://api.github.com/user" -Headers $headers
        Write-Host "GitHub token is valid. Authenticated as: $($response.login)" -ForegroundColor Green
        
        # Check repository access
        $repoResponse = Invoke-RestMethod -Uri "https://api.github.com/repos/$GitHubOwner/$GitHubRepo" -Headers $headers
        Write-Host "Repository access confirmed: $($repoResponse.full_name)" -ForegroundColor Green
        
        return $true
    }
    catch {
        Write-Host "GitHub token validation failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main execution
try {
    Write-Host "Step 1: Validating GitHub token..." -ForegroundColor Cyan
    if (-not (Test-GitHubToken)) {
        throw "GitHub token validation failed. Please check your token and repository access."
    }
    
    Write-Host "Step 2: Storing GitHub token in AWS Secrets Manager..." -ForegroundColor Cyan
    Store-GitHubToken
    
    Write-Host "Step 3: Updating pipeline configuration..." -ForegroundColor Cyan
    Update-PipelineConfiguration
    
    Write-Host ""
    Write-Host "GitHub integration setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Build and deploy the pipeline:" -ForegroundColor White
    Write-Host "   dotnet build" -ForegroundColor Cyan
    Write-Host "   cdk deploy VaskeMediaProcessor-Pipeline --require-approval never" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "2. Push code to GitHub to trigger automatic deployment:" -ForegroundColor White
    Write-Host "   git add ." -ForegroundColor Cyan
    Write-Host "   git commit -m 'Add CI/CD pipeline with GitHub integration'" -ForegroundColor Cyan
    Write-Host "   git push origin $Branch" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The pipeline will automatically trigger on pushes to the '$Branch' branch!" -ForegroundColor Green
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}