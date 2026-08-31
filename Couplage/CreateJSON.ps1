param(
    [Parameter(Mandatory = $true)]
    [int]$TimeStep,
    [Parameter(Mandatory = $true)]
    [int]$runTime,
    [Parameter(Mandatory = $true)]
    [int]$FirstYear,
    [Parameter(Mandatory = $false)]
    [float]$WestLon,
    [Parameter(Mandatory = $false)]
    [float]$NorthLat,
    [Parameter(Mandatory = $false)]
    [float]$CellSize
)

# --- 1. Initialisation et Validation ---
$scriptDir = $PSScriptRoot
$jsonPath = Join-Path $scriptDir "FIBE\diatome\configs_json\config_default.json"
$finalJsonPath = Join-Path $scriptDir "FIBE\diatome\configs_json\config.json"
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
$config.simulation | Add-Member -Name "coupling" -Value $true -MemberType NoteProperty -Force
$config.simulation | Add-Member -Name "duration_years" -Value $runTime -MemberType NoteProperty -Force
if ($FirstYear -le 0) { $FirstYear = 2000 }
$config.simulation | Add-Member -Name "start_date" -Value "$FirstYear-01-01" -MemberType NoteProperty -Force

# Application des paramètres de la carte si fournis
if ($PSBoundParameters.ContainsKey('WestLon') -and 
    $PSBoundParameters.ContainsKey('NorthLat') -and 
    $PSBoundParameters.ContainsKey('CellSize')) {
    if (-not $config.maps.PSObject.Properties.Match('spatial_extent')) {
        $config.maps | Add-Member -Name "spatial_extent" -Value ([PSCustomObject]@{}) -MemberType NoteProperty
    }
    $config.maps.spatial_extent | Add-Member -Name "west" -Value $WestLon -MemberType NoteProperty -Force
    $config.maps.spatial_extent | Add-Member -Name "north" -Value $NorthLat -MemberType NoteProperty -Force
    $config.maps.spatial_extent | Add-Member -Name "cell_size_deg" -Value $CellSize -MemberType NoteProperty -Force
}
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

# OffVesselPrice (Fichier unique)
$offVesselPricePath = Join-Path $scriptDir "Data\OffVesselPrice\EcospaceOffVesselPrice.csv"
if (Test-Path $offVesselPricePath) {
    $config.maps.species_tables | Add-Member -Name "price" -Value $offVesselPricePath -MemberType NoteProperty -Force
} else {
    Write-Warning "Fichier off vessel price introuvable : $offVesselPricePath"
}

# Landings (Fichier unique)
$landingsPath = Join-Path $scriptDir "Data\Landings\EcospaceLandings.csv"
if (Test-Path $landingsPath) {
    $config.maps.species_tables | Add-Member -Name "catchability" -Value $landingsPath -MemberType NoteProperty -Force
} else {
    Write-Warning "Fichier landings introuvable : $landingsPath"
}

# Restricted areas (zones restreintes), exportées par EwE dans restricted_zones.json
$restrictedZonesPath = Join-Path $scriptDir "Data\restricted_zones.json"
if (Test-Path $restrictedZonesPath) {
    $restrictedZones = Get-Content -Path $restrictedZonesPath -Raw | ConvertFrom-Json
    if ($restrictedZones.PSObject.Properties.Match('restricted_area_map')) {
        $config.maps | Add-Member -Name "restricted_area_map" -Value $restrictedZones.restricted_area_map -MemberType NoteProperty -Force
        Write-Host "Zones restreintes (map) appliquées depuis restricted_zones.json"
    }
    if ($restrictedZones.PSObject.Properties.Match('restricted_area_vector')) {
        $config.maps | Add-Member -Name "restricted_area_vector" -Value $restrictedZones.restricted_area_vector -MemberType NoteProperty -Force
        Write-Host "Zones restreintes (vector) appliquées depuis restricted_zones.json"
    }
} else {
    Write-Warning "restricted_zones.json introuvable : $restrictedZonesPath"
}

# --- 4. Écriture Atomique ---
try {
    # Conversion unique en JSON
    $jsonOutput = $config | ConvertTo-Json -Depth 10
    
    # Écriture temporaire
    $jsonOutput | Set-Content -Path $tempJsonPath -NoNewline
    
    $moved = $false
    for ($attempt = 0; $attempt -lt 20 -and -not $moved; $attempt++) {
        try {
            # Remplacement atomique
            Move-Item -Path $tempJsonPath -Destination $finalJsonPath -Force -ErrorAction Stop
            $moved = $true
        } catch [System.IO.IOException] {
            Start-Sleep -Milliseconds 100
        }
    }
    if (-not $moved) { throw "config.json is locked after multiple attempts." }
    
    Write-Host "Configuration mise à jour avec succès : $finalJsonPath"
}
catch {
    Write-Error "Échec de l'écriture : $_"
    if (Test-Path $tempJsonPath) { Remove-Item $tempJsonPath -Force }
    exit 1
}