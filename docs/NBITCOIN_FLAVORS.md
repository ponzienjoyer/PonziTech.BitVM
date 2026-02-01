# NBitcoin Flavor Support

PonziTech.BitVM supports building against two different NBitcoin implementations:

1. **Standard** (default): Public NBitcoin from NuGet
2. **Okeanos**: Internal fork with PoS, Smart Contracts, and Cold Staking support

## Quick Reference

### Build with Standard NBitcoin (Public)
```powershell
# Default - uses NBitcoin from NuGet
.\build\build.ps1

# Explicit
.\build\build.ps1 -NBitcoinFlavor Standard
```

### Build with Okeanos NBitcoin (Internal)
```powershell
# Build with Okeanos fork
.\build\build.ps1 -NBitcoinFlavor Okeanos

# Pack for Okeanos ecosystem
.\build\build.ps1 -NBitcoinFlavor Okeanos -Pack
```

### Using in Okeanos Directory.Build.props

Add this to your `Directory.Build.props` in the Okeanos repository:

```xml
<Project>
  <PropertyGroup>
    <!-- Use Okeanos NBitcoin throughout -->
    <NBitcoinFlavor>Okeanos</NBitcoinFlavor>
  </PropertyGroup>
</Project>
```

This automatically applies to all projects that include PonziTech.BitVM.

## Project Setup for Okeanos

### As Git Submodule

1. Add PonziTech.BitVM as a submodule:
```bash
git submodule add https://github.com/PonziTech/BitVM.git external/PonziTech.BitVM
git submodule update --init --recursive
```

2. Create `Directory.Build.props` in your solution root:
```xml
<Project>
  <PropertyGroup>
    <NBitcoinFlavor>Okeanos</NBitcoinFlavor>
  </PropertyGroup>
  
  <!-- Reference your NBitcoin fork -->
  <ItemGroup Condition="'$(NBitcoinFlavor)' == 'Okeanos'">
    <ProjectReference Include="$(SolutionDir)external\NBitcoin\NBitcoin.csproj" 
                      Condition="Exists('$(SolutionDir)external\NBitcoin\NBitcoin.csproj')" />
  </ItemGroup>
</Project>
```

### Project References

In your Okeanos projects, reference BitVM normally:

```xml
<ProjectReference Include="..\..\external\PonziTech.BitVM\src\PonziTech.BitVM.Core\PonziTech.BitVM.Core.csproj" />
<ProjectReference Include="..\..\external\PonziTech.BitVM\src\PonziTech.BitVM.Bridge\PonziTech.BitVM.Bridge.csproj" />
```

The NBitcoin flavor will automatically cascade from your `Directory.Build.props`.

## How It Works

### Directory.Packages.props

The central package management conditionally defines which NBitcoin to use:

```xml
<PropertyGroup>
  <NBitcoinFlavor Condition="'$(NBitcoinFlavor)' == ''">Standard</NBitcoinFlavor>
  <NBitcoinPackageId Condition="'$(NBitcoinFlavor)' == 'Okeanos'">Okeanos.NBitcoin</NBitcoinPackageId>
  <NBitcoinPackageId Condition="'$(NBitcoinFlavor)' != 'Okeanos'">NBitcoin</NBitcoinPackageId>
</PropertyGroup>
```

### Project Files

Each project conditionally references NBitcoin:

```xml
<!-- Standard NBitcoin from NuGet -->
<PackageReference Include="NBitcoin" Condition="'$(NBitcoinFlavor)' != 'Okeanos'" />

<!-- Okeanos NBitcoin from local project -->
<ProjectReference Include="..\..\..\external\NBitcoin\NBitcoin.csproj" 
                  Condition="'$(NBitcoinFlavor)' == 'Okeanos'" />
```

## Feature Comparison

| Feature | Standard NBitcoin | Okeanos NBitcoin |
|---------|-------------------|------------------|
| Bitcoin | ✅ | ✅ |
| Proof of Stake | ❌ | ✅ |
| Smart Contract Opcodes | ❌ | ✅ |
| Cold Staking | ❌ | ✅ |
| Okeanos.Chain Integration | ❌ | ✅ |
| NuGet Availability | ✅ Public | 🔒 Internal |

## Publishing Packages

### Standard Flavor (Public NuGet)

```powershell
.\build\build.ps1 -Configuration Release -Pack
# Publishes to NuGet.org
```

### Okeanos Flavor (Internal)

```powershell
.\build\build.ps1 -Configuration Release -NBitcoinFlavor Okeanos -Pack
# Use internally or publish to private feed
```

⚠️ **Warning**: Packages built with Okeanos flavor require the Okeanos.NBitcoin fork to be available. These are for internal Okeanos ecosystem use.

## Troubleshooting

### "NBitcoin project not found"

Ensure your Okeanos.NBitcoin fork is at the expected path:
```
external/
  ├── NBitcoin/
  │   └── NBitcoin.csproj
  └── PonziTech.BitVM/
      └── src/
```

### "Package restore failed"

Clean and restore:
```powershell
dotnet clean
.\build\build.ps1 -NBitcoinFlavor Okeanos
```

### MSBuild property not propagating

Ensure `Directory.Build.props` is in a parent directory of your projects, or explicitly pass the property:
```powershell
dotnet build -p:NBitcoinFlavor=Okeanos
```

## Migration Guide

### From Standard to Okeanos

If you started with Standard and need Okeanos features:

1. Add Okeanos.NBitcoin fork as project reference
2. Set `<NBitcoinFlavor>Okeanos</NBitcoinFlavor>` in Directory.Build.props
3. Rebuild

### From Okeanos to Standard

If you want to publish publicly:

1. Remove Okeanos-specific code (PoS, Smart Contracts, etc.)
2. Set `<NBitcoinFlavor>Standard</NBitcoinFlavor>` or remove the property
3. Ensure build works with public NBitcoin
4. Publish to NuGet

## API Compatibility

The good news: Both NBitcoin versions share the same core API surface:
- `NBitcoin.Key`
- `NBitcoin.PubKey`
- `NBitcoin.BitcoinAddress`
- `NBitcoin.Transaction`
- `NBitcoin.Network`

Code written for Standard NBitcoin will compile against Okeanos NBitcoin (and vice versa for basic features).

---

**For Okeanos Team**: See `docs/OKEANOS_INTEGRATION.md` for detailed setup instructions.
