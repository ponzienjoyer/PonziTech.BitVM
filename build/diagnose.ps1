# PonziTech.BitVM Environment Diagnostics
# Run this to check what dependencies are installed and what's missing

param(
    [switch]$Fix
)

$ErrorActionPreference = "Continue"

Write-Host "PonziTech.BitVM Environment Diagnostics" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# Check 1: PowerShell Version
Write-Host "[Check 1] PowerShell Version" -ForegroundColor Yellow
$psVersion = $PSVersionTable.PSVersion
Write-Host "  Version: $($psVersion.Major).$($psVersion.Minor).$($psVersion.Patch)"
if ($psVersion.Major -lt 5) {
    Write-Host "  ❌ FAILED: PowerShell 5.1 or later required" -ForegroundColor Red
    $allGood = $false
} else {
    Write-Host "  ✓ OK" -ForegroundColor Green
}
Write-Host ""

# Check 2: .NET SDK
Write-Host "[Check 2] .NET SDK" -ForegroundColor Yellow
try {
    $dotnetVersion = & dotnet --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $dotnetVersion) {
        Write-Host "  Version: $dotnetVersion"
        if ($dotnetVersion -match "^10\.") {
            Write-Host "  ✓ OK (.NET 10.0 detected)" -ForegroundColor Green
        } else {
            Write-Host "  ⚠ WARNING: .NET 10.0 recommended (found $dotnetVersion)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ❌ FAILED: .NET SDK not found or not working" -ForegroundColor Red
        Write-Host "  Install: winget install Microsoft.DotNet.SDK.10" -ForegroundColor Cyan
        $allGood = $false
    }
} catch {
    Write-Host "  ❌ FAILED: .NET SDK not in PATH" -ForegroundColor Red
    Write-Host "  Install: winget install Microsoft.DotNet.SDK.10" -ForegroundColor Cyan
    $allGood = $false
}
Write-Host ""

# Check 3: Rust/Cargo
Write-Host "[Check 3] Rust Toolchain" -ForegroundColor Yellow
try {
    $cargoVersion = & cargo --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $cargoVersion) {
        Write-Host "  Version: $cargoVersion"
        Write-Host "  ✓ OK" -ForegroundColor Green
    } else {
        throw "Not found"
    }
} catch {
    # Try common paths
    $cargoPaths = @(
        "$env:USERPROFILE\.cargo\bin\cargo.exe",
        "$env:CARGO_HOME\bin\cargo.exe"
    )
    $found = $false
    foreach ($path in $cargoPaths) {
        if (Test-Path $path) {
            Write-Host "  Found at: $path"
            Write-Host "  ⚠ WARNING: Not in PATH - will attempt to add temporarily" -ForegroundColor Yellow
            $found = $true
            break
        }
    }
    if (-not $found) {
        Write-Host "  ❌ FAILED: Rust/Cargo not found" -ForegroundColor Red
        Write-Host "  Install: winget install Rustlang.Rustup" -ForegroundColor Cyan
        $allGood = $false
    }
}
Write-Host ""

# Check 4: Visual Studio / MSVC
Write-Host "[Check 4] Visual Studio / MSVC Build Tools" -ForegroundColor Yellow

# Check if we're in VS environment
if ($env:VSCMD_VER) {
    Write-Host "  VS Developer environment: YES ($env:VSCMD_VER)"
    Write-Host "  ✓ OK" -ForegroundColor Green
} else {
    Write-Host "  VS Developer environment: NO"
    
    # Try to find VS
    $vsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vsWherePath) {
        Write-Host "  vswhere.exe: Found"
        try {
            $vsPath = & $vsWherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
            if ($vsPath) {
                Write-Host "  VS Installation: $vsPath"
                
                # Check for vcvars
                $vcvarsPath = Join-Path $vsPath "VC\Auxiliary\Build\vcvars64.bat"
                if (Test-Path $vcvarsPath) {
                    Write-Host "  vcvars64.bat: Found"
                    Write-Host "  ⚠ WARNING: Not in VS Developer environment" -ForegroundColor Yellow
                    Write-Host "    The build script will attempt to configure this automatically." -ForegroundColor Gray
                } else {
                    Write-Host "  ❌ FAILED: C++ tools not installed (vcvars64.bat missing)" -ForegroundColor Red
                    Write-Host "  Install C++ workload via Visual Studio Installer" -ForegroundColor Cyan
                    $allGood = $false
                }
            } else {
                Write-Host "  ❌ FAILED: VS with C++ tools not found" -ForegroundColor Red
                Write-Host "  Install: winget install Microsoft.VisualStudio.2022.BuildTools" -ForegroundColor Cyan
                Write-Host "  IMPORTANT: Select 'Desktop development with C++' workload" -ForegroundColor Cyan
                $allGood = $false
            }
        } catch {
            Write-Host "  ❌ FAILED: Error running vswhere.exe" -ForegroundColor Red
            $allGood = $false
        }
    } else {
        Write-Host "  vswhere.exe: Not found"
        Write-Host "  ❌ FAILED: Visual Studio Build Tools not installed" -ForegroundColor Red
        Write-Host "  Install: winget install Microsoft.VisualStudio.2022.BuildTools" -ForegroundColor Cyan
        Write-Host "  IMPORTANT: Select 'Desktop development with C++' workload" -ForegroundColor Cyan
        $allGood = $false
    }
}
Write-Host ""

# Check 5: Git Submodules
Write-Host "[Check 5] Git Submodules (BitVM)" -ForegroundColor Yellow
$bitvmPath = "$PSScriptRoot\..\external\BitVM"
if (Test-Path "$bitvmPath\bitvm\src\lib.rs") {
    Write-Host "  BitVM submodule: Present"
    Write-Host "  ✓ OK" -ForegroundColor Green
} else {
    Write-Host "  ❌ FAILED: BitVM submodule not initialized" -ForegroundColor Red
    Write-Host "  Fix: git submodule update --init --recursive" -ForegroundColor Cyan
    $allGood = $false
}
Write-Host ""

# Summary
Write-Host "======================================" -ForegroundColor Cyan
if ($allGood) {
    Write-Host "✓ All checks passed! Ready to build." -ForegroundColor Green
    Write-Host ""
    Write-Host "Run: .\build\build.ps1" -ForegroundColor Cyan
} else {
    Write-Host "❌ Some checks failed. Please install missing dependencies." -ForegroundColor Red
    Write-Host ""
    Write-Host "Quick install (run as Administrator):" -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.DotNet.SDK.10" -ForegroundColor Cyan
    Write-Host "  winget install Rustlang.Rustup" -ForegroundColor Cyan
    Write-Host "  winget install Microsoft.VisualStudio.2022.BuildTools" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Then restart your terminal and run diagnostics again." -ForegroundColor Yellow
}
Write-Host ""

# Auto-fix option
if ($Fix -and -not $allGood) {
    Write-Host "Attempting to fix issues..." -ForegroundColor Yellow
    
    # Add Rust to PATH if found but not in PATH
    $cargoPath = "$env:USERPROFILE\.cargo\bin"
    if (Test-Path "$cargoPath\cargo.exe") {
        if ($env:PATH -notlike "*$cargoPath*") {
            Write-Host "Adding Rust to PATH..." -ForegroundColor Yellow
            $env:PATH = "$cargoPath;$env:PATH"
            Write-Host "✓ Rust added to PATH for this session" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "Run diagnostics again to verify fixes:" -ForegroundColor Cyan
    Write-Host "  .\build\diagnose.ps1" -ForegroundColor Cyan
}
