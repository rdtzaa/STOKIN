param([string]$SqlTestConnection)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $projectRoot 'tmp/dotnet-home'
$previousConnection=$env:STOKIN_TEST_CONNECTION
if($SqlTestConnection){$env:STOKIN_TEST_CONNECTION=$SqlTestConnection}
Push-Location $projectRoot
try {
    & dotnet run --project tests/Stokin.Domain.Tests -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Pengujian domain/aplikasi gagal.'}
    & dotnet run --project tests/Stokin.IntegrationTests -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Pengujian integrasi/keamanan/UI gagal.'}
    if(-not $env:STOKIN_TEST_CONNECTION){Write-Warning 'Tes SQL dilewati. Atur STOKIN_TEST_CONNECTION dengan database berawalan Stokin_Test_ untuk menjalankan tes nyata.'}
} finally {$env:STOKIN_TEST_CONNECTION=$previousConnection;Pop-Location}
