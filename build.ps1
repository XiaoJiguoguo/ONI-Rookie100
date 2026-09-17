# Rookie100 构建脚本
# 编译即部署：csproj 的 Release 输出直接写入缺氧 Dev 模组目录，无需拷贝步骤。
# 用法:
#   .\build.ps1          编译并部署
param(
    [string]$GamePath = "Y:\steam\steamapps\common\OxygenNotIncluded"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Test-Path (Join-Path $GamePath "OxygenNotIncluded.exe"))) {
    Write-Host "[警告] 未找到游戏目录: $GamePath（若游戏装在别处用 -GamePath 指定）" -ForegroundColor Yellow
}

Write-Host "== dotnet build (Release -> mods\Dev\Rookie100) ==" -ForegroundColor Cyan
dotnet build (Join-Path $root "Rookie100.csproj") -c Release /p:GamePath="$GamePath"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[失败] 编译未通过" -ForegroundColor Red
    exit 1
}

Write-Host "[完成] 已部署到 Documents\Klei\OxygenNotIncluded\mods\Dev\Rookie100" -ForegroundColor Green
