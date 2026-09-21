param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$managed = Join-Path $GamePath 'OxygenNotIncluded_Data/Managed/Assembly-CSharp.dll'
if (!(Test-Path -LiteralPath $managed)) { throw 'Game DLLs not found. Pass the OxygenNotIncluded installation directory.' }
$output = (Join-Path $root 'artifacts/check') + '/'
& dotnet build (Join-Path $root 'Rookie100.csproj') -c Debug --nologo "-p:GamePath=$GamePath" "-p:OutputPath=$output"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Build complete: $output (not deployed)."
