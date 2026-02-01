# PonziTech.BitVM

[![CI](https://github.com/PonziTech/BitVM/actions/workflows/ci.yml/badge.svg)](https://github.com/PonziTech/BitVM/actions)
[![NuGet](https://img.shields.io/nuget/v/PonziTech.BitVM.Core.svg)](https://www.nuget.org/packages/PonziTech.BitVM.Core/)

A production-quality C# binding to BitVM2, enabling trust-minimized Bitcoin bridges from .NET.

## Overview

PonziTech.BitVM provides a comprehensive .NET wrapper around BitVM, the trust-minimized Bitcoin bridge that implements Groth16 SNARK verification on Bitcoin without soft forks. This package enables CLR applications to:

- Execute Bitcoin scripts with full BitVM primitives
- Create and manage peg-in/peg-out bridge operations
- Work with Winternitz signatures and other BitVM cryptographic primitives
- Verify SNARK proofs using Groth16

## Quick Start

```bash
# Install the core package
dotnet add package PonziTech.BitVM.Core

# Install the bridge package for peg-in/peg-out operations
dotnet add package PonziTech.BitVM.Bridge
```

```csharp
using PonziTech.BitVM.Core;
using PonziTech.BitVM.Bridge;

// Execute Bitcoin scripts
using var executor = new ScriptExecutor();
var result = await executor.ExecuteAsync(scriptBytes);

// Create bridge operations
var config = new BridgeConfiguration {
    Network = BitcoinNetwork.Testnet,
    EsploraUrl = "https://mempool.space/testnet/api",
    VerifierPublicKeys = new[] { verifierPubkey }
};

using var bridge = new BridgeClient(config);
using var depositor = DepositorContext.Create(config, depositorWif);

var pegIn = await bridge.CreatePegInAsync(depositor, depositOutpoint, "0x1234...");
```

## Architecture

### Package Structure

```
PonziTech.BitVM.Native    - Auto-generated P/Invoke bindings (FFI)
PonziTech.BitVM.Core      - Core operations (script execution, crypto)
PonziTech.BitVM.Bridge    - Bridge operations (peg-in/peg-out graphs)
```

### Layered Design

```
┌─────────────────────────────────────────────────────────┐
│  C# Application (NBitcoin integration)                  │
├─────────────────────────────────────────────────────────┤
│  PonziTech.BitVM.Core  │  PonziTech.BitVM.Bridge       │
│  (Managed API)         │  (Domain Operations)           │
├─────────────────────────────────────────────────────────┤
│  PonziTech.BitVM.Native (Auto-generated FFI)            │
├─────────────────────────────────────────────────────────┤
│  libbitvm_ffi.{dll/so/dylib} (Rust)                     │
├─────────────────────────────────────────────────────────┤
│  BitVM Core Library (Rust)                              │
│  - Script execution                                     │
│  - Hash functions (SHA256, BLAKE3)                      │
│  - U32 operations                                       │
│  - BN254 elliptic curves                                │
│  - Winternitz signatures                                │
│  - Groth16 verification                                 │
├─────────────────────────────────────────────────────────┤
│  BitVM Bridge Library (Rust)                            │
│  - Peg-in/peg-out graphs                                │
│  - Transaction management                               │
│  - Multi-sig coordination                               │
└─────────────────────────────────────────────────────────┘
```

## Supported Platforms

| Platform | Architecture | Status |
|----------|--------------|--------|
| Windows | x64 | ✅ Supported |
| Linux | x64 | ✅ Supported |
| macOS | x64 | ✅ Supported |
| macOS | ARM64 | ✅ Supported |

## Building from Source

### Prerequisites

#### Common Requirements (All Platforms)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.100 or later)
- [Rust toolchain](https://rustup.rs/) (latest stable)
- Git with submodules support

#### Windows-Specific Requirements
**Option 1: Visual Studio 2022 (Recommended)**
- Visual Studio 2022 with "Desktop development with C++" workload
- Or Build Tools for Visual Studio 2022 with C++ tools

**Option 2: winget (Quick Install)**
```powershell
# Install all dependencies automatically
winget install Microsoft.DotNet.SDK.8
winget install Rustlang.Rustup
winget install Microsoft.VisualStudio.2022.BuildTools
```

**Option 3: Manual Download**
- [Build Tools for Visual Studio 2022](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)
- During installation, select:
  - "Desktop development with C++" workload, OR
  - Individual components:
    - MSVC v143 - VS 2022 C++ x64/x86 build tools
    - Windows SDK
    - C++ CMake tools for Windows

#### macOS Requirements
- Xcode Command Line Tools: `xcode-select --install`

#### Linux Requirements
- GCC: `sudo apt install build-essential` (Ubuntu/Debian) or equivalent
- OpenSSL development libraries: `sudo apt install libssl-dev`

### Verify Installation

```bash
# Check .NET
dotnet --version  # Should be 8.0.x

# Check Rust
cargo --version   # Should be 1.8x.x

# Check C++ compiler (Windows)
cl.exe            # Should show Microsoft C/C++ Compiler

# Check C++ compiler (Unix)
gcc --version     # Should show GCC version
```

### Build Steps

```bash
# Clone with submodules
git clone --recursive https://github.com/PonziTech/BitVM.git
cd BitVM

# Build everything (PowerShell)
./build/build.ps1 -Configuration Release

# Or manually:
cd ffi
cargo build --release
cd ..
dotnet build PonziTech.BitVM.sln -c Release
```

### Build for All Platforms

```powershell
# Build native libraries for all platforms
./build/build.ps1 -Configuration Release

# Pack NuGet packages
./build/build.ps1 -Configuration Release -Pack
```

### NBitcoin Flavors

PonziTech.BitVM supports building against two NBitcoin implementations:

**Standard (default)** - Public NBitcoin from NuGet:
```powershell
./build/build.ps1 -Configuration Release
# or explicitly
./build/build.ps1 -NBitcoinFlavor Standard
```

**Okeanos** - Internal fork with PoS, Smart Contracts, Cold Staking:
```powershell
./build/build.ps1 -NBitcoinFlavor Okeanos
```

The Okeanos flavor requires the [Okeanos.NBitcoin](docs/OKEANOS_INTEGRATION.md) fork to be available at `external/NBitcoin/`. This is designed for the Okeanos ecosystem.

See [docs/NBITCOIN_FLAVORS.md](docs/NBITCOIN_FLAVORS.md) for detailed documentation.

## API Examples

### Script Execution

```csharp
using var executor = new ScriptExecutor();

// Execute a simple script
var script = new byte[] { 0x51, 0x69 }; // OP_TRUE OP_VERIFY
var result = await executor.ExecuteAsync(script);

Console.WriteLine($"Success: {result.Success}");
Console.WriteLine($"Stack: {result.FinalStack}");
```

### Hash Functions

```csharp
using var executor = new ScriptExecutor();

// Generate SHA256 script
var sha256Script = executor.GenerateSha256Script(32);

// Generate BLAKE3 script
var blake3Script = executor.GenerateBlake3Script(128);

// Generate u32 operations
var pushScript = executor.GenerateU32PushScript(42);
var verifyScript = executor.GenerateU32EqualVerifyScript();
```

### Winternitz Signatures

```csharp
// Generate keypair
var secret = WinternitzSignatures.GenerateSecret();
var pubkey = WinternitzSignatures.GetPublicKey(secret, WinternitzSignatures.MessageSize.Size16);

// Sign message
var message = new byte[16]; // 16 bytes for Size16
var signature = WinternitzSignatures.Sign(secret, message, WinternitzSignatures.MessageSize.Size16);

// Get verification script
var verifyScript = WinternitzSignatures.GetChecksigScript(pubkey, WinternitzSignatures.MessageSize.Size16);
```

### Bridge Operations

```csharp
// Configure bridge
var config = new BridgeConfiguration {
    Network = BitcoinNetwork.Testnet,
    EsploraUrl = "https://mempool.space/testnet/api",
    VerifierPublicKeys = new[] { verifierPubkey1, verifierPubkey2 }
};

// Create client
using var bridge = new BridgeClient(config);

// Create depositor context
using var depositor = DepositorContext.Create(config, depositorWif);

// Get deposit address
var depositAddress = depositor.GetAddress();
Console.WriteLine($"Deposit to: {depositAddress}");

// After funding, create peg-in graph
var depositTx = await GetDepositTransactionAsync(); // Your implementation
var pegIn = await bridge.CreatePegInAsync(depositor, depositTx.Outpoint, "0xYourEvmAddress");

// Check status
var status = await bridge.GetPegInStatusAsync(pegIn);
Console.WriteLine($"Status: {status}");

// Serialize for sharing
var json = bridge.SerializePegInGraph(pegIn);
```

## Testing

```bash
# Run all tests
dotnet test PonziTech.BitVM.sln

# Run with coverage
dotnet test PonziTech.BitVM.sln --collect:"XPlat Code Coverage"
```

## Troubleshooting

### Windows: "linker `link.exe` not found"
**Problem**: Rust requires the Visual C++ linker which comes with Visual Studio Build Tools.

**Solution**:
```powershell
# Install via winget
winget install Microsoft.VisualStudio.2022.BuildTools

# Or download from:
# https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022

# IMPORTANT: Select "Desktop development with C++" workload during install
```

### Windows: "could not find `cargo`"
**Problem**: Rust is not in your PATH.

**Solution**:
```powershell
# Restart your terminal after installing Rust
# Or manually add to PATH:
$env:PATH += ";$env:USERPROFILE\.cargo\bin"
```

### "error: failed to run custom build command for `bitvm-ffi`"
**Problem**: The BitVM submodule may not be initialized.

**Solution**:
```bash
# Initialize submodules
git submodule update --init --recursive

# Verify BitVM is present
ls external/BitVM
```

### "Unable to find native library"
**Problem**: The native DLL was not copied to the runtimes directory.

**Solution**:
```powershell
# Build the FFI layer first
cd ffi
cargo build --release
cd ..

# Copy manually if needed
Copy-Item ffi\target\release\bitvm_ffi.dll runtimes\win-x64\native\
```

### "The type or namespace name 'BitVMNative' does not exist"
**Problem**: The csbindgen-generated bindings haven't been created.

**Solution**:
```bash
# Build the FFI layer - this runs the build.rs script which generates bindings
cd ffi && cargo build

# Or use the build script
./build/build.ps1
```

## Project Status

⚠️ **Warning: DO NOT USE IN PRODUCTION**

BitVM is experimental technology. This package is in early development (v0.1.0-alpha). The API is subject to change and the underlying BitVM implementation is not yet production-ready.

## Documentation

- [BitVM Whitepaper](https://bitvm.org/bitvm2)
- [BitVM Repository](https://github.com/BitVM/BitVM)
- [API Reference](docs/API.md) (coming soon)
- [Examples](examples/) (coming soon)

## Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details.

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Acknowledgments

- [BitVM](https://github.com/BitVM/BitVM) - The original BitVM implementation
- [NBitcoin](https://github.com/MetacoSA/NBitcoin) - Bitcoin library for .NET
- [Arkworks](https://github.com/arkworks-rs) - zkSNARK ecosystem

## Support

For questions and support:
- GitHub Issues: [github.com/PonziTech/BitVM/issues](https://github.com/PonziTech/BitVM/issues)
- Discord: [PonziTech Discord](https://discord.gg/ponzitech)

---

**There is no meme, we love you.** ❤️
