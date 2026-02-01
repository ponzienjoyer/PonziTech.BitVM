using System;
using System.Text.Json;
using System.Threading.Tasks;
using PonziTech.BitVM.Native;

namespace PonziTech.BitVM.Core;

/// <summary>
/// Information about script execution
/// </summary>
public class ScriptExecutionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? FinalStack { get; set; }
    public string? RemainingScript { get; set; }
    public string? LastOpcode { get; set; }
    public ExecutionStats? Stats { get; set; }
}

/// <summary>
/// Execution statistics
/// </summary>
public class ExecutionStats
{
    public int MaxStackSize { get; set; }
    public int MaxAltStackSize { get; set; }
    public int OpcodeCount { get; set; }
}

/// <summary>
/// Executes Bitcoin scripts using BitVM
/// </summary>
public class ScriptExecutor : IDisposable
{
    private bool _disposed;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a new script executor
    /// </summary>
    public ScriptExecutor()
    {
        BitVmNativeRuntime.AddRef();
    }

    /// <summary>
    /// Executes a Bitcoin script
    /// </summary>
    /// <param name="script">The script bytecode to execute</param>
    /// <returns>Execution result</returns>
    public ScriptExecutionResult Execute(ReadOnlySpan<byte> script)
    {
        ThrowIfDisposed();

        unsafe
        {
            fixed (byte* scriptPtr = script)
            {
                var result = BitVMNative.bitvm_execute_script(scriptPtr, (nuint)script.Length);
                var jsonBytes = FfiHelpers.ReadBytes(result);
                var executionResult = JsonSerializer.Deserialize<ScriptExecutionResult>(jsonBytes, JsonOptions);
                if (executionResult == null)
                {
                    throw new FFIException("Failed to deserialize script execution result");
                }
                return executionResult;
            }
        }
    }

    /// <summary>
    /// Executes a script with witness inputs
    /// </summary>
    /// <param name="script">The script bytecode</param>
    /// <param name="witness">Witness data as array of byte arrays</param>
    /// <returns>Execution result</returns>
    public ScriptExecutionResult ExecuteWithWitness(ReadOnlySpan<byte> script, byte[][] witness)
    {
        ThrowIfDisposed();

        if (witness == null)
        {
            throw new ArgumentNullException(nameof(witness));
        }

        var witnessJson = FfiHelpers.SerializeByteMatrix(witness);
        var witnessJsonNullTerminated = FfiHelpers.WithNullTerminator(witnessJson);

        unsafe
        {
            fixed (byte* scriptPtr = script)
            fixed (byte* witnessPtr = witnessJsonNullTerminated)
            {
                var result = BitVMNative.bitvm_execute_script_with_witness(
                    scriptPtr,
                    (nuint)script.Length,
                    witnessPtr);
                var jsonBytes = FfiHelpers.ReadBytes(result);
                var executionResult = JsonSerializer.Deserialize<ScriptExecutionResult>(jsonBytes, JsonOptions);
                if (executionResult == null)
                {
                    throw new FFIException("Failed to deserialize script execution result");
                }
                return executionResult;
            }
        }
    }

    /// <summary>
    /// Executes a Bitcoin script asynchronously
    /// </summary>
    /// <param name="script">The script bytecode to execute</param>
    /// <returns>Execution result</returns>
    public Task<ScriptExecutionResult> ExecuteAsync(ReadOnlyMemory<byte> script)
    {
        return Task.FromResult(Execute(script.Span));
    }

    /// <summary>
    /// Executes a script with witness inputs asynchronously
    /// </summary>
    /// <param name="script">The script bytecode</param>
    /// <param name="witness">Witness data as array of byte arrays</param>
    /// <returns>Execution result</returns>
    public Task<ScriptExecutionResult> ExecuteWithWitnessAsync(ReadOnlyMemory<byte> script, byte[][] witness)
    {
        return Task.FromResult(ExecuteWithWitness(script.Span, witness));
    }

    /// <summary>
    /// Generates a SHA256 script for the given message length
    /// </summary>
    /// <param name="messageLength">Length of message to hash</param>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateSha256Script(int messageLength)
    {
        ThrowIfDisposed();

        if (messageLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(messageLength), "Message length must be non-negative");
        }

        var result = BitVMNative.bitvm_sha256_script((nuint)messageLength);
        return FfiHelpers.ReadBytes(result);
    }

    /// <summary>
    /// Generates a SHA256 script for 32-byte input
    /// </summary>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateSha256Script32Bytes()
    {
        ThrowIfDisposed();

        var result = BitVMNative.bitvm_sha256_32bytes_script();
        return FfiHelpers.ReadBytes(result);
    }

    /// <summary>
    /// Generates a BLAKE3 script
    /// </summary>
    /// <param name="messageLength">Length of message to hash</param>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateBlake3Script(int messageLength)
    {
        ThrowIfDisposed();

        if (messageLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(messageLength), "Message length must be non-negative");
        }

        var result = BitVMNative.bitvm_blake3_script((nuint)messageLength);
        return FfiHelpers.ReadBytes(result);
    }

    /// <summary>
    /// Generates a script to push a u32 value
    /// </summary>
    /// <param name="value">The value to push</param>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateU32PushScript(uint value)
    {
        ThrowIfDisposed();

        var result = BitVMNative.bitvm_u32_push_script(value);
        return FfiHelpers.ReadBytes(result);
    }

    /// <summary>
    /// Generates a u32 equality verification script
    /// </summary>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateU32EqualVerifyScript()
    {
        ThrowIfDisposed();

        var result = BitVMNative.bitvm_u32_equalverify_script();
        return FfiHelpers.ReadBytes(result);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ScriptExecutor));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            BitVmNativeRuntime.Release();
            _disposed = true;
        }
    }
}
