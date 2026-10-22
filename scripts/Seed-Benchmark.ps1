param([Parameter(Mandatory)][string]$SqlTestConnection)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $root 'tmp/dotnet-home'
$priorConnection=$env:STOKIN_TEST_CONNECTION;$priorBenchmark=$env:STOKIN_RUN_BENCHMARK
$env:STOKIN_TEST_CONNECTION=$SqlTestConnection;$env:STOKIN_RUN_BENCHMARK='1'
Push-Location $root
try{& dotnet run --project tests/Stokin.IntegrationTests -c Release --no-restore -- BaselineTests;if($LASTEXITCODE -ne 0){throw 'Benchmark belum memenuhi target atau lingkungan tidak siap.'}}finally{Pop-Location;$env:STOKIN_TEST_CONNECTION=$priorConnection;$env:STOKIN_RUN_BENCHMARK=$priorBenchmark}
