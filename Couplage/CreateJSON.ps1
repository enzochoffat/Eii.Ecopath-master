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
$jsonPath = Join-Path $scriptDir "FIBE\configs_json\config_default.json"
$finalJsonPath = Join-Path $scriptDir "FIBE\configs_json\config.json"
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

# --- Agents FIBE (onglet "Agents (FIBE)") : appliqué à CHAQUE step ---
# Exporté par EwE dans Data/fibe_agents.json (agrégé par flottille : nombres, noms,
# ports, habitats, seuils de vague = minimum par flottille).
# $config est reconstruit depuis config_default.json à chaque step, donc les champs
# statiques (names, ports, habitats, wave) doivent être réappliqués à chaque fois.
# Le bloc "Agents temporels" ci-dessous écrase ensuite UNIQUEMENT les num_*
# (num_archipelago / num_coastal / num_trawler) quand Agents_number.csv a une donnée.
$fibeAgentsPath = Join-Path $scriptDir "Data\fibe_agents.json"
if (Test-Path $fibeAgentsPath) {
    try {
        $fibeAgents = Get-Content -Path $fibeAgentsPath -Raw | ConvertFrom-Json
        if (-not $config.PSObject.Properties.Match('agents')) {
            $config | Add-Member -Name "agents" -Value ([PSCustomObject]@{}) -MemberType NoteProperty
        }
        if ($fibeAgents.PSObject.Properties.Match('num_agents')) {
            if (-not $config.agents.PSObject.Properties.Match('num_agents')) {
                $config.agents | Add-Member -Name "num_agents" -Value ([PSCustomObject]@{}) -MemberType NoteProperty
            }
            foreach ($k in @('num_archipelago', 'num_coastal', 'num_trawler')) {
                if ($fibeAgents.num_agents.PSObject.Properties.Match($k)) {
                    $config.agents.num_agents | Add-Member -Name $k -Value ([int]$fibeAgents.num_agents.$k) -MemberType NoteProperty -Force
                    $config.agents | Add-Member -Name $k -Value ([int]$fibeAgents.num_agents.$k) -MemberType NoteProperty -Force
                }
            }
        }
        foreach ($k in @('names', 'archipelago_ports', 'coastal_ports', 'trawler_ports',
                         'archipelago_habitats', 'coastal_habitats', 'trawler_habitats')) {
            if ($fibeAgents.PSObject.Properties.Match($k)) {
                $arr = @($fibeAgents.$k)
                $config.agents | Add-Member -Name $k -Value $arr -MemberType NoteProperty -Force
            }
        }
        # NOTE: historical plural key for the archipelago wave threshold (matches config_default.json)
        if ($fibeAgents.PSObject.Properties.Match('archipelagos_wave_height')) {
            $config.agents | Add-Member -Name "archipelagos_wave_height" -Value ([double]$fibeAgents.archipelagos_wave_height) -MemberType NoteProperty -Force
        }
        foreach ($k in @('coastal_wave_height', 'trawler_wave_height')) {
            if ($fibeAgents.PSObject.Properties.Match($k)) {
                $config.agents | Add-Member -Name $k -Value ([double]$fibeAgents.$k) -MemberType NoteProperty -Force
            }
        }
        $nA = $config.agents.num_archipelago; $nC = $config.agents.num_coastal; $nT = $config.agents.num_trawler
        if ($TimeStep -eq 1) {
            Write-Host "Agents FIBE initiaux (step 1): archipelago=$nA coastal=$nC trawler=$nT (source $fibeAgentsPath)"
        } else {
            Write-Host "Agents FIBE (step $TimeStep): champs statiques réappliqués depuis $fibeAgentsPath (seuls les num_* peuvent être écrasés par Agents_number.csv)"
        }
    } catch {
        Write-Warning "Agents FIBE: echec lecture ${fibeAgentsPath}: $_"
    }
} else {
    Write-Host "Agents FIBE: Data/fibe_agents.json absent -> agents de config_default.json conserves"
}

