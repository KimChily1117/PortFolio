param(
    [string]$Mode,
    [string]$LogPath,
    [string]$ExtraEnvName,
    [string]$ExtraEnvValue
)
Set-Location 'E:\task\Server\Server\Server\bin\Debug\netcoreapp3.1'
if ($ExtraEnvName) { Set-Item -Path "Env:$ExtraEnvName" -Value $ExtraEnvValue }
& dotnet Server.dll 2>&1 | ForEach-Object { "{0:O} {1}" -f (Get-Date), $_ } | Tee-Object -FilePath $LogPath