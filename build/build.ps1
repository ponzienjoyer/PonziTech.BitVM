# PonziTech.BitVM Build Script
# This script builds the complete PonziTech.BitVM solution including:
# 1. Rust FFI layer
# 2. C# bindings (auto-generated)
# 3. Native binaries for all platforms
# 4. .NET solution
#
# NBitcoin Flavor Support:
#   - Standard (default): Uses public NBitcoin from NuGet
#   - Okeanos: Uses Okeanos.NBitcoin from external/NBitcoin submodule
#
# Usage:
#   .\build.ps1                                    # Build with Standard NBitcoin
#   .\build.ps1 -NBitcoinFlavor Okeanos            # Build with Okeanos NBitcoin
#   .\build.ps1 -Pack                              # Create NuGet packages (Standard only)
#   .\build.ps1 -NBitcoinFlavor Okeanos -Pack      # Create packages with Okeanos flavor

param(
    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter()]
    [ValidateSet("Standard", "Okeanos")]
    [string]$NBitcoinFlavor = "Standard",
    
    [Parameter()]
    [switch]$SkipNative,
    
    [Parameter()]
    [switch]$SkipDotNet,
    
    [Parameter()]
    [switch]$Pack,
    
    [Parameter()]
    [switch]$Test
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Write-Host "PonziTech.BitVM Build Script" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "NBitcoin Flavor: $NBitcoinFlavor"
Write-Host "Root Directory: $rootDir"
Write-Host ""

if ($NBitcoinFlavor -eq "Okeanos") {
    Write-Host "WARNING: Building with Okeanos.NBitcoin (internal fork)" -ForegroundColor Yellow
    Write-Host "         This flavor is for Okeanos ecosystem use only." -ForegroundColor Yellow
    Write-Host ""
}

# Check if we're in a Visual Studio Developer environment
function Test-VSEnvironment {
    if ($env:VSCMD_VER -or $env:VCINSTALLDIR) {
        return $true
    }
    return $false
}

# Setup Visual Studio environment for Rust/MSVC builds
function Setup-VSEnvironment {
    if (Test-VSEnvironment) {
        return
    }
    
    Write-Host "Visual Studio environment not detected. Attempting to configure..." -ForegroundColor Yellow
    
    $vsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    
    if (-not (Test-Path $vsWherePath)) {
        Write-Error @"
Visual Studio Build Tools not found!

Please install Visual Studio 2022 Build Tools with C++ support:

Option 1 - Using winget (recommended):
    winget install Microsoft.VisualStudio.2022.BuildTools

Option 2 - Manual download:
    https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022

IMPORTANT: During installation, select:
    - "Desktop development with C++" workload

See docs/WINDOWS_BUILD.md for detailed instructions.
"@
        exit 1
    }
    
    $vsPath = & $vsWherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    
    if (-not $vsPath) {
        Write-Error @"
Visual Studio C++ tools not found!

Please ensure Visual Studio 2022 Build Tools are installed with:
    - "Desktop development with C++" workload

Run: winget install Microsoft.VisualStudio.2022.BuildTools

See docs/WINDOWS_BUILD.md for detailed instructions.
"@
        exit 1
    }
    
    Write-Host "Found Visual Studio at: $vsPath" -ForegroundColor Green
    
    $vcvarsPath = Join-Path $vsPath "VC\Auxiliary\Build\vcvars64.bat"
    
    if (-not (Test-Path $vcvarsPath)) {
        Write-Error "vcvars64.bat not found at: $vcvarsPath"
        exit 1
    }
    
    Write-Host "Setting up MSVC environment..." -ForegroundColor Yellow
    
    $envDump = cmd /c "`"$vcvarsPath`" && set" 2>`$null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to execute vcvars64.bat"
        exit 1
    }
    
    $envDump | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            $varName = $matches[1]
            $varValue = $matches[2]
            Set-Item -Path "Env:$varName" -Value $varValue -Force
        }
    }
    
    Write-Host "MSVC environment configured successfully" -ForegroundColor Green
    Write-Host ""
}

# Ensure Rust is available
function Ensure-Rust {
    $cargoPath = Get-Command cargo -ErrorAction SilentlyContinue
    if (-not $cargoPath) {
        $possiblePaths = @(
            "$env:USERPROFILE\.cargo\bin\cargo.exe",
            "$env:CARGO_HOME\bin\cargo.exe"
        )
        
        foreach ($path in $possiblePaths) {
            if (Test-Path $path) {
                $env:PATH = "$([System.IO.Path]::GetDirectoryName($path));$env:PATH"
                Write-Host "Found Rust at: $path" -ForegroundColor Green
                return
            }
        }
        
        Write-Host "Rust not found. Installing via winget..." -ForegroundColor Yellow
        winget install --id Rustlang.Rustup -e --accept-package-agreements --accept-source-agreements
        $env:PATH = "$env:USERPROFILE\.cargo\bin;$env:PATH"
    }
    
    try {
        $rustVersion = & cargo --version 2>&1
        Write-Host "Rust version: $rustVersion" -ForegroundColor Green
    } catch {
        Write-Error "Failed to verify Rust installation"
        exit 1
    }
}

