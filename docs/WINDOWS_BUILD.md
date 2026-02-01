# Windows Build Dependencies - Complete Guide

This document details all dependencies required to build PonziTech.BitVM on Windows.

## Windows Support Scope

- **Core library** (script execution + cryptographic primitives): ✅ supported on Windows.
- **Bridge library** (peg-in/peg-out operations): ❌ not supported on Windows due to upstream unix-only SSH/SFTP dependencies.
- **Workaround**: Use WSL/Linux to build and run bridge workflows.

## Required Dependencies

### 1. .NET 10.0 SDK
**Purpose**: Building and running C# projects

**Installation**:
```powershell
# Via winget (recommended)
winget install Microsoft.DotNet.SDK.10

# Or download from:
# https://dotnet.microsoft.com/download/dotnet/10.0
```

**Verification**:
```powershell
dotnet --version
# Expected: 10.0.xxx (any 10.0 version)
```

### 2. Rust Toolchain
**Purpose**: Compiling the FFI layer

**Installation**:
```powershell
# Via winget (recommended)
winget install Rustlang.Rustup

# Or from https://rustup.rs/
```

**Verification**:
```powershell
cargo --version
# Expected: 1.8x.x or later
```

**Note**: After installation, restart your terminal or run:
```powershell
$env:PATH += ";$env:USERPROFILE\.cargo\bin"
```

### 3. Visual Studio Build Tools with C++
**Purpose**: C++ compiler and linker required by Rust for building native libraries

**Installation**:
```powershell
# Via winget (recommended)
winget install Microsoft.VisualStudio.2022.BuildTools

# Or download from:
# https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022
```

**Required Components**:
During installation, you MUST select:
- **"Desktop development with C++"** workload

OR these individual components:
- MSVC v143 - VS 2022 C++ x64/x86 build tools
- Windows 11 SDK (or Windows 10 SDK)
- C++ CMake tools for Windows

**Verification**:
Open a new terminal and run:
```powershell
# This should not give an error about missing linker
cd ffi
cargo build
```

## One-Line Install (PowerShell as Admin)

```powershell
# Install ALL dependencies in one go
winget install Microsoft.DotNet.SDK.10 -e
winget install Rustlang.Rustup -e
winget install Microsoft.VisualStudio.2022.BuildTools -e

# Restart your terminal after this!
```

## Complete First-Time Setup

1. **Install all dependencies** (see above)

2. **Restart your terminal** to get updated PATH

3. **Verify installations**:
```powershell
dotnet --version
cargo --version
```

4. **Clone the repository**:
```powershell
git clone --recursive https://github.com/PonziTech/BitVM.git
cd BitVM
```

5. **Build everything**:
```powershell
# This will build Rust FFI, generate bindings, and build .NET solution
./build/build.ps1 -Configuration Release
```

## Common Issues

### Issue: "error: linker `link.exe` not found"
**Cause**: Visual Studio Build Tools not installed or C++ workload not selected

**Fix**: 
```powershell
winget install Microsoft.VisualStudio.2022.BuildTools
# Then run Visual Studio Installer and add "Desktop development with C++"
```

### Issue: "'cargo' is not recognized"
**Cause**: Rust not in PATH

**Fix**:
```powershell
# Add to PATH
$env:PATH += ";$env:USERPROFILE\.cargo\bin"

# Or restart terminal
```

### Issue: "The SDK 'Microsoft.NET.Sdk' specified could not be found"
**Cause**: .NET SDK not in PATH or wrong version

**Fix**:
```powershell
# Check installed versions
dotnet --list-sdks

# Ensure 10.0.x is installed and in global.json
```

## Manual Build (Without PowerShell Script)

If the automated build script doesn't work:

```powershell
# 1. Build Rust FFI
cd ffi
cargo build --release
cd ..

# 2. Copy native library
New-Item -ItemType Directory -Force -Path runtimes\win-x64\native
Copy-Item ffi\target\release\bitvm_ffi.dll runtimes\win-x64\native\

# 3. Restore .NET packages
dotnet restore PonziTech.BitVM.sln

# 4. Build solution
dotnet build PonziTech.BitVM.sln -c Release --no-restore

# 5. Run tests
dotnet test PonziTech.BitVM.sln -c Release --no-build
```

## Platform Support

| Component | Windows 10 | Windows 11 |
|-----------|------------|------------|
| .NET 10.0 | ✅ Supported | ✅ Supported |
| Rust | ✅ Supported | ✅ Supported |
| VS Build Tools 2022 | ✅ Supported | ✅ Supported |

## Verification Checklist

Before building, verify:

- [ ] .NET 10.0 SDK installed (`dotnet --version` shows 10.0.x)
- [ ] Rust installed (`cargo --version` shows 1.8x.x)
- [ ] Visual Studio Build Tools installed
- [ ] C++ workload selected in Build Tools
- [ ] Git submodules initialized (`external/BitVM` exists)
- [ ] Restarted terminal after installing dependencies

## Next Steps

Once all dependencies are installed:

1. Run: `./build/build.ps1 -Configuration Release`
2. Wait for build completion
3. Run: `dotnet test PonziTech.BitVM.sln`
4. Success! 🎉

---

**Note**: The first build will take several minutes as it:
1. Compiles Rust dependencies (bitvm, bridge, arkworks)
2. Generates C# bindings via csbindgen
3. Compiles .NET solution
4. Copies native libraries

Subsequent builds will be much faster!
