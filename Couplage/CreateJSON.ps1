param(
    [Parameter(Mandatory = $true)]
    [int]$TimeStep
)

# --- 1. Initialisation et Validation ---
$scriptDir = $PSScriptRoot
$jsonPath = Join-Path $scriptDir "FIBE\diatome\configs\config_default.json"
$finalJsonPath = Join-Path $scriptDir "FIBE\diatome\configs\config.json"
$tempJsonPath = "$finalJsonPath.tmp"

if (-Not (Test-Path $jsonPath)) {
    Write-Error "Fichier JSON de base introuvable : $jsonPath"
    exit 1
}

# Chargement unique de la configuration
$config = Get-Content -Path $jsonPath -Raw | ConvertFrom-Json

# Initialisation unique du conteneur 'maps'
if (-not $config.PSObject.Properties.Match('maps')) {
    $config | Add-Member -Name "maps" -Value ([PSCustomObject]@{}) -MemberType NoteProperty
}
$maps = $config.maps

# Application du TimeStep
$config.simulation | Add-Member -Name "step" -Value $TimeStep -MemberType NoteProperty -Force

# --- 2. Fonctions Helper pour la réduction de code ---

function Add-FileMap {
    param(
        [string]$Path,
        [string]$PropertyName,
        [scriptBlock]$KeyExtractor
    )

    if (Test-Path $Path) {
        Write-Host "Traitement : $Path"
        $fileMap = @{}
        
        # Optimisation : Filtrage direct dans Get-ChildItem
        $files = Get-ChildItem -Path $Path -Filter "*.csv" -File -ErrorAction SilentlyContinue
        
        foreach ($file in $files) {
            $key = & $KeyExtractor $file
            if ($key) {
                $fileMap[$key] = $file.FullName
            }
        }
        
        $maps | Add-Member -Name $PropertyName -Value $fileMap -MemberType NoteProperty -Force
    } else {
        Write-Warning "Chemin introuvable : $Path"
    }
}

# --- 3. Exécution des Mappings ---

# Biomass
Add-FileMap -Path (Join-Path $scriptDir "Data\Biomass") -PropertyName "species_map" -KeyExtractor {
    param($f)
    $name = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
    if ($name -match "map_(.*)") { return $matches[1] }
    return $name
}

# Depth (Fichier unique)
$depthPath = Join-Path $scriptDir "Data\Depth\DepthMap.csv"
if (Test-Path $depthPath) {
    $maps | Add-Member -Name "spatial_map" -Value $depthPath -MemberType NoteProperty -Force
} else {
    Write-Warning "Fichier de profondeur introuvable : $depthPath"
}

# Ports (Fichier unique)
$portsPath = Join-Path $scriptDir "Data\Ports\PortsMap.csv"
if (Test-Path $portsPath) {
    $maps | Add-Member -Name "ports_map" -Value $portsPath -MemberType NoteProperty -Force
} else {
    Write-Warning "Fichier de ports introuvable : $portsPath"
}

# Habitats
Add-FileMap -Path (Join-Path $scriptDir "Data\Habitats") -PropertyName "habitat_map" -KeyExtractor {
    param($f)
    $name = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
    if ($name -match "^[^_]+_\d+_(.*)") { return $matches[1] }
    return $name
}

# --- 4. Écriture Atomique ---
try {
    # Conversion unique en JSON
    $jsonOutput = $config | ConvertTo-Json -Depth 10
    
    # Écriture temporaire
    $jsonOutput | Set-Content -Path $tempJsonPath -NoNewline
    
    # Remplacement atomique
    Move-Item -Path $tempJsonPath -Destination $finalJsonPath -Force
    
    Write-Host "Configuration mise à jour avec succès : $finalJsonPath"
}
catch {
    Write-Error "Échec de l'écriture : $_"
    if (Test-Path $tempJsonPath) { Remove-Item $tempJsonPath -Force }
    exit 1
}