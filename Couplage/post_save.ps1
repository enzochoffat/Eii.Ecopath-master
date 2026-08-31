param(
    [Parameter(Mandatory = $true)]
    [string]$InputFile,
    [Parameter(Mandatory = $true)]
    [int]$TimeStep,
    [Parameter(Mandatory = $true)]
    $runTime,
    [Parameter(Mandatory = $true)]
    [int]$FirstYear,
    [Parameter(Mandatory = $true)]
    [string]$WestLon,
    [Parameter(Mandatory = $true)]
    [string]$NorthLat,
    [Parameter(Mandatory = $true)]
    [string]$CellSize,
    [Parameter(Mandatory = $true)]
    [string]$LogFile
)

New-Item -Path (Split-Path $LogFile) -ItemType Directory -Force | Out-Null

function Run-Python([string]$Script, [string]$Arg) {
    try {
        $out = & python $Script $Arg 2>&1 | ForEach-Object { $_.ToString() } | Out-String
        $code = $LASTEXITCODE
    } catch {
        $out = "EXCEPTION: " + $_.Exception.Message
        $code = 1
    }
    $out | Out-File -FilePath $LogFile -Append -Encoding utf8
    if ($code -ne 0) {
        ("ERROR: " + $Script + " exited with code " + $code) | Out-File -FilePath $LogFile -Append -Encoding utf8
        exit $code
    }
}

$scriptDir = $PSScriptRoot
New-Item -Path (Split-Path $scriptDir) -Name "Biomass" -ItemType Directory -Force
$filePath = Join-Path $scriptDir $InputFile

$parentDir = Split-Path (Split-Path $scriptDir -Parent) -Parent
$pythonScript = Join-Path $parentDir "FIBE.py"
Run-Python $pythonScript $InputFile
$parentDir = Split-Path (Split-Path $scriptDir -Parent ) -Parent 
$pythonScript = Join-Path $parentDir "Convert_static_map.py"
$InputFile = Join-Path (Split-Path $scriptDir -Parent) "Depth"
Run-Python $pythonScript $InputFile

$InputFile = Join-Path (Split-Path $scriptDir -Parent) "Ports"
Run-Python $pythonScript $InputFile

$InputFile = Join-Path (Split-Path $scriptDir -Parent) "Habitats"
Run-Python $pythonScript $InputFile

$pythonScript = Join-Path $parentDir "Convert_off_vessel_price.py"
$InputFile = Join-Path (Split-Path $scriptDir -Parent) "OffVesselPrice"
Run-Python $pythonScript $InputFile

$pythonScript = Join-Path $parentDir "Convert_landings.py"
$InputFile = Join-Path (Split-Path $scriptDir -Parent) "Landings"
Run-Python $pythonScript $InputFile

try {
    $out = & ".\..\..\CreateJSON.ps1" $TimeStep $runTime $FirstYear 3.025443 43.56926 0.02159576 2>&1 | Out-String
} catch {
    $out = "EXCEPTION: " + $_.Exception.Message
}
$out | Out-File -FilePath $LogFile -Append -Encoding utf8
