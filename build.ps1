param(
    [string]$GamePath = 'C:\STEAM\steamapps\common\OxygenNotIncluded',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
if (!$PSBoundParameters.ContainsKey('GamePath') -and $env:ONI_GAME_PATH) {
    $GamePath = $env:ONI_GAME_PATH
}
$taskDotnet = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $taskDotnet)) {
    $taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source
}
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools\packages'
& $taskDotnet build (Join-Path $PSScriptRoot 'Rookie100.csproj') -c $Configuration --nologo "/p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Build complete: $PSScriptRoot\bin\$Configuration"
