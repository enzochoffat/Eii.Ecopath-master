
$scriptDir = $PSScriptRoot
$scriptDirParent = (Get-Item $scriptDir).Parent.Parent.FullName
$fibePath = Join-Path $scriptDirParent "FIBE\diatome"
if (Test-Path $fibePath) {
    exit
} else {
    $fibeParent = Join-Path $scriptDirParent "FIBE"
    if (-not (Test-Path $fibeParent)) {
        New-Item -Path $fibeParent -ItemType Directory -Force | Out-Null
    }
    Set-Location $fibeParent
    git clone https://github.com/enzochoffat/diatome.git
    Set-Location $fibePath
    python -m venv venv
    .\venv\Scripts\Activate.ps1
    pip install -r requirement.txt
}
