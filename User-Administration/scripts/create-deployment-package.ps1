# Generate deployment package
# Creates a ZIP package for deployment

$packageDir = "deployment_package_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
$excludePatterns = @(
    "*.log",
    "*.tmp",
    ".git",
    ".gitignore",
    ".dockerignore",
    "node_modules",
    "dist",
    ".cache",
    ".vscode",
    ".idea",
    "*.zip",
    "backup_*",
    "logs",
    "temp"
)

Write-Host "Creating deployment package..." -ForegroundColor Yellow

# Create package directory
New-Item -ItemType Directory -Path $packageDir | Out-Null

# Copy essential files and directories
$sourceItems = @(
    "db/init_db.sql",
    "backend/src/Nexorsys.Identity.Core",
    "backend/src/Nexorsys.Identity.Infrastructure",
    "backend/src/Nexorsys.Identity.API",
    "backend/Nexorsys.Identity.Solution.sln",
    "backend/.gitignore",
    "frontend/nexorsys-identity-ui/src",
    "frontend/nexorsys-identity-ui/public",
    "frontend/nexorsys-identity-ui/package.json",
    "frontend/nexorsys-identity-ui/tailwind.config.js",
    "frontend/nexorsys-identity-ui/tsconfig.json",
    "frontend/nexorsys-identity-ui/vite.config.ts",
    "frontend/nexorsys-identity-ui/.gitignore",
    "AGENTS.md",
    "README.md",
    ".env.example",
    "Dockerfile.backend",
    "Dockerfile.frontend",
    "nginx.conf",
    "docker-compose.yml"
)

foreach ($item in $sourceItems)
{
    if (Test-Path $item)
    {
        Copy-Item -Path $item -Destination $packageDir -Recurse -Force
        Write-Host "Copied: $item" -ForegroundColor Green
    }
    else
    {
        Write-Host "Not found: $item" -ForegroundColor Yellow
    }
}

# Create ZIP archive
$zipFile = "nexorsys-identity-deployment.zip"
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($packageDir, $zipFile)

# Clean up temporary package directory
Remove-Item -Recurse -Force $packageDir

Write-Host "Deployment package created successfully!" -ForegroundColor Green
Write-Host "File: $zipFile" -ForegroundColor Yellow