$scriptDir = $PSScriptRoot
$databasePath = Join-Path $scriptDir "Biomass"
$jsonPath = Join-Path $scriptDir "FIBE\diatome\configs\config_default.json"
$newFile = Join-Path $scriptDir "FIBE\diatome\configs\config.json" 

if (-Not (Test-Path $jsonPath)) {
    Write-Host "Base JSON file not found at $jsonPath"
    exit 1
}

Copy-Item $jsonPath -Destination $newFile -Force
Write-Host "Copied base JSON to $newFile"

$configContent = Get-Content -Path $newFile -Raw -Encoding UTF8
$config = $configContent | ConvertFrom-Json

$dataFolder = $databasePath 

if (Test-Path $dataFolder) {
    Write-Host "Scan du dossier : $dataFolder"
    
    $csvFiles = Get-ChildItem -Path $dataFolder -Filter "*.csv" -File
    $speciesMap = @{}

    if ($csvFiles.Count -gt 0) {
        foreach ($file in $csvFiles) {
            $fileName = $file.Name
            $nameWithoutExt = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
            $key = $nameWithoutExt
            
            # Nettoyage du nom si préfixe "map_" présent
            if ($key -match "map_(.*)") {
                $key = $matches[1] 
            }
            
            # Construction du chemin relatif tel que dans votre exemple
            $relativePath = Join-Path $scriptDir "Biomass\$fileName"
            $speciesMap[$key] = $relativePath
        }
    }

    # Vérification et création de la structure "maps" si elle n'existe pas
    if (-not $config.PSObject.Properties.Match('maps')) {
        $config | Add-Member -Name "maps" -Value (New-Object PSObject) -MemberType NoteProperty
    }

    # Mise à jour spécifique de la propriété "species_map" sans toucher aux autres
    $config.maps | Add-Member -Name "species_map" -Value $speciesMap -MemberType NoteProperty -Force
    
    $config | ConvertTo-Json -Depth 10 | Set-Content -Path $newFile
    Write-Host "Configuration mise à jour et enregistrée dans $newFile"
} else {
    Write-Host "Le dossier de données n'existe pas : $dataFolder"
}