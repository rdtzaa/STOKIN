param([string]$ServerInstance='.\SQLEXPRESS',[switch]$TestSql)
$ErrorActionPreference='Stop'
if(-not [Environment]::Is64BitOperatingSystem){throw 'Windows x64 diperlukan.'}
$runtimes=& dotnet --list-runtimes
if($LASTEXITCODE -ne 0 -or -not ($runtimes -match 'Microsoft.WindowsDesktop.App 8\.')){throw '.NET 8 Windows Desktop Runtime x64 diperlukan.'}
Write-Output 'OK: Windows x64 dan .NET 8 Windows Desktop Runtime.'
if($TestSql){& sqlcmd -S $ServerInstance -E -b -l 8 -Q "SET NOCOUNT ON; SELECT SERVERPROPERTY('ProductVersion') AS Versi, SERVERPROPERTY('Edition') AS Edisi;";if($LASTEXITCODE -ne 0){throw 'Koneksi Windows Authentication gagal. Periksa instance dan sesi Windows/SSPI.'}}
