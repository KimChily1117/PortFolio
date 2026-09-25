param()
$ErrorActionPreference = 'Stop'
$vendorDirectory = $PSScriptRoot
$packageDirectory = [IO.Path]::GetFullPath((Join-Path $vendorDirectory '../..'))
$projectFile = Join-Path $vendorDirectory 'MoonSharp.Interpreter.csproj'
dotnet build $projectFile -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'MoonSharp source build failed.' }
$builtDll = Join-Path $vendorDirectory 'bin/Release/netstandard2.1/MoonSharp.Interpreter.dll'
$runtimeDll = Join-Path $packageDirectory 'Runtime/Plugins/MoonSharp.Interpreter.dll'
Copy-Item -LiteralPath $builtDll -Destination $runtimeDll
$checksProject = Join-Path $packageDirectory 'Tools~/VmChecks/VmChecks.csproj'
$upstreamDll = Join-Path $vendorDirectory 'reference/MoonSharp.Interpreter.dll'
dotnet run --project $checksProject -- $upstreamDll $runtimeDll
if ($LASTEXITCODE -ne 0) { throw 'MoonSharp public API or VM checks failed.' }
Get-FileHash -LiteralPath $runtimeDll -Algorithm SHA256
