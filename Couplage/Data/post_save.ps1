param(
    [Parameter(Mandatory = $true)]
    [string]$InputFile
)

$scriptDir = $PSScriptRoot
$filePath = Join-Path $scriptDir $InputFile
Write-Host "Post save script running for file: $InputFile"

$pythonScript = Join-Path (Split-Path $scriptDir -Parent) "FIBE.py"
python $pythonScript $InputFile

Write-Host "File processed: $InputFile"
