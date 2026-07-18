@echo off
chcp 65001 >nul
echo ============================================
echo   IndustrialDataCollector 社区版 - 环境准备
echo ============================================
echo.

REM 1. 清理旧的编译产物（防止 CS0579 重复特性等错误）
echo [1/4] 清理编译缓存...
if exist "%~dp0IndustrialDataCollector\bin" rmdir /s /q "%~dp0IndustrialDataCollector\bin"
if exist "%~dp0IndustrialDataCollector\obj" rmdir /s /q "%~dp0IndustrialDataCollector\obj"
echo        完成。

REM 2. 解除 Web 标记（下载 zip 后 Windows 会封锁文件）
echo [2/4] 解除文件 Web 标记...
powershell -Command "Get-ChildItem -Path '%~dp0' -Recurse | Unblock-File -ErrorAction SilentlyContinue"
echo        完成。

REM 3. 还原 NuGet 包
echo [3/4] 还原 NuGet 包...
where nuget >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo        nuget.exe 未找到，正在下载...
    powershell -Command "Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile '%~dp0nuget.exe'"
)
if exist "%~dp0nuget.exe" (
    "%~dp0nuget.exe" restore "%~dp0IndustrialDataCollector.sln"
) else (
    powershell -Command "& {$nugetDir = Join-Path $env:LOCALAPPDATA 'NuGet'; $nugetExe = Join-Path $nugetDir 'nuget.exe'; if (Test-Path $nugetExe) { & $nugetExe restore '%~dp0IndustrialDataCollector.sln' } else { Write-Host '请手动还原 NuGet 包：在 VS 中右键解决方案 → 还原 NuGet 包' }}"
)
echo        完成。

REM 4. 验证
echo [4/4] 验证完成。
echo.
echo ============================================
echo   环境准备完成！用 VS 打开解决方案：
echo   %~dp0IndustrialDataCollector.sln
echo ============================================
pause
