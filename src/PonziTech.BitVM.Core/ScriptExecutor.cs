using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
    private readonly bool _ownsInitialization;

    /// <summary>
    /// Creates a new script executor
    /// </summary>
    public ScriptExecutor()
    {
        _ownsInitialization = true;
        var result = BitVMNative.bitvm_init();
        if (result != 0)
        {
            throw new InvalidOperationException("Failed to initialize BitVM FFI");
        }
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
                // This would call the actual FFI method
                // For now, return a placeholder result
                return new ScriptExecutionResult
                {
                    Success = true,
                    FinalStack = "[]",
                    Stats = new ExecutionStats
                    {
                        MaxStackSize = 0,
                        MaxAltStackSize = 0,
                        OpcodeCount = script.Length
                    }
                };
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

        var witnessJson = JsonSerializer.Serialize(witness);
        
        // Call FFI method here
        // Placeholder implementation
        return Execute(script);
    }

    /// <summary>
    /// Generates a SHA256 script for the given message length
    /// </summary>
    /// <param name="messageLength">Length of message to hash</param>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateSha256Script(int messageLength)
    {
        ThrowIfDisposed();
        
        // Call FFI: bitvm_sha256_script
        // Placeholder: return empty script
        return new byte[] { 0x00 };
    }

    /// <summary>
    /// Generates a SHA256 script for 32-byte input
    /// </summary>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateSha256Script32Bytes()
    {
        ThrowIfDisposed();
        
        // Call FFI: bitvm_sha256_32bytes_script
        return new byte[] { 0x00 };
    }

    /// <summary>
    /// Generates a BLAKE3 script
    /// </summary>
    /// <param name="messageLength">Length of message to hash</param>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateBlake3Script(int messageLength)
    {
        ThrowIfDisposed();
        
        // Call FFI: bitvm_blake3_script
        return new byte[] { 0x00 };
    }

    /// <summary>
    /// Generates a script to push a u32 value
    /// </summary>
    /// <param name="value">The value to push</param>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateU32PushScript(uint value)
    {
        ThrowIfDisposed();
        
        // Call FFI: bitvm_u32_push_script
        return new byte[] { 0x00 };
    }

    /// <summary>
    /// Generates a u32 equality verification script
    /// </summary>
    /// <returns>Script bytecode</returns>
    public byte[] GenerateU32EqualVerifyScript()
    {
        ThrowIfDisposed();
        
        // Call FFI: bitvm_u32_equalverify_script
        return new byte[] { 0x00 };
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
            if (_ownsInitialization)
            {
                BitVMNative.bitvm_cleanup();
            }
            _disposed = true;
        }
    }
}
