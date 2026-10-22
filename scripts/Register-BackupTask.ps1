[CmdletBinding(SupportsShouldProcess)]
param([string]$ServerInstance='.\SQLEXPRESS',[string]$Database='Stokin',[Parameter(Mandatory)][string]$BackupDirectory,[PSCredential]$Credential,[string]$At='23:00')
$ErrorActionPreference='Stop'
if(-not $Credential){$Credential=Get-Credential -Message 'Akun Windows untuk backup terjadwal, termasuk ketika aplikasi ditutup.'}
$scriptPath=Join-Path $PSScriptRoot 'Backup-Stokin.ps1'
foreach($inputValue in @($scriptPath,$ServerInstance,$Database,$BackupDirectory)){if($inputValue.Contains('"') -or $inputValue.Contains("`n") -or $inputValue.Contains("`r")){throw 'Karakter argumen jadwal tidak valid.'}}
$arguments='-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File "'+$scriptPath+'" -ServerInstance "'+$ServerInstance+'" -Database "'+$Database+'" -BackupDirectory "'+$BackupDirectory+'"'
$action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
$trigger=New-ScheduledTaskTrigger -Daily -At ([datetime]::Parse($At))
$settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2)
if($PSCmdlet.ShouldProcess('STOKIN-DailyBackup','Daftarkan task Windows harian')){
    Register-ScheduledTask -TaskName 'STOKIN-DailyBackup' -Action $action -Trigger $trigger -Settings $settings -User $Credential.UserName -Password $Credential.GetNetworkCredential().Password -Force | Out-Null
}
