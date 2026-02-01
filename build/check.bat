@echo off
REM Simple diagnostic script for PonziTech.BitVM build requirements

echo ==========================================
echo PonziTech.BitVM Build Diagnostics
echo ==========================================
echo.

REM Check 1: .NET
echo [1] Checking .NET SDK...
"%ProgramFiles%\dotnet\dotnet.exe" --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo     FAIL: .NET SDK not found
    echo     Install: winget install Microsoft.DotNet.SDK.10
    set ALLGOOD=0
) else (
    for /f "tokens=*" %%a in ('"%ProgramFiles%\dotnet\dotnet.exe" --version') do echo     OK: .NET %%a
)
echo.

REM Check 2: Rust
echo [2] Checking Rust...
cargo --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo     FAIL: Rust not found
    echo     Install: winget install Rustlang.Rustup
    echo     NOTE: You may need to restart your terminal after installing Rust
    set ALLGOOD=0
) else (
    for /f "tokens=*" %%a in ('cargo --version') do echo     OK: %%a
)
echo.

REM Check 3: Visual Studio
echo [3] Checking Visual Studio Build Tools...
if "%VSCMD_VER%"=="" (
    echo     WARNING: Not in VS Developer environment
    echo.
    echo     Looking for VS installation...
    
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
        echo     Found vswhere.exe
        
        for /f "tokens=*" %%a in ('"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath') do set VSPATH=%%a
        
        if defined VSPATH (
            echo     VS Installation: %VSPATH%
            
            if exist "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" (
                echo     OK: C++ tools found
                echo     The build script will configure these automatically.
            ) else (
                echo     FAIL: C++ tools NOT installed
                echo     You need to install the C++ workload:
                echo     1. Open "Visual Studio Installer" from Start Menu
                echo     2. Click "Modify" on your installation
                echo     3. Select "Desktop development with C++"
                echo     4. Click Install
                set ALLGOOD=0
            )
        ) else (
            echo     FAIL: VS with C++ tools not found
            echo     Install: winget install Microsoft.VisualStudio.2022.BuildTools
            echo     IMPORTANT: Select "Desktop development with C++" workload
            set ALLGOOD=0
        )
    ) else (
        echo     FAIL: Visual Studio not found
        echo     Install: winget install Microsoft.VisualStudio.2022.BuildTools
        echo     IMPORTANT: Select "Desktop development with C++" workload
        set ALLGOOD=0
    )
) else (
    echo     OK: VS Developer environment detected ^(%VSCMD_VER%^)
)
echo.

REM Check 4: Git submodules
echo [4] Checking BitVM submodule...
if exist "external\BitVM\bitvm\src\lib.rs" (
    echo     OK: BitVM submodule present
) else (
    echo     FAIL: BitVM submodule missing
    echo     Fix: git submodule update --init --recursive
    set ALLGOOD=0
)
echo.

REM Summary
echo ==========================================
if "%ALLGOOD%"=="0" (
    echo STATUS: Issues found - please install missing components
    echo.
    echo Quick fix (run as Administrator):
    echo   winget install Microsoft.DotNet.SDK.10
    echo   winget install Rustlang.Rustup
    echo   winget install Microsoft.VisualStudio.2022.BuildTools
    echo.
    echo IMPORTANT for VS Build Tools:
    echo   After installing, open "Visual Studio Installer" from Start Menu
    echo   Click "Modify" and select "Desktop development with C++"
    echo   Then click Install/Modify to add C++ tools
) else (
    echo STATUS: All checks passed!
    echo You can now run: .\build\build.ps1
)
echo ==========================================

pause
