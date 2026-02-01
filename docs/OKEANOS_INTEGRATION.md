# Okeanos Integration Guide

This guide explains how to integrate PonziTech.BitVM into the Okeanos ecosystem with full support for Okeanos.NBitcoin (the internal fork with PoS, Smart Contracts, and Cold Staking).

## Prerequisites

- Okeanos repository structure with NBitcoin fork
- PonziTech.BitVM added as git submodule
- Visual Studio 2022 or VS Build Tools

## Directory Structure

Your Okeanos repository should look like this:

```
Okeanos/
├── src/
│   ├── external/
│   │   ├── NBitcoin/              # Your fork
│   │   │   └── NBitcoin.csproj
│   │   └── PonziTech.BitVM/       # This repo as submodule
│   │       ├── src/
│   │       │   ├── PonziTech.BitVM.Core/
│   │       │   └── PonziTech.BitVM.Bridge/
│   │       └── build/
│   │           └── build.ps1
│   └── Okeanos.Chain/             # Your projects
│       └── Okeanos.Chain.csproj
├── Directory.Build.props          # Sets NBitcoinFlavor
└── Okeanos.sln
```

## Setup Instructions

### Step 1: Add PonziTech.BitVM as Submodule

```bash
cd D:\industrial-illusions\Okeanos
git submodule add https://github.com/PonziTech/BitVM.git src/external/PonziTech.BitVM
git submodule update --init --recursive
```

### Step 2: Create Directory.Build.props

Create `src/Directory.Build.props` (or modify existing):

```xml
<Project>
  <PropertyGroup>
    <!-- Use Okeanos NBitcoin throughout the solution -->
    <NBitcoinFlavor>Okeanos</NBitcoinFlavor>
    
    <!-- Other common properties -->
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

### Step 3: Reference BitVM in Your Projects

In your Okeanos project files:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Reference BitVM with automatic NBitcoin flavor -->
    <ProjectReference Include="..\..\external\PonziTech.BitVM\src\PonziTech.BitVM.Core\PonziTech.BitVM.Core.csproj" />
    <ProjectReference Include="..\..\external\PonziTech.BitVM\src\PonziTech.BitVM.Bridge\PonziTech.BitVM.Bridge.csproj" />
  </ItemGroup>
</Project>
```

### Step 4: Build

```powershell
cd D:\industrial-illusions\Okeanos\src\external\PonziTech.BitVM
.\build\build.ps1 -NBitcoinFlavor Okeanos
```

Or from Okeanos root:

```powershell
cd D:\industrial-illusions\Okeanos\src
dotnet build -p:NBitcoinFlavor=Okeanos
```

## Using BitVM in Okeanos Code

### Basic Script Execution

```csharp
using PonziTech.BitVM.Core;

public class BitVmService
{
    public void ExecuteScript(byte[] scriptBytes)
    {
        using var executor = new ScriptExecutor();
        var result = executor.Execute(scriptBytes);
        
        if (result.Success)
        {
            Console.WriteLine("Script executed successfully!");
        }
    }
}
```

### Bridge Operations with Okeanos Features

```csharp
using PonziTech.BitVM.Bridge;
using NBitcoin;  // This is Okeanos.NBitcoin with PoS support!

public class BridgeService
{
    public async Task<PegInGraph> CreatePegIn(
        string depositorWif, 
        OutPoint depositOutpoint,
        string evmAddress)
    {
        var config = new BridgeConfiguration
        {
            Network = BitcoinNetwork.Mainnet,  // Or your Okeanos network
            EsploraUrl = "https://mempool.space/api",
            VerifierPublicKeys = GetVerifierKeys()
        };
        
        using var bridge = new BridgeClient(config);
        using var depositor = DepositorContext.Create(config, depositorWif);
        
        // Create peg-in with full Okeanos network support
        var pegIn = await bridge.CreatePegInAsync(depositor, depositOutpoint, depositAmount, evmAddress);
        
        return pegIn;
    }
    
    private byte[][] GetVerifierKeys()
    {
        // Load from your Okeanos validator set
        // This works with PoS validator keys!
        return new[] { /* validator pubkeys */ };
    }
}
```

## Advanced: Custom Okeanos Network

Since you're using Okeanos.NBitcoin, you can work with custom networks:

```csharp
// Access Okeanos-specific networks
var network = OkeanosNetworks.Mainnet;  // Your custom network with PoS

// Create addresses for cold staking
var coldStakingAddress = BitcoinAddress.Create("okeanos1...", network);

// Work with smart contract outputs
var contractTx = new Transaction();
contractTx.Outputs.Add(new TxOut(
    Money.Coins(1.0m),
    SmartContractScript.CreateP2SH(contractCode)
));
```

## Building Packages for Internal Distribution

If you need to distribute BitVM packages within your organization:

```powershell
cd D:\industrial-illusions\Okeanos\src\external\PonziTech.BitVM
.\build\build.ps1 -NBitcoinFlavor Okeanos -Pack
```

This creates:
- `PonziTech.BitVM.Core.1.0.0.nupkg` (linked to Okeanos.NBitcoin)
- `PonziTech.BitVM.Bridge.1.0.0.nupkg` (linked to Okeanos.NBitcoin)

Publish to your internal NuGet feed:
```powershell
dotnet nuget push packages\PonziTech.BitVM.Core.1.0.0.nupkg `
  --source https://your-internal-nuget-feed/nuget `
  --api-key YOUR_API_KEY
```

## Troubleshooting

### Build says it can't find NBitcoin

Check that the relative paths are correct:
- From `PonziTech.BitVM.Core.csproj`: `..\..\..\external\NBitcoin\NBitcoin.csproj`
- This should resolve to: `src\external\NBitcoin\NBitcoin.csproj`

### Okeanos-specific features not available

Ensure you're actually using Okeanos.NBitcoin:
```csharp
// Check the assembly
Console.WriteLine(typeof(Key).Assembly.FullName);
// Should show: Okeanos.NBitcoin, Version=...
```

### Conflicting NBitcoin versions

If you have both NBitcoin and Okeanos.NBitcoin referenced, remove the standard one:
```xml
<!-- Remove this -->
<!-- <PackageReference Include="NBitcoin" /> -->

<!-- Keep only Okeanos -->
<ProjectReference Include="..\..\external\NBitcoin\NBitcoin.csproj" />
```

## Continuous Integration

### GitHub Actions for Okeanos

```yaml
name: Build with Okeanos NBitcoin

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive
          
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          
      - name: Build
        run: dotnet build -p:NBitcoinFlavor=Okeanos
        working-directory: src
```

## Best Practices

1. **Always set NBitcoinFlavor in Directory.Build.props** - Don't rely on defaults
2. **Use project references, not package references** - During development
3. **Version lock your submodule** - Pin to a specific BitVM commit
4. **Build and test both flavors** - Before publishing anything

## Questions?

- **For BitVM issues**: See main README.md
- **For NBitcoin flavor docs**: See docs/NBITCOIN_FLAVORS.md
- **For Okeanos-specific**: Contact the Okeanos team

---

**Remember**: Okeanos flavor builds are for internal use. If you need to publish to public NuGet, build with Standard flavor and ensure your code works with public NBitcoin.
