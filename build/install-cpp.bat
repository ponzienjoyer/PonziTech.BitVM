@echo off
REM Quick launcher for Visual Studio Installer to add C++ tools

echo ============================================
echo VS Build Tools - C++ Workload Installer
echo ============================================
echo.
echo BuildTools is installed but C++ tools are missing.
echo Opening Visual Studio Installer...
echo.

REM Try to find and launch the VS Installer
set FOUND=0

REM Check common locations
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vs_installer.exe" (
    echo Found VS Installer, launching...
    start "" "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vs_installer.exe"
    set FOUND=1
    goto :done
)

if exist "%ProgramFiles%\Microsoft Visual Studio\Installer\vs_installer.exe" (
    echo Found VS Installer, launching...
    start "" "%ProgramFiles%\Microsoft Visual Studio\Installer\vs_installer.exe"
    set FOUND=1
    goto :done
)

if exist "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe" (
    echo Found VS Installer, launching...
    start "" "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe"
    set FOUND=1
    goto :done
)

:done
if %FOUND%==1 (
    echo.
    echo ============================================
    echo VS Installer is now open!
    echo.
    echo NEXT STEPS:
    echo 1. Click "Modify" on your Build Tools installation
    echo 2. Check "Desktop development with C++" workload
    echo 3. Click "Modify" button
    echo 4. Wait for installation to complete
    echo 5. Return to this terminal and run: .\build\build.ps1
    echo ============================================
) else (
    echo.
    echo ERROR: Could not find Visual Studio Installer!
    echo.
    echo Please download and install manually:
    echo https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022
    echo.
    echo IMPORTANT: Select "Desktop development with C++" during install!
)

pause