# --- Agents temporels : nombre par flottille / date ---
# Attendu : CSV (ou XLSX renommé .csv) avec col1 = date (OADate 40179 / 01/01/2010 / janv-10) + 3 cols archipelago/coastal/trawler
# Le chemin vient de l'UI EwE (Data/agent_numbers.json -> agent_numbers_file)
# Ce bloc écrase UNIQUEMENT les compteurs num_* (num_archipelago / num_coastal /
# num_trawler, formes imbriquée + plate). Les champs statiques (names, ports,
# habitats, wave) appliqués par le bloc "Agents FIBE" ci-dessus sont conservés.
$agentJsonPath = Join-Path $scriptDir "Data\agent_numbers.json"
if (Test-Path $agentJsonPath) {
    try {
        $agentInfo = Get-Content $agentJsonPath -Raw | ConvertFrom-Json
        $agentsFile = $agentInfo.agent_numbers_file
        if ($agentsFile -and (Test-Path $agentsFile)) {
            $currentDate = (Get-Date "$FirstYear-01-01").AddMonths($TimeStep - 1).Date
            $frCulture = [cultureinfo]::GetCultureInfo("fr-FR")
            $monthMap = @{janv=1;"févr"=2;fevr=2;mars=3;avr=4;mai=5;juin=6;juil=7;"août"=8;aout=8;sept=9;oct=10;nov=11;"déc"=12;dec=12}
            function Parse-AgentDate($raw, $strings) {
                if ($null -eq $raw) { return $null }
                $s = $raw.ToString().Trim()
                if ($s -match "^\d+(\.\d+)?$") {
                    try { return [DateTime]::FromOADate([double]$s).Date } catch {}
                }
                try { return [DateTime]::ParseExact($s,"dd/MM/yyyy",$frCulture).Date } catch {}
                try { return [DateTime]::Parse($s,$frCulture).Date } catch {}
                if ($s -match "^(.+)-(\d{2})$") {
                    $mStr = $matches[1].ToLower().Trim(); $y = 2000 + [int]$matches[2]
                    if ($monthMap.ContainsKey($mStr)) {
                        return (Get-Date -Year $y -Month $monthMap[$mStr] -Day 1).Date
                    }
                }
                return $null
            }
            $rows = @()
            $isZip = $false
            try {
                $bytes = Get-Content $agentsFile -Encoding Byte -TotalCount 4
                if ($bytes[0] -eq 0x50 -and $bytes[1] -eq 0x4B) { $isZip = $true }
            } catch {}
            if ($isZip) {
                $tmp = Join-Path $env:TEMP ("agents_unzip_" + [Guid]::NewGuid().ToString("N"))
                $tmpZip = "$tmp.zip"
                Copy-Item $agentsFile $tmpZip -Force
                Expand-Archive $tmpZip $tmp -Force
                Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue
                $sheetPath = Join-Path $tmp "xl\worksheets\sheet1.xml"
                $ssPath = Join-Path $tmp "xl\sharedStrings.xml"
                $strings = @()
                if (Test-Path $ssPath) {
                    $ssXml = [xml](Get-Content $ssPath -Raw)
                    $strings = @($ssXml.sst.si | ForEach-Object { $_.t })
                }
                $sheetXml = [xml](Get-Content $sheetPath -Raw)
                $sheetRows = $sheetXml.worksheet.sheetData.row
                $headerSkipped = $false
                foreach ($r in $sheetRows) {
                    if (-not $headerSkipped) { $headerSkipped = $true; continue } # saute archipelago/coastal/trawler
                    $cells = @($r.c)
                    if ($cells.Count -lt 4) { continue }
                    # col A = date (v ou t=s)
                    $rawDate = $cells[0].v
                    if ($cells[0].t -eq "s" -and $rawDate -match "^\d+$") {
                        $idx = [int]$rawDate; if ($idx -lt $strings.Count) { $rawDate = $strings[$idx] }
                    }
                    $d = Parse-AgentDate $rawDate $strings
                    if ($null -eq $d) { continue }
                    # cols B/C/D = entiers, peuvent être inlineStr ou v
                    function Get-CellInt($c) {
                        if ($null -eq $c) { return 0 }
                        $v = $c.v; if ($c.t -eq "s" -and $v -match "^\d+$") { $idx=[int]$v; if($idx -lt $strings.Count){$v=$strings[$idx]} }
                        try { return [int][double]$v } catch { return 0 }
                    }
                    $rows += [PSCustomObject]@{ date=$d; arch=(Get-CellInt $cells[1]); coast=(Get-CellInt $cells[2]); traw=(Get-CellInt $cells[3]) }
                }
                Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
            } else {
                $firstLine = Get-Content $agentsFile -TotalCount 1
                $delim = if ($firstLine -match "`t") { "`t" } elseif ($firstLine -match ";") { ";" } else { "," }
                $csvRows = Import-Csv $agentsFile -Delimiter $delim
                foreach ($row in $csvRows) {
                    $props = @($row.PSObject.Properties)
                    # 1ère colonne vide -> header "" ou date, on prend la 1ère valeur
                    $rawDate = $props[0].Value
                    if ([string]::IsNullOrWhiteSpace($rawDate)) { $rawDate = $props[1].Value }
                    # si header vide, les noms archipelago/coastal/trawler sont en props[1..3]
                    $d = Parse-AgentDate $rawDate $null
                    if ($null -eq $d) { continue }
                    # mapping colonnes par nom, fallback par index
                    $arch = 0; $coast=0; $traw=0
                    try { $arch = [int]$row.archipelago } catch { try { $arch = [int]$props[1].Value } catch {} }
                    try { $coast = [int]$row.coastal } catch { try { $coast = [int]$props[2].Value } catch {} }
                    try { $traw = [int]$row.trawler } catch { try { $traw = [int]$props[3].Value } catch {} }
                    # si Import-Csv a créé colonne vide "", on corrige via index
                    if ($arch -eq 0 -and $props.Count -ge 4) { try { $arch = [int]$props[1].Value } catch {} }
                    $rows += [PSCustomObject]@{ date=$d; arch=$arch; coast=$coast; traw=$traw }
                }
            }
            if ($rows.Count -gt 0) {
                $firstDate = ($rows | Sort-Object date | Select-Object -First 1).date
                if ($currentDate -ge $firstDate) {
                    $match = $rows | Where-Object { $_.date -eq $currentDate } | Select-Object -First 1
                    if ($match) {
                        # Assure structure imbriquée attendue par loader.py (agents.num_agents)
                        if (-not $config.agents.PSObject.Properties.Match('num_agents')) {
                            $config.agents | Add-Member -Name "num_agents" -Value ([PSCustomObject]@{}) -MemberType NoteProperty
                        }
                        $config.agents.num_agents | Add-Member -Name "num_archipelago" -Value $match.arch -MemberType NoteProperty -Force
                        $config.agents.num_agents | Add-Member -Name "num_coastal" -Value $match.coast -MemberType NoteProperty -Force
                        $config.agents.num_agents | Add-Member -Name "num_trawler" -Value $match.traw -MemberType NoteProperty -Force
                        # Garde aussi le format plat (config_default.json) pour rétro-compatibilité
                        $config.agents | Add-Member -Name "num_archipelago" -Value $match.arch -MemberType NoteProperty -Force
                        $config.agents | Add-Member -Name "num_coastal" -Value $match.coast -MemberType NoteProperty -Force
                        $config.agents | Add-Member -Name "num_trawler" -Value $match.traw -MemberType NoteProperty -Force
                        Write-Host "Agents temporels: $currentDate -> arch=$($match.arch) coast=$($match.coast) traw=$($match.traw) (source $agentsFile)"
                    } else {
                        # Pas de ligne exacte ce mois-ci → garde le dernier connu (stepwise constant)
                        $last = $rows | Where-Object { $_.date -le $currentDate } | Sort-Object date -Descending | Select-Object -First 1
                        if ($last) {
                            if (-not $config.agents.PSObject.Properties.Match('num_agents')) {
                                $config.agents | Add-Member -Name "num_agents" -Value ([PSCustomObject]@{}) -MemberType NoteProperty
                            }
                            $config.agents.num_agents | Add-Member -Name "num_archipelago" -Value $last.arch -MemberType NoteProperty -Force
                            $config.agents.num_agents | Add-Member -Name "num_coastal" -Value $last.coast -MemberType NoteProperty -Force
                            $config.agents.num_agents | Add-Member -Name "num_trawler" -Value $last.traw -MemberType NoteProperty -Force
                            $config.agents | Add-Member -Name "num_archipelago" -Value $last.arch -MemberType NoteProperty -Force
                            $config.agents | Add-Member -Name "num_coastal" -Value $last.coast -MemberType NoteProperty -Force
                            $config.agents | Add-Member -Name "num_trawler" -Value $last.traw -MemberType NoteProperty -Force
                            Write-Host "Agents temporels: pas de ligne exacte pour $currentDate, dernier connu $($last.date.ToString('yyyy-MM-dd')) -> arch=$($last.arch) coast=$($last.coast) traw=$($last.traw)"
                        } else {
                            $curA = if ($config.agents.PSObject.Properties.Match('num_agents')) { $config.agents.num_agents.num_archipelago } else { $config.agents.num_archipelago }
                            $curC = if ($config.agents.PSObject.Properties.Match('num_agents')) { $config.agents.num_agents.num_coastal } else { $config.agents.num_coastal }
                            $curT = if ($config.agents.PSObject.Properties.Match('num_agents')) { $config.agents.num_agents.num_trawler } else { $config.agents.num_trawler }
                            Write-Host "Agents temporels: pas de ligne pour $currentDate (première $($firstDate.ToString('yyyy-MM-dd'))) -> garde $curA/$curC/$curT"
                        }
                    }
                } else {
                    Write-Host "Agents temporels: $currentDate < première date CSV $firstDate -> garde défaut"
                }
            }
        } else {
            Write-Warning "Agents temporels: fichier introuvable $agentsFile"
        }
    } catch {
        Write-Warning "Agents temporels: échec lecture $($agentsFile): $_"
    }
} else {
    Write-Host "Agents temporels: Data/agent_numbers.json absent -> agents fixes"
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
            # Étape 4 - Fix race : laisse 50ms au FS pour que le nouveau mtime soit visible
            Start-Sleep -Milliseconds 50
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