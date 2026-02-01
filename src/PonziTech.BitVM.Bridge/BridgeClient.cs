using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.DataEncoders;
using PonziTech.BitVM.Core;
using PonziTech.BitVM.Native;

namespace PonziTech.BitVM.Bridge;

/// <summary>
/// Supported Bitcoin networks
/// </summary>
public enum BitcoinNetwork
{
    Mainnet,
    Testnet,
    Signet,
    Regtest
}

/// <summary>
/// Configuration for BitVM bridge operations
/// </summary>
public class BridgeConfiguration
{
    /// <summary>
    /// Bitcoin network to use
    /// </summary>
    public BitcoinNetwork Network { get; set; } = BitcoinNetwork.Testnet;
    
    /// <summary>
    /// Esplora API URL for blockchain queries
    /// </summary>
    public string? EsploraUrl { get; set; }
    
    /// <summary>
    /// Data store path for persisting graph data
    /// </summary>
    public string? DataStorePath { get; set; }
    
    /// <summary>
    /// Verifier public keys (required for all operations)
    /// </summary>
    public byte[][] VerifierPublicKeys { get; set; } = Array.Empty<byte[]>();
    
    /// <summary>
    /// Minimum number of verifiers required
    /// </summary>
    public int MinimumVerifiers { get; set; } = 1;
}

/// <summary>
/// Context for depositor operations
/// </summary>
public class DepositorContext : IDisposable
{
    private readonly BridgeConfiguration _config;
    private readonly Key _key;
    private bool _disposed;

    internal DepositorContext(BridgeConfiguration config, Key key)
    {
        _config = config;
        _key = key;
    }

