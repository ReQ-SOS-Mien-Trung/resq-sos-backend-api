# =============================================================
# Script để build và push Docker image lên Docker Hub
# Chạy: .\scripts\build-and-push.ps1 -DockerUsername "your-username" -Tag "latest"
# =============================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$DockerUsername,
    
    [Parameter(Mandatory=$false)]
    [string]$Tag = "latest",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"

$ImageName = "$DockerUsername/resq-backend"
$FullImageName = "${ImageName}:${Tag}"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building RESQ Backend Docker Image" -ForegroundColor Cyan
Write-Host "Image: $FullImageName" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Navigate to project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Set-Location $ProjectRoot

Write-Host "`n[1/3] Building Docker image..." -ForegroundColor Yellow
docker build -t $FullImageName -f RESQ.Presentation/Dockerfile .

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`n[2/3] Tagging as latest..." -ForegroundColor Yellow
docker tag $FullImageName "${ImageName}:latest"

if (-not $SkipPush) {
    Write-Host "`n[3/3] Pushing to Docker Hub..." -ForegroundColor Yellow
    Write-Host "Make sure you're logged in: docker login" -ForegroundColor Gray
    
    docker push $FullImageName
    docker push "${ImageName}:latest"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Push failed! Make sure you're logged in with: docker login" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "SUCCESS! Image pushed to Docker Hub" -ForegroundColor Green
    Write-Host "Image: $FullImageName" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    
    Write-Host "`nFrontend team can now use:" -ForegroundColor Cyan
    Write-Host "  docker pull $FullImageName" -ForegroundColor White
} else {
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "SUCCESS! Image built locally" -ForegroundColor Green
    Write-Host "Image: $FullImageName" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}
