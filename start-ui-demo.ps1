param([int]$Port = 4174)
$taskPython = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (!(Test-Path -LiteralPath $taskPython)) { $taskPython = (Get-Command python -ErrorAction Stop).Source }
& $taskPython -m http.server $Port --bind 127.0.0.1 --directory (Join-Path $PSScriptRoot 'ui-demo')
