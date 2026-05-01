# PowerShell script to test the Serverless Media Processor API
# Usage: ./test-api.ps1 -ApiUrl "https://your-api-url.execute-api.eu-north-1.amazonaws.com/prod"

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiUrl
)

# Test image as base64 (1x1 pixel PNG)
$testImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=="

Write-Host "🎥 Serverless Media Processor API Test" -ForegroundColor Cyan
Write-Host "API URL: $ApiUrl" -ForegroundColor Green
Write-Host ""

# Step 1: Upload image
Write-Host "Step 1: Uploading test image..." -ForegroundColor Yellow
$uploadUrl = "$ApiUrl/api/upload"

try {
    $headers = @{
        'Content-Type' = 'image/png'
        'X-Filename' = 'test-demo.png'
    }
    
    $response = Invoke-RestMethod -Uri $uploadUrl -Method POST -Body $testImageBase64 -Headers $headers
    
    $jobId = $response.JobId
    Write-Host "✅ Upload successful!" -ForegroundColor Green
    Write-Host "JobId: $jobId" -ForegroundColor White
    Write-Host "Status: $($response.Status)" -ForegroundColor White
    Write-Host "Message: $($response.Message)" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host "❌ Upload failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Monitor status
Write-Host "Step 2: Monitoring processing status..." -ForegroundColor Yellow
$statusUrl = "$ApiUrl/api/status/$jobId"

$maxAttempts = 12
$attempt = 1

do {
    try {
        Start-Sleep -Seconds 5
        $statusResponse = Invoke-RestMethod -Uri $statusUrl -Method GET
        
        Write-Host "Attempt $attempt/$maxAttempts - Status: $($statusResponse.Status)" -ForegroundColor Cyan
        
        if ($statusResponse.Status -eq "completed") {
            Write-Host "✅ Processing completed successfully!" -ForegroundColor Green
            Write-Host "Original File: $($statusResponse.OriginalFileName)" -ForegroundColor White
            Write-Host "Processing Time: $([DateTime]::Parse($statusResponse.ProcessingCompletedAt) - [DateTime]::Parse($statusResponse.ProcessingStartedAt))" -ForegroundColor White
            
            if ($statusResponse.Result) {
                Write-Host "Processed Dimensions: $($statusResponse.Result.Width)x$($statusResponse.Result.Height)" -ForegroundColor White
                Write-Host "Processed Size: $($statusResponse.Result.FileSize) bytes" -ForegroundColor White
                Write-Host "Download URL: $($statusResponse.Result.OutputUrl)" -ForegroundColor White
            }
            break
        }
        elseif ($statusResponse.Status -eq "failed") {
            Write-Host "❌ Processing failed: $($statusResponse.ErrorMessage)" -ForegroundColor Red
            break
        }
        
        $attempt++
    } catch {
        Write-Host "❌ Status query failed: $($_.Exception.Message)" -ForegroundColor Red
        break
    }
    
} while ($attempt -le $maxAttempts)

if ($attempt -gt $maxAttempts) {
    Write-Host "⏰ Processing taking longer than expected. Check CloudWatch logs for details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Demo completed! 🎉" -ForegroundColor Cyan
Write-Host "Check the AWS Console to explore the created resources:" -ForegroundColor White
Write-Host "- Lambda Functions: ImageUploadHandler, MediaProcessorHandler, StatusQueryHandler" -ForegroundColor Gray
Write-Host "- S3 Buckets: Input and Output buckets" -ForegroundColor Gray
Write-Host "- DynamoDB Table: MediaProcessingJobs" -ForegroundColor Gray
Write-Host "- CloudWatch Logs: Real-time processing logs" -ForegroundColor Gray