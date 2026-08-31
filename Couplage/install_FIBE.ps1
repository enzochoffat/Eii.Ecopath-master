$scriptDir = $PSScriptRoot
$fibePath = Join-Path -Path $scriptDir -ChildPath "FIBE\diatome"
$FibeParent = Join-Path -Path $scriptDir -ChildPath "FIBE"

$RepoUrl = "https://github.com/enzochoffat/diatome.git"

if (-Not (Test-Path $fibePath)) {
    Write-Host "FIBE directory does not exist. Cloning the repository..." -ForegroundColor Cyan
    New-Item -Path $FibeParent -ItemType Directory -Force | Out-Null
    git clone $RepoUrl $FibeParent

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to clone the repository."
        exit 1
    }
} else {
    Write-Host "FIBE directory exists. Checking for updates..." -ForegroundColor Cyan
    Push-Location $fibePath

    if (-Not (Test-Path ".git")) {
        Write-Host "Error: The directory exists but is not a valid Git repository."
        Pop-Location
        exit 1
    }
    Pop-Location
    git fetch origin
    Write-Host "Fetching updates from the repository..." -ForegroundColor Cyan

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error: Failed to fetch updates from the repository."
        Pop-Location
        exit 1
    }

    $LocalHash = git rev-parse HEAD
    $RemoteHash = git rev-parse origin/main

    if ($LocalHash -eq $RemoteHash) {
        Write-Host "The repository is already up to date."
    } else {
        git pull origin main
        Write-Host "Pulling updates from the repository..." -ForegroundColor Cyan

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Error: Failed to pull updates from the repository."
            Pop-Location
            exit 1
        }
        Write-Host "The repository has been updated successfully." -ForegroundColor Green
        
    }
    Pop-Location
}

Write-Host "FIBE setup completed successfully." -ForegroundColor Green