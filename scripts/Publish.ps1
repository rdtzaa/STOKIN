param([switch]$SelfContained)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $projectRoot 'tmp/dotnet-home'
Push-Location $projectRoot
try {
    if($SelfContained){& dotnet publish src/Stokin.Desktop -c Release -r win-x64 --self-contained true -o output/app}
    else {& dotnet publish src/Stokin.Desktop -c Release --no-restore --self-contained false -o output/app}
    if($LASTEXITCODE -ne 0){throw 'Publish gagal.'}
    Write-Output (Join-Path $projectRoot 'output/app/Stokin.Desktop.exe')
} finally {Pop-Location}
