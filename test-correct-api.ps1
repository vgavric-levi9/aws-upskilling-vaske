# Test correct API URL
$correctApiUrl = "https://ilvyxchttb.execute-api.eu-north-1.amazonaws.com/prod"

Write-Host "🎥 Testing Serverless Media Processor with CORRECT URL" -ForegroundColor Cyan
Write-Host "Upload URL: $correctApiUrl/api/upload" -ForegroundColor Green

# Test image as base64 (1x1 pixel PNG)
$testImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=="

try {
    $headers = @{
        'Content-Type' = 'image/png'
        'X-Filename' = 'test-demo.png'
    }
    
    Write-Host "Uploading test image..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri "$correctApiUrl/api/upload" -Method POST -Body $testImageBase64 -Headers $headers
    
    Write-Host "✅ SUCCESS! Upload worked!" -ForegroundColor Green
    Write-Host "JobId: $($response.JobId)" -ForegroundColor White
    Write-Host "Status: $($response.Status)" -ForegroundColor White
    Write-Host "Message: $($response.Message)" -ForegroundColor White
    
    Write-Host ""
    Write-Host "Now test status endpoint:" -ForegroundColor Yellow
    Write-Host "curl https://ilvyxchttb.execute-api.eu-north-1.amazonaws.com/prod/api/status/$($response.JobId)" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response)" -ForegroundColor Red
}