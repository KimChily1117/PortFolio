[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskName = 'Kimchily-RemotePlay-Servers'
$taskScript = Join-Path $PSScriptRoot 'Start-GameServers.ps1'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot '.local') -Force | Out-Null
$taskWindowsUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$taskExisting = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($taskExisting) {
    if (@($taskExisting.Actions | Where-Object { $_.Arguments -notlike ('*' + $taskScript + '*') }).Count) {
        throw 'A different task already uses this name. It was not replaced.'
    }
    Write-Output 'The matching startup task is already registered.'
    exit 0
}
$taskAction = New-ScheduledTaskAction -Execute 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
    -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + $taskScript + '" -Watch') -WorkingDirectory $PSScriptRoot
$taskTrigger = New-ScheduledTaskTrigger -AtStartup
$taskTrigger.Delay = 'PT30S'
# S4U uses the existing local Windows account without storing a password.
# LocalDB belongs to this account. Do not substitute SYSTEM or another user.
$taskPrincipal = New-ScheduledTaskPrincipal -UserId $taskWindowsUser -LogonType S4U -RunLevel Highest
$taskSettings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$taskDefinition = New-ScheduledTask -Action $taskAction -Trigger $taskTrigger -Principal $taskPrincipal -Settings $taskSettings `
    -Description 'Start the Task copies of Project Dawn, UGC publisher and UGC realtime after boot; retain the existing LocalDB owner.'
Register-ScheduledTask -TaskName $taskName -InputObject $taskDefinition | Select-Object TaskName,State
Export-ScheduledTask -TaskName $taskName | Set-Content -LiteralPath (Join-Path $PSScriptRoot '.local\startup-task.xml') -Encoding UTF8
