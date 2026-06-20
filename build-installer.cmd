@echo off
echo ========================================
echo Stock ^& Flow Installer Builder
echo ========================================
echo.

REM Step 1: Clean previous builds
echo [1/4] Cleaning previous builds...
if exist "StockAndFlow\bin\Release" rmdir /s /q "StockAndFlow\bin\Release"
if exist "installer-output" rmdir /s /q "installer-output"
echo Done.
echo.

REM Step 2: Publish the application
echo [2/4] Publishing application (self-contained)...
dotnet publish StockAndFlow/StockAndFlow.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output StockAndFlow/bin/Release/net10.0-windows/win-x64/publish ^
    /p:PublishSingleFile=false ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:PublishReadyToRun=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo Done.
echo.

REM Step 3: Check if Inno Setup is installed
echo [3/4] Checking for Inno Setup...
set INNO_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %INNO_PATH% (
    set INNO_PATH="C:\Program Files\Inno Setup 6\ISCC.exe"
)

if not exist %INNO_PATH% (
    echo.
    echo WARNING: Inno Setup not found!
    echo.
    echo Please download and install Inno Setup from:
    echo https://jrsoftware.org/isdl.php
    echo.
    echo After installation, run this script again to create the installer.
    echo.
    echo Your published application is ready at:
    echo StockAndFlow\bin\Release\net10.0-windows\win-x64\publish
    echo.
    pause
    exit /b 1
)
echo Found: %INNO_PATH%
echo.

REM Step 4: Build installer with Inno Setup
echo [4/4] Building installer with Inno Setup...
%INNO_PATH% installer-setup.iss

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Installer creation failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo SUCCESS!
echo ========================================
echo.
echo Installer created at:
echo installer-output\StockAndFlow-Setup-1.0.0.exe
echo.
echo File size:
dir /s installer-output\StockAndFlow-Setup-1.0.0.exe | find "StockAndFlow-Setup"
echo.
pause
