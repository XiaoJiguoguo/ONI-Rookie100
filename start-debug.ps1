param(
    [string]$Snapshot = "$env:USERPROFILE\Documents\Klei\OxygenNotIncluded\mods\local\Rookie100\debug_snapshot.json",
    [int]$Port = 4173
)
$ErrorActionPreference = 'Stop'
$taskPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (!(Test-Path -LiteralPath $taskPython)) { $taskPython = (Get-Command python -ErrorAction Stop).Source }
& $taskPython (Join-Path $PSScriptRoot 'bridge\serve.py') --snapshot $Snapshot --port $Port