    /// <summary>
    /// Creates a new depositor context
    /// </summary>
    /// <param name="config">Bridge configuration</param>
    /// <param name="secret">Secret key (WIF or hex)</param>
    public static DepositorContext Create(BridgeConfiguration config, string secret)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret is required", nameof(secret));
        }

        // Parse secret key
        Key key;
        try
        {
            // Try WIF first
            key = Key.Parse(secret, config.Network switch
            {
                BitcoinNetwork.Mainnet => Network.Main,
                BitcoinNetwork.Testnet => Network.TestNet,
                BitcoinNetwork.Signet => Network.TestNet,
                BitcoinNetwork.Regtest => Network.RegTest,
                _ => Network.TestNet
            });
        }
        catch
        {
            // Try hex
            var bytes = NBitcoin.DataEncoders.Encoders.Hex.DecodeData(secret);
            key = new Key(bytes);
        }

        return new DepositorContext(config, key);
    }

    /// <summary>
    /// Gets the public key
    /// </summary>
    public PubKey PublicKey => _key.PubKey;

    /// <summary>
    /// Gets the Bitcoin address
    /// </summary>
    public BitcoinAddress GetAddress(ScriptPubKeyType type = ScriptPubKeyType.Segwit)
    {
        var network = _config.Network switch
        {
            BitcoinNetwork.Mainnet => Network.Main,
            BitcoinNetwork.Testnet => Network.TestNet,
            BitcoinNetwork.Signet => Network.TestNet,
            BitcoinNetwork.Regtest => Network.RegTest,
            _ => Network.TestNet
        };

        return _key.PubKey.GetAddress(type, network);
    }

    internal byte[] GetSecretBytes() => _key.ToBytes();

    internal string GetSecretHex() => Encoders.Hex.EncodeData(_key.ToBytes()).ToLowerInvariant();

    public void Dispose()
    {
        if (!_disposed)
        {
            _key.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Peg-in transaction graph
/// </summary>
public class PegInGraph
{
    public string Id { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string DepositorEvmAddress { get; set; } = string.Empty;
    public byte[] DepositorPublicKey { get; set; } = Array.Empty<byte>();
    public byte[][] VerifierPublicKeys { get; set; } = Array.Empty<byte[]>();
    public uint256? DepositTxid { get; set; }
    public uint? DepositVout { get; set; }
    public Money? DepositAmount { get; set; }
    public string? RawJson { get; set; }
}

/// <summary>
/// Peg-in depositor status
/// </summary>
public enum PegInDepositorStatus
{
    Unknown,
    PegInDepositWait,
    PegInConfirmWait,
    PegInConfirmComplete,
    PegInRefundAvailable,
    PegInRefundComplete
}

/// <summary>
/// Main client for BitVM bridge operations
/// </summary>
public class BridgeClient : IDisposable
{
    private readonly BridgeConfiguration _config;
    private bool _disposed;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a new bridge client
    /// </summary>
    /// <param name="config">Bridge configuration</param>
    public BridgeClient(BridgeConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        if (config.VerifierPublicKeys.Length == 0)
        {
            throw new ArgumentException("At least one verifier public key is required", nameof(config));
        }

        if (config.MinimumVerifiers < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(config.MinimumVerifiers), "Minimum verifiers must be at least 1");
        }

        if (config.VerifierPublicKeys.Length < config.MinimumVerifiers)
        {
            throw new ArgumentException("Verifier public keys must satisfy MinimumVerifiers", nameof(config));
        }

        BitVmNativeRuntime.AddRef();
    }

    /// <summary>
    /// Creates a peg-in graph
    /// </summary>
    /// <param name="depositor">Depositor context</param>
    /// <param name="depositOutpoint">Deposit transaction outpoint</param>
    /// <param name="depositAmount">Deposit amount in satoshis</param>
    /// <param name="evmAddress">Destination EVM address (e.g., "0x1234...")</param>
    /// <returns>Peg-in graph</returns>
    public async Task<PegInGraph> CreatePegInAsync(
        DepositorContext depositor,
        OutPoint depositOutpoint,
        Money depositAmount,
        string evmAddress)
    {
        ThrowIfDisposed();

        if (depositor == null)
        {
            throw new ArgumentNullException(nameof(depositor));
        }

        if (string.IsNullOrWhiteSpace(evmAddress))
        {
            throw new ArgumentException("EVM address is required", nameof(evmAddress));
        }

        if (depositAmount <= Money.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(depositAmount), "Deposit amount must be positive");
        }

        var contextJson = CreateDepositorContextJson(depositor);
        var graphJson = CreatePegInGraphJson(contextJson, depositOutpoint, depositAmount, evmAddress);

        var graph = ParsePegInGraph(graphJson);
        graph.VerifierPublicKeys = _config.VerifierPublicKeys;
        graph.DepositTxid = depositOutpoint.Hash;
        graph.DepositVout = depositOutpoint.N;
        graph.DepositAmount = depositAmount;
        graph.RawJson = graphJson;

        return await Task.FromResult(graph);
    }

    /// <summary>
    /// Gets the peg-in status from the depositor's perspective
    /// </summary>
    /// <param name="graph">Peg-in graph</param>
    /// <returns>Current status</returns>
    public Task<PegInDepositorStatus> GetPegInStatusAsync(PegInGraph graph)
    {
        ThrowIfDisposed();

        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        if (string.IsNullOrWhiteSpace(graph.RawJson))
        {
            throw new ArgumentException("Peg-in graph is missing raw JSON data", nameof(graph));
        }

        var graphJsonBytes = FfiHelpers.GetNullTerminatedUtf8(graph.RawJson!);
        byte[]? esploraBytes = null;
        if (!string.IsNullOrWhiteSpace(_config.EsploraUrl))
        {
            esploraBytes = FfiHelpers.GetNullTerminatedUtf8(_config.EsploraUrl!);
        }

        PegInDepositorStatus status;
        unsafe
        {
            fixed (byte* graphPtr = graphJsonBytes)
            fixed (byte* esploraPtr = esploraBytes)
            {
                var result = BitVMNative.bridge_get_peg_in_depositor_status(
                    graphPtr,
                    esploraBytes == null ? null : esploraPtr);
                var jsonBytes = FfiHelpers.ReadBytes(result);
                var statusDto = JsonSerializer.Deserialize<PegInStatusDto>(jsonBytes, JsonOptions);
                if (statusDto == null || string.IsNullOrWhiteSpace(statusDto.Code))
                {
                    throw new FFIException("Failed to deserialize peg-in status");
                }

                status = ParseStatus(statusDto.Code!);
            }
        }

        return Task.FromResult(status);
    }

    /// <summary>
    /// Serializes a peg-in graph to JSON for storage or transmission
    /// </summary>
    /// <param name="graph">Peg-in graph</param>
    /// <returns>JSON string</returns>
    public string SerializePegInGraph(PegInGraph graph)
    {
        ThrowIfDisposed();

        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        if (string.IsNullOrWhiteSpace(graph.RawJson))
        {
            throw new ArgumentException("Peg-in graph is missing raw JSON data", nameof(graph));
        }

        var graphJsonBytes = FfiHelpers.GetNullTerminatedUtf8(graph.RawJson!);
        unsafe
        {
            fixed (byte* graphPtr = graphJsonBytes)
            {
                var result = BitVMNative.bridge_serialize_peg_in_graph(graphPtr);
                var jsonBytes = FfiHelpers.ReadBytes(result);
                return Encoding.UTF8.GetString(jsonBytes);
            }
        }
    }

    /// <summary>
    /// Deserializes a peg-in graph from JSON
    /// </summary>
    /// <param name="json">JSON string</param>
    /// <returns>Peg-in graph</returns>
    public PegInGraph DeserializePegInGraph(string json)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON is required", nameof(json));
        }

        var jsonBytes = FfiHelpers.GetNullTerminatedUtf8(json);
        unsafe
        {
            fixed (byte* jsonPtr = jsonBytes)
            {
                var result = BitVMNative.bridge_deserialize_peg_in_graph(jsonPtr);
                var normalizedBytes = FfiHelpers.ReadBytes(result);
                var normalizedJson = Encoding.UTF8.GetString(normalizedBytes);
                var graph = ParsePegInGraph(normalizedJson);
                graph.RawJson = normalizedJson;
                return graph;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BridgeClient));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            BitVmNativeRuntime.Release();
            _disposed = true;
        }
    }

    private string CreateDepositorContextJson(DepositorContext depositor)
    {
        var network = _config.Network.ToString().ToLowerInvariant();
        var secretHex = depositor.GetSecretHex();
        var verifierKeysJson = JsonSerializer.Serialize(ToHexStrings(_config.VerifierPublicKeys));

        var networkBytes = FfiHelpers.GetNullTerminatedUtf8(network);
        var secretBytes = FfiHelpers.GetNullTerminatedUtf8(secretHex);
        var verifierBytes = FfiHelpers.GetNullTerminatedUtf8(verifierKeysJson);

        unsafe
        {
            fixed (byte* networkPtr = networkBytes)
            fixed (byte* secretPtr = secretBytes)
            fixed (byte* verifierPtr = verifierBytes)
            {
                var result = BitVMNative.bridge_create_depositor_context(
                    networkPtr,
                    secretPtr,
                    verifierPtr);
                var jsonBytes = FfiHelpers.ReadBytes(result);
                return Encoding.UTF8.GetString(jsonBytes);
            }
        }
    }

    private string CreatePegInGraphJson(
        string contextJson,
        OutPoint depositOutpoint,
        Money depositAmount,
        string evmAddress)
    {
        var contextBytes = FfiHelpers.GetNullTerminatedUtf8(contextJson);
        var txidBytes = FfiHelpers.GetNullTerminatedUtf8(depositOutpoint.Hash.ToString());
        var evmBytes = FfiHelpers.GetNullTerminatedUtf8(evmAddress);

        unsafe
        {
            fixed (byte* contextPtr = contextBytes)
            fixed (byte* txidPtr = txidBytes)
            fixed (byte* evmPtr = evmBytes)
            {
                var result = BitVMNative.bridge_create_peg_in_graph(
                    contextPtr,
                    txidPtr,
                    depositOutpoint.N,
                    (ulong)depositAmount.Satoshi,
                    evmPtr);
                var jsonBytes = FfiHelpers.ReadBytes(result);
                return Encoding.UTF8.GetString(jsonBytes);
            }
        }
    }

    private static PegInGraph ParsePegInGraph(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var graph = new PegInGraph
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            Network = root.TryGetProperty("network", out var network)
                ? network.GetString() ?? string.Empty
                : string.Empty,
            DepositorEvmAddress = root.TryGetProperty("depositor_evm_address", out var evm)
                ? evm.GetString() ?? string.Empty
                : string.Empty,
            DepositorPublicKey = TryParseHexBytes(root, "depositor_public_key"),
            VerifierPublicKeys = TryParseHexArray(root, "n_of_n_public_keys")
        };

        return graph;
    }

    private static byte[] TryParseHexBytes(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return Array.Empty<byte>();
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return Array.Empty<byte>();
        }

        var hex = element.GetString();
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Array.Empty<byte>();
        }

        try
        {
            return Encoders.Hex.DecodeData(hex);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static byte[][] TryParseHexArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return Array.Empty<byte[]>();
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<byte[]>();
        }

        var list = new List<byte[]>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var hex = item.GetString();
            if (string.IsNullOrWhiteSpace(hex))
            {
                continue;
            }

            try
            {
                list.Add(Encoders.Hex.DecodeData(hex));
            }
            catch
            {
                // Ignore malformed entries
            }
        }

        return list.ToArray();
    }

    private static PegInDepositorStatus ParseStatus(string code)
    {
        return code switch
        {
            "PegInDepositWait" => PegInDepositorStatus.PegInDepositWait,
            "PegInConfirmWait" => PegInDepositorStatus.PegInConfirmWait,
            "PegInConfirmComplete" => PegInDepositorStatus.PegInConfirmComplete,
            "PegInRefundAvailable" => PegInDepositorStatus.PegInRefundAvailable,
            "PegInRefundComplete" => PegInDepositorStatus.PegInRefundComplete,
            _ => PegInDepositorStatus.Unknown
        };
    }

    private static string[] ToHexStrings(byte[][] entries)
    {
        var result = new string[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            result[i] = Encoders.Hex.EncodeData(entries[i]).ToLowerInvariant();
        }
        return result;
    }

    private sealed class PegInStatusDto
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? GraphId { get; set; }
    }
}