# Build FFI layer
function Build-FFI {
    Write-Host "`nBuilding FFI layer..." -ForegroundColor Cyan
    
    Set-Location "$rootDir\ffi"
    
    Setup-VSEnvironment
    
    try {
        & cargo install csbindgen --version "^1.0" 2>&1 | Out-Null
    } catch {
        Write-Host "csbindgen already installed or install failed (OK if already installed)" -ForegroundColor Yellow
    }
    
    Write-Host "Building FFI library..." -ForegroundColor Yellow
    Write-Host "Note: First build may take several minutes" -ForegroundColor Gray
    & cargo build --release
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "FFI build failed"
        exit 1
    }
    
    $sourceDll = "$rootDir\ffi\target\release\bitvm_ffi.dll"
    $sourceLib = "$rootDir\ffi\target\release\bitvm_ffi.dll.lib"
    
    if (Test-Path $sourceDll) {
        $runtimeDir = "$rootDir\runtimes\win-x64\native"
        New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null
        Copy-Item $sourceDll $runtimeDir -Force
        Write-Host "Copied native library to: $runtimeDir" -ForegroundColor Green
        
        if (Test-Path $sourceLib) {
            Copy-Item $sourceLib "$runtimeDir\bitvm_ffi.lib" -Force
        }
    } else {
        Write-Warning "Native library not found at: $sourceDll"
    }
    
    Set-Location $rootDir
}

# Build .NET solution
function Build-DotNet {
    Write-Host "`nBuilding .NET solution..." -ForegroundColor Cyan
    
    Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
    & dotnet restore "$rootDir\PonziTech.BitVM.sln" --verbosity quiet /p:Platform="Any CPU" /p:NBitcoinFlavor="$NBitcoinFlavor"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "NuGet restore failed"
        exit 1
    }
    
    Write-Host "Building solution..." -ForegroundColor Yellow
    & dotnet build "$rootDir\PonziTech.BitVM.sln" -c $Configuration --no-restore --verbosity quiet /p:Platform="Any CPU" /p:NBitcoinFlavor="$NBitcoinFlavor"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
    
    Write-Host "Build succeeded!" -ForegroundColor Green
}

# Run tests
function Run-Tests {
    Write-Host "`nRunning tests..." -ForegroundColor Cyan
    
    & dotnet test "$rootDir\PonziTech.BitVM.sln" -c $Configuration --no-build
    
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Some tests failed"
    } else {
        Write-Host "All tests passed!" -ForegroundColor Green
    }
}

# Pack NuGet packages
function Pack-NuGet {
    Write-Host "`nPacking NuGet packages..." -ForegroundColor Cyan
    
    $outputDir = "$rootDir\packages"
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    
    # Clear platform to avoid path issues during pack
    $env:Platform = $null
    
    if ($NBitcoinFlavor -eq "Okeanos") {
        Write-Host "WARNING: Packing with Okeanos.NBitcoin creates packages that require" -ForegroundColor Yellow
        Write-Host "         the Okeanos.NBitcoin fork to be available at runtime." -ForegroundColor Yellow
        Write-Host "         These packages are for internal Okeanos ecosystem use only." -ForegroundColor Yellow
        Write-Host ""
    }
    
    $projects = @(
        "src\PonziTech.BitVM.Core\PonziTech.BitVM.Core.csproj",
        "src\PonziTech.BitVM.Bridge\PonziTech.BitVM.Bridge.csproj"
    )
    
    foreach ($project in $projects) {
        Write-Host "Packing $project..." -ForegroundColor Yellow
        & dotnet pack "$rootDir\$project" -c $Configuration --no-build -o $outputDir /p:NBitcoinFlavor="$NBitcoinFlavor"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to pack $project"
            exit 1
        }
    }
    
    Write-Host "Packages created in: $outputDir" -ForegroundColor Green
    Get-ChildItem $outputDir -Filter "*.nupkg" | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Gray
    }
}

# Main execution
try {
    if (-not $SkipNative) {
        Ensure-Rust
        Build-FFI
    }
    
    if (-not $SkipDotNet) {
        Build-DotNet
    }
    
    if ($Test) {
        Run-Tests
    }
    
    if ($Pack) {
        Pack-NuGet
    }
    
    Write-Host "`nBuild completed successfully!" -ForegroundColor Green
    
    if ($NBitcoinFlavor -eq "Okeanos") {
        Write-Host "`nOkeanos Flavor Build Summary:" -ForegroundColor Cyan
        Write-Host "   - Built with Okeanos.NBitcoin (internal fork)" -ForegroundColor White
        Write-Host "   - Features: PoS, Smart Contracts, Cold Staking" -ForegroundColor White
        Write-Host "   - For Okeanos ecosystem use only" -ForegroundColor White
    }
} catch {
    Write-Error "Build failed: $_"
    exit 1
} finally {
    Set-Location $rootDir
}
