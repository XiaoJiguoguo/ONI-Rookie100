$projectFile = "Rookie100.csproj"
$devModPath = "C:\Users\Administrator\Documents\Klei\OxygenNotIncluded\mods\Dev\Rookie100"

Write-Host "== dotnet build (Release -> mods\Dev\Rookie100) =="

# 尝试删除dll，释放文件锁（PowerShell内执行）
$targetDll = Join-Path $devModPath "Rookie100.dll"
if (Test-Path $targetDll) {
    Remove-Item $targetDll -Force -ErrorAction SilentlyContinue
    Write-Host "✅ 已尝试删除旧dll释放文件锁"
}

dotnet build $projectFile -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 编译失败"
    exit $LASTEXITCODE
}

Write-Host "✅ [完成] 已部署到 $devModPath"