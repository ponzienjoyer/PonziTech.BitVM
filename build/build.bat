@echo off
REM PonziTech.BitVM Build Script (Batch Version)
REM This is a fallback for environments where PowerShell execution policy prevents running .ps1 files

echo PonziTech.BitVM Build Script (Batch)
echo ===================================

REM Check if we're in VS Developer environment
if not "%VSCMD_VER%"=="" goto :has_vs_env
echo.
echo [WARNING] Visual Studio Developer environment not detected.
echo.
echo Please open "Developer Command Prompt for VS 2022" or "Developer PowerShell for VS 2022"
echo from the Start Menu, then run this script from there.
echo.
echo Alternatively, install Visual Studio Build Tools:
echo   winget install Microsoft.VisualStudio.2022.BuildTools
echo.
echo See docs/WINDOWS_BUILD.md for details.
echo.
pause
exit /b 1

:has_vs_env
echo [OK] Visual Studio environment detected: %VSCMD_VER%

REM Check for Rust
where cargo >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Rust not found. Please install:
    echo   winget install Rustlang.Rustup
    pause
    exit /b 1
)
for /f "tokens=*" %%a in ('cargo --version') do echo [OK] Rust: %%a

REM Check for .NET
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET not found. Please install:
    echo   winget install Microsoft.DotNet.SDK.10
    pause
    exit /b 1
)
for /f "tokens=*" %%a in ('dotnet --version') do echo [OK] .NET: %%a

echo.
echo Building FFI layer...
cd ffi

echo Installing csbindgen...
cargo install csbindgen --version "^1.0" 2>nul

echo Building Rust library (this may take several minutes on first run)...
cargo build --release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] FFI build failed!
    cd ..
    pause
    exit /b 1
)

cd ..

REM Copy native library
if not exist "runtimes\win-x64\native" mkdir "runtimes\win-x64\native"
copy /Y "ffi\target\release\bitvm_ffi.dll" "runtimes\win-x64\native\"
if exist "ffi\target\release\bitvm_ffi.dll.lib" copy /Y "ffi\target\release\bitvm_ffi.dll.lib" "runtimes\win-x64\native\bitvm_ffi.lib"

echo.
echo Building .NET solution...
dotnet restore PonziTech.BitVM.sln
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] NuGet restore failed!
    pause
    exit /b 1
)

dotnet build PonziTech.BitVM.sln -c Release --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo ===================================
echo Build completed successfully!
echo ===================================
echo.
pause
