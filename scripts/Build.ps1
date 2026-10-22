param([string]$OfflinePackageSource)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $projectRoot 'tmp/dotnet-home'
Push-Location $projectRoot
try {
    $restoreArgs=@('restore','STOKIN.sln','--packages',(Join-Path $projectRoot 'tmp/packages'))
    if($OfflinePackageSource){$restoreArgs+=@('--source',$OfflinePackageSource)}
    & dotnet @restoreArgs
    if($LASTEXITCODE -ne 0){throw 'Restore paket gagal.'}
    & dotnet build STOKIN.sln -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Build gagal.'}
} finally {Pop-Location}
