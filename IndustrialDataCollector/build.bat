@echo off
chcp 65001 >nul
setlocal
set "SLN=%~dp0..\IndustrialDataCollector.sln"
set "MSB="

REM --- 1) Prefer vswhere (VS 2017+) ---
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
    for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSB=%%i"
)

REM --- 2) Fallback: common VS install locations ---
if not defined MSB (
    for %%e in (Enterprise Professional Community BuildTools) do (
        if not defined MSB if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2022\%%e\MSBuild\Current\Bin\MSBuild.exe" set "MSB=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\%%e\MSBuild\Current\Bin\MSBuild.exe"
        if not defined MSB if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\MSBuild.exe" set "MSB=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\MSBuild.exe"
    )
)

if not defined MSB (
    echo [ERROR] MSBuild not found.
    echo         Install Visual Studio 2019+ ^(with .NET Framework 4.8 developer pack^)
    echo         or the standalone Build Tools, then run this script again.
    echo         You can also just open the solution in Visual Studio:
    echo         %SLN%
    exit /b 1
)

if not exist "%SLN%" (
    echo [ERROR] Solution not found: %SLN%
    exit /b 1
)

echo [INFO] MSBuild: %MSB%
echo [INFO] Solution: %SLN%
"%MSB%" "%SLN%" /p:Configuration=Release /v:m /nologo
set "RC=%ERRORLEVEL%"
echo.
if "%RC%"=="0" (
    echo [OK] Build succeeded. Output: %~dp0bin\Release\
) else (
    echo [FAIL] Build failed with exit code %RC%
)
exit /b %RC%
