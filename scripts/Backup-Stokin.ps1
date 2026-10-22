[CmdletBinding()]
param([string]$ServerInstance='.\SQLEXPRESS',[string]$Database='Stokin',[Parameter(Mandatory)][string]$BackupDirectory)
$ErrorActionPreference='Stop'
if($Database -notmatch '^Stokin(?:_[A-Za-z0-9_]+)?$'){throw 'Nama database tidak valid.'}
if($ServerInstance -notmatch '^(\.|localhost|\(local\)|'+[regex]::Escape($env:COMPUTERNAME)+')(\\[A-Za-z0-9_]+)?$'){throw 'Jalankan skrip penjadwalan pada komputer SQL Server; path dan retensi memakai filesystem server lokal.'}
$backupRoot=[IO.Path]::GetFullPath($BackupDirectory)
function Invoke-VerifiedBackup([string]$kind,[int]$keep){
    $folder=Join-Path $backupRoot $kind
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $file=Join-Path $folder ($Database+'_'+$kind+'_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.bak')
    $literal=$file.Replace("'","''")
    $sql="BACKUP DATABASE [$Database] TO DISK=N'$literal' WITH COPY_ONLY,INIT,CHECKSUM;RESTORE VERIFYONLY FROM DISK=N'$literal' WITH CHECKSUM;"
    & sqlcmd -S $ServerInstance -d master -E -b -l 15 -t 1200 -Q $sql
    if($LASTEXITCODE -ne 0){throw ('Backup/verifikasi gagal: '+$file)}
    Set-Content -LiteralPath ($file+'.verified') -Value (Get-Date -Format o) -Encoding UTF8
    $verified=Get-ChildItem -LiteralPath $folder -File -Filter ($Database+'_'+$kind+'_*.bak') | Where-Object {Test-Path -LiteralPath ($_.FullName+'.verified')} | Sort-Object LastWriteTime -Descending
    foreach($old in $verified | Select-Object -Skip $keep){
        $target=[IO.Path]::GetFullPath($old.FullName)
        if(-not $target.StartsWith($folder+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Target retensi berada di luar folder backup.'}
        Remove-Item -LiteralPath $target
        Remove-Item -LiteralPath ($target+'.verified')
    }
    Write-Output ('Backup terverifikasi: '+$file)
}
Invoke-VerifiedBackup 'daily' 7
if((Get-Date).DayOfWeek -eq [DayOfWeek]::Sunday){Invoke-VerifiedBackup 'weekly' 4}
