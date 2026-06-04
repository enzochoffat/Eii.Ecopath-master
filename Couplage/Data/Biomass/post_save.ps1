param(
    [Parameter(Mandatory = $true)]
    [string]$InputFile
)

$scriptDir = $PSScriptRoot
New-Item -Path (Split-Path $scriptDir) -Name "Biomass" -ItemType Directory -Force
$filePath = Join-Path $scriptDir $InputFile
Write-Host "Post save script running for file: $InputFile"

$parentDir = Split-Path (Split-Path $scriptDir -Parent) -Parent
$pythonScript = Join-Path $parentDir "FIBE.py"
Write-Host "Post save script running for file: $pythonScript"
python $pythonScript $InputFile
$parentDir = Split-Path (Split-Path $scriptDir -Parent ) -Parent 
$pythonScript = Join-Path $parentDir "Convert_static_map.py"
Write-Host "Script processed: $pythonScript"
$InputFile = Join-Path (Split-Path $scriptDir -Parent) "Depth"
Write-Host "File processed: $InputFile"
python $pythonScript $InputFile

$InputFile = Join-Path (Split-Path $scriptDir -Parent) "Ports"
python $pythonScript $InputFile

$InputFile = Join-Path (Split-Path $scriptDir -Parent) "Habitats"
python $pythonScript $InputFile

Write-Host "File processed: $InputFile"
dir
.\..\..\CreateJSON.ps1
cd ..\..\FIBE\diatome
.\venv\Scripts\Activate.ps1
python .\scripts\run_simulation.py .\configs\config.json
