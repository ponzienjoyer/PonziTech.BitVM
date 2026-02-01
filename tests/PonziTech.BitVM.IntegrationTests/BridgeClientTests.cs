using System;
using System.Linq;
using Xunit;
using NBitcoin;
using PonziTech.BitVM.Bridge;

namespace PonziTech.BitVM.IntegrationTests;

public class BridgeClientTests : IDisposable
{
    private readonly BridgeClient? _client;
    private readonly BridgeConfiguration? _config;
    private readonly bool _skip;

    public BridgeClientTests()
    {
        if (!NativeTestSupport.EnsureNativeAvailable() || OperatingSystem.IsWindows())
        {
            _skip = true;
            return;
        }

        _config = new BridgeConfiguration
        {
            Network = BitcoinNetwork.Regtest,
            VerifierPublicKeys = CreateVerifierKeys(1)
        };
        _client = new BridgeClient(_config);
    }

    [Fact]
    public void CreateClient_WithValidConfig_Succeeds()
    {
        if (_skip)
        {
            return;
        }

        Assert.NotNull(_client);
    }

    [Fact]
    public void CreateClient_WithoutVerifiers_Throws()
    {
        if (_skip)
        {
            return;
        }

        var badConfig = new BridgeConfiguration
        {
            Network = BitcoinNetwork.Testnet,
            VerifierPublicKeys = Array.Empty<byte[]>()
        };

        Assert.Throws<ArgumentException>(() => new BridgeClient(badConfig));
    }

    [Fact]
    public void DepositorContext_Create_WithWif_Succeeds()
    {
        if (_skip)
        {
            return;
        }

        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config!, wif);
        
        Assert.NotNull(depositor.PublicKey);
    }

    [Fact]
    public void DepositorContext_Create_WithHex_Succeeds()
    {
        if (_skip)
        {
            return;
        }

        var key = new Key();
        var hex = Convert.ToHexString(key.ToBytes());
        
        using var depositor = DepositorContext.Create(_config!, hex);
        
        Assert.NotNull(depositor.PublicKey);
    }

    [Fact]
    public void DepositorContext_GetAddress_ReturnsValidAddress()
    {
        if (_skip)
        {
            return;
        }

        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config!, wif);
        var address = depositor.GetAddress();
        
        Assert.NotNull(address);
        Assert.StartsWith("bcrt1", address.ToString()); // Regtest taproot address
    }

    [Fact]
    public async Task CreatePegInGraph_ReturnsGraph()
    {
        if (_skip)
        {
            return;
        }

        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config!, wif);
        var outpoint = new OutPoint(uint256.One, 0);
        var amount = Money.Satoshis(100_000);
        
        var pegIn = await _client!.CreatePegInAsync(depositor, outpoint, amount, "0x1234567890abcdef");
        
        Assert.NotNull(pegIn);
        Assert.NotEmpty(pegIn.Id);
        Assert.Equal("0x1234567890abcdef", pegIn.DepositorEvmAddress);
        Assert.Equal(amount, pegIn.DepositAmount);
        Assert.False(string.IsNullOrWhiteSpace(pegIn.RawJson));
    }

    [Fact]
    public async Task GetPegInStatus_ReturnsStatus()
    {
        if (_skip)
        {
            return;
        }

        var esploraUrl = Environment.GetEnvironmentVariable("PONZITECH_ESPLORA_URL");
        if (string.IsNullOrWhiteSpace(esploraUrl))
        {
            return;
        }

        _config!.EsploraUrl = esploraUrl;

        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config!, wif);
        var outpoint = new OutPoint(uint256.One, 0);
        var amount = Money.Satoshis(100_000);
        var pegIn = await _client!.CreatePegInAsync(depositor, outpoint, amount, "0x1234");
        
        var status = await _client.GetPegInStatusAsync(pegIn);
        
        Assert.IsType<PegInDepositorStatus>(status);
    }

    [Fact]
    public async Task SerializeDeserializePegInGraph_Works()
    {
        if (_skip)
        {
            return;
        }

        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config!, wif);
        var outpoint = new OutPoint(uint256.One, 0);
        var amount = Money.Satoshis(100_000);
        var pegIn = await _client!.CreatePegInAsync(depositor, outpoint, amount, "0x1234");

        var json = _client!.SerializePegInGraph(pegIn);
        Assert.NotEmpty(json);

        var deserialized = _client.DeserializePegInGraph(json);
        Assert.NotNull(deserialized);
        Assert.Equal(pegIn.Id, deserialized.Id);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private static byte[][] CreateVerifierKeys(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => new Key().PubKey.ToBytes())
            .ToArray();
    }
}
