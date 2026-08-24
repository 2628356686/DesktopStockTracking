@echo off
setlocal EnableExtensions
chcp 65001 >nul

for %%I in ("%~dp0..") do set "DST_PROJECT_DIR=%%~fI"
set "DST_PROJECT_FILE=%DST_PROJECT_DIR%\StockTickerLite.csproj"
set "DST_PUBLIC_DIR=%DST_PROJECT_DIR%\public"
set "DST_PUBLISH_DIR=%DST_PUBLIC_DIR%\win-x64"
set "DST_PUBLISH_EXE=%DST_PUBLISH_DIR%\StockTickerLite.exe"
set "DST_ZIP_FILE=%DST_PUBLIC_DIR%\StockTickerLite-win-x64.zip"

rem Build and restore must use a direct connection as well.
set "HTTP_PROXY="
set "HTTPS_PROXY="
set "ALL_PROXY="
set "http_proxy="
set "https_proxy="
set "all_proxy="

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK was not found. Install the .NET 9 SDK first.
    exit /b 1
)

echo [1/5] Closing the previously published app if it is running...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$target=[IO.Path]::GetFullPath($env:DST_PUBLISH_EXE); Get-Process -Name 'StockTickerLite' -ErrorAction SilentlyContinue | Where-Object { try { [IO.Path]::GetFullPath($_.Path) -eq $target } catch { $false } } | ForEach-Object { Write-Host ('Stopping published app, PID {0}...' -f $_.Id); Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }; exit 0"

echo [2/5] Cleaning Release output...
dotnet clean "%DST_PROJECT_FILE%" -c Release
if errorlevel 1 goto :failed

echo [3/5] Preparing public directory...
if exist "%DST_PUBLISH_DIR%" rmdir /s /q "%DST_PUBLISH_DIR%"
if exist "%DST_PUBLISH_DIR%" (
    echo [ERROR] Cannot clean "%DST_PUBLISH_DIR%". Close any app using files in this directory.
    exit /b 1
)
if exist "%DST_ZIP_FILE%" del /q "%DST_ZIP_FILE%"
if exist "%DST_ZIP_FILE%" (
    echo [ERROR] Cannot remove "%DST_ZIP_FILE%". Close any app using this ZIP file.
    exit /b 1
)
if not exist "%DST_PUBLIC_DIR%" mkdir "%DST_PUBLIC_DIR%"
if errorlevel 1 goto :failed

echo [4/5] Publishing Windows x64 self-contained executable...
dotnet publish "%DST_PROJECT_FILE%" -p:PublishProfile=WinX64SelfContained -o "%DST_PUBLISH_DIR%"
if errorlevel 1 goto :failed

if not exist "%DST_PUBLISH_EXE%" (
    echo [ERROR] Publish completed without StockTickerLite.exe.
    exit /b 1
)

echo [5/5] Creating ZIP package...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; Compress-Archive -Path (Join-Path $env:DST_PUBLISH_DIR '*') -DestinationPath $env:DST_ZIP_FILE -Force"
if errorlevel 1 goto :failed

echo.
echo Build completed successfully.
echo EXE: %DST_PUBLISH_EXE%
echo ZIP: %DST_ZIP_FILE%
exit /b 0

:failed
set "DST_EXIT_CODE=%errorlevel%"
echo.
echo [ERROR] Build failed with exit code %DST_EXIT_CODE%.
exit /b %DST_EXIT_CODE%
