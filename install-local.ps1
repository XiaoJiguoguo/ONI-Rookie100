param([string]$ModsRoot = "$env:USERPROFILE\Documents\Klei\OxygenNotIncluded\mods\local")
$ErrorActionPreference = 'Stop'
if (Get-Process -Name OxygenNotIncluded -ErrorAction SilentlyContinue) {
    throw 'Close Oxygen Not Included before installing the mod.'
}
$taskOutput = Join-Path $PSScriptRoot 'bin\Release'
$taskRoot = [IO.Path]::GetFullPath($ModsRoot).TrimEnd('\')
$taskDestination = [IO.Path]::GetFullPath((Join-Path $taskRoot 'Rookie100'))
if (!$taskDestination.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Invalid destination path.'
}
$taskFiles = @('Rookie100.dll', 'quests.json', 'mod.yaml', 'mod_info.yaml', 'Curriculum\catalog.v1.json', 'Videos\lessons.index.json')
$taskLessons = Get-Content -LiteralPath (Join-Path $taskOutput 'Videos\lessons.index.json') -Raw | ConvertFrom-Json
foreach ($taskLesson in $taskLessons.lessons) {
    $taskDefinitionName = [string]$taskLesson.definition
    if ([IO.Path]::GetFileName($taskDefinitionName) -ne $taskDefinitionName -or $taskDefinitionName.Contains('\') -or $taskDefinitionName.Contains('/')) { throw 'Invalid lesson manifest path.' }
    $taskDefinition = Get-Content -LiteralPath (Join-Path $taskOutput ('Videos\' + $taskDefinitionName)) -Raw | ConvertFrom-Json
    $taskMovieName = [string]$taskDefinition.file
    if ([IO.Path]::GetFileName($taskMovieName) -ne $taskMovieName -or $taskMovieName.Contains('\') -or $taskMovieName.Contains('/')) { throw 'Invalid lesson movie path.' }
    $taskFiles += @(('Videos\' + $taskDefinitionName), ('Videos\' + $taskMovieName))
}
$taskFiles = @($taskFiles | Select-Object -Unique)
foreach ($taskName in $taskFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $taskOutput $taskName))) { throw "Missing build output: $taskName" }
}
$taskBackup = Join-Path $PSScriptRoot ('.tools\deployment-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach ($taskName in $taskFiles) {
    $taskTarget = Join-Path $taskDestination $taskName
    if (Test-Path -LiteralPath $taskTarget) {
        $taskSaved = Join-Path $taskBackup $taskName
        New-Item -ItemType Directory -Path (Split-Path $taskSaved -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $taskTarget -Destination $taskSaved
    }
    New-Item -ItemType Directory -Path (Split-Path $taskTarget -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $taskOutput $taskName) -Destination $taskTarget -Force
}
$taskSwitch = Join-Path $taskDestination 'debug_export.enabled'
if (!(Test-Path -LiteralPath $taskSwitch)) { New-Item -ItemType File -Path $taskSwitch | Out-Null }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DEBUG_BRIDGE.md') -Destination (Join-Path $taskDestination 'DEBUG_BRIDGE.md') -Force
Write-Host "Installed: $taskDestination"
Write-Host 'Enable this local mod in-game; use only one Rookie100 copy.'
