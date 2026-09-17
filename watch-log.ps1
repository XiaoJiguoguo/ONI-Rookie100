# 实时监视缺氧日志（Player.log），过滤本模组与错误信息
# 用法: .\watch-log.ps1        （Ctrl+C 停止）
#       .\watch-log.ps1 -All   显示全部日志行

param([switch]$All)

$log = Join-Path $env:USERPROFILE "AppData\LocalLow\Klei\Oxygen Not Included\Player.log"

if (-not (Test-Path $log)) {
    Write-Host "[提示] 日志文件不存在（游戏可能还没运行过）: $log" -ForegroundColor Yellow
    exit 1
}

Write-Host "== 监视 $log ==" -ForegroundColor Cyan
Write-Host "== 只显示 Rookie100 / 补丁 / 异常 相关行，-All 显示全部 ==" -ForegroundColor Cyan

if ($All) {
    Get-Content $log -Tail 50 -Wait
}
else {
    Get-Content $log -Tail 50 -Wait |
        Select-String -Pattern "Rookie100|百天|Harmony|harmony|patch|Exception|ExceptionFromHarmony"
}
