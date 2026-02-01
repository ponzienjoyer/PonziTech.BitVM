# VS Build Tools Fix Script
# Run this to locate and fix your VS Build Tools installation

Write-Host "=== VS Build Tools Diagnostic ===" -ForegroundColor Cyan

# Search common locations for VS
$searchPaths = @(
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio",
    "${env:ProgramFiles}\Microsoft Visual Studio",
    "C:\Program Files (x86)\Microsoft Visual Studio",
    "C:\Program Files\Microsoft Visual Studio",
    "C:\VS"
)

$foundVS = $null

foreach ($path in $searchPaths) {
    if (Test-Path $path) {
        Write-Host "Found VS directory: $path" -ForegroundColor Green
        $foundVS = $path
        
        # Look for vswhere
        $vswhere = Get-ChildItem -Path $path -Recurse -Filter "vswhere.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($vswhere) {
            Write-Host "Found vswhere.exe at: $($vswhere.FullName)" -ForegroundColor Green
            
            # Run vswhere to see what's installed
            & $vswhere.FullName -all -products * -format json | ConvertFrom-Json | ForEach-Object {
                Write-Host ""
                Write-Host "Installation found:" -ForegroundColor Yellow
                Write-Host "  Path: $($_.installationPath)"
                Write-Host "  Version: $($_.installationVersion)"
                Write-Host "  Name: $($_.displayName)"
                
                # Check for C++ tools
                $hasCPP = $_.packages | Where-Object { $_.id -like "*VCTools*" }
                if ($hasCPP) {
                    Write-Host "  C++ Tools: INSTALLED ✓" -ForegroundColor Green
                } else {
                    Write-Host "  C++ Tools: NOT INSTALLED ✗" -ForegroundColor Red
                    Write-Host ""
                    Write-Host "To fix this, run:" -ForegroundColor Yellow
                    $installer = Join-Path $_.installationPath "..\..\Installer\vs_installer.exe"
                    if (Test-Path $installer) {
                        Write-Host "  & '$installer' modify --installPath '$($_.installationPath)' --add Microsoft.VisualStudio.Workload.VCTools --quiet"
                    }
                }
            }
        }
    }
}

if (-not $foundVS) {
    Write-Host ""
    Write-Host "VS Build Tools directory not found in standard locations!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Let's check if it's installed elsewhere..." -ForegroundColor Yellow
    
    # Check registry
    $regPaths = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\Setup",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\Setup"
    )
    
    foreach ($regPath in $regPaths) {
        if (Test-Path $regPath) {
            $sharedPath = Get-ItemProperty -Path $regPath -Name "SharedInstallationPath" -ErrorAction SilentlyContinue
            if ($sharedPath) {
                Write-Host "Registry indicates VS at: $($sharedPath.SharedInstallationPath)" -ForegroundColor Yellow
            }
        }
    }
    
    Write-Host ""
    Write-Host "=== SOLUTION ===" -ForegroundColor Cyan
    Write-Host "The BuildTools installer is present but C++ tools aren't installed." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Option 1 - Use Visual Studio Installer:" -ForegroundColor Yellow
    Write-Host "  1. Press Windows key, type 'Visual Studio Installer'" -ForegroundColor White
    Write-Host "  2. Click 'Modify' on your Build Tools installation" -ForegroundColor White
    Write-Host "  3. Check 'Desktop development with C++'" -ForegroundColor White
    Write-Host "  4. Click 'Modify' to install" -ForegroundColor White
    Write-Host ""
    Write-Host "Option 2 - Command line install:" -ForegroundColor Yellow
    Write-Host "  Download and run the installer with C++ workload:" -ForegroundColor White
    Write-Host '  curl -o vs_buildtools.exe https://aka.ms/vs/17/release/vs_buildtools.exe' -ForegroundColor Cyan
    Write-Host '  .\vs_buildtools.exe --add Microsoft.VisualStudio.Workload.VCTools --quiet --wait' -ForegroundColor Cyan
}

Write-Host ""
Write-Host "=== Environment Check ===" -ForegroundColor Cyan
Write-Host "VSCMD_VER: $env:VSCMD_VER"
Write-Host "VCINSTALLDIR: $env:VCINSTALLDIR"
Write-Host "LIB: $env:LIB"
