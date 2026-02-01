using Xunit;
using NBitcoin;
using PonziTech.BitVM.Bridge;

namespace PonziTech.BitVM.IntegrationTests;

public class BridgeClientTests : IDisposable
{
    private readonly BridgeClient _client;
    private readonly BridgeConfiguration _config;

    public BridgeClientTests()
    {
        _config = new BridgeConfiguration
        {
            Network = BitcoinNetwork.Regtest,
            VerifierPublicKeys = new[] { RandomUtils.GetBytes(33) }
        };
        _client = new BridgeClient(_config);
    }

    [Fact]
    public void CreateClient_WithValidConfig_Succeeds()
    {
        Assert.NotNull(_client);
    }

    [Fact]
    public void CreateClient_WithoutVerifiers_Throws()
    {
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
        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config, wif);
        
        Assert.NotNull(depositor.PublicKey);
    }

    [Fact]
    public void DepositorContext_Create_WithHex_Succeeds()
    {
        var key = new Key();
        var hex = Convert.ToHexString(key.ToBytes());
        
        using var depositor = DepositorContext.Create(_config, hex);
        
        Assert.NotNull(depositor.PublicKey);
    }

    [Fact]
    public void DepositorContext_GetAddress_ReturnsValidAddress()
    {
        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config, wif);
        var address = depositor.GetAddress();
        
        Assert.NotNull(address);
        Assert.StartsWith("bcrt1", address.ToString()); // Regtest taproot address
    }

    [Fact]
    public async Task CreatePegInGraph_ReturnsGraph()
    {
        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config, wif);
        var outpoint = new OutPoint(uint256.One, 0);
        
        var pegIn = await _client.CreatePegInAsync(depositor, outpoint, "0x1234567890abcdef");
        
        Assert.NotNull(pegIn);
        Assert.NotEmpty(pegIn.Id);
        Assert.Equal("0x1234567890abcdef", pegIn.DepositorEvmAddress);
    }

    [Fact]
    public async Task GetPegInStatus_ReturnsStatus()
    {
        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config, wif);
        var outpoint = new OutPoint(uint256.One, 0);
        var pegIn = await _client.CreatePegInAsync(depositor, outpoint, "0x1234");
        
        var status = await _client.GetPegInStatusAsync(pegIn);
        
        Assert.IsType<PegInDepositorStatus>(status);
    }

    [Fact]
    public async Task SerializeDeserializePegInGraph_Works()
    {
        var key = new Key();
        var wif = key.GetWif(Network.RegTest).ToWif();
        
        using var depositor = DepositorContext.Create(_config, wif);
        var outpoint = new OutPoint(uint256.One, 0);
        var pegIn = await _client.CreatePegInAsync(depositor, outpoint, "0x1234");
        
        var json = _client.SerializePegInGraph(pegIn);
        Assert.NotEmpty(json);
        
        var deserialized = _client.DeserializePegInGraph(json);
        Assert.NotNull(deserialized);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
