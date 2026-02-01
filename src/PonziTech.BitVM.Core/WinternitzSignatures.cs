using System;
using PonziTech.BitVM.Native;

namespace PonziTech.BitVM.Core;

/// <summary>
/// Winternitz signature operations
/// </summary>
public class WinternitzSignatures
{
    /// <summary>
    /// Supported message sizes for Winternitz signatures
    /// </summary>
    public enum MessageSize
    {
        /// <summary>4 byte messages</summary>
        Size4 = 4,
        /// <summary>16 byte messages (HASH_LEN)</summary>
        Size16 = 16,
        /// <summary>32 byte messages</summary>
        Size32 = 32,
        /// <summary>64 byte messages</summary>
        Size64 = 64,
        /// <summary>80 byte messages</summary>
        Size80 = 80
    }

    /// <summary>
    /// Generates a new Winternitz secret key
    /// </summary>
    /// <returns>20-byte secret key</returns>
    public static byte[] GenerateSecret()
    {
        return WithNative(() =>
        {
            var result = BitVMNative.crypto_generate_winternitz_secret();
            var secret = FfiHelpers.ReadBytes(result);
            if (secret.Length != 20)
            {
                throw new FFIException($"Unexpected secret length: {secret.Length}");
            }
            return secret;
        });
    }

    /// <summary>
    /// Derives a public key from a secret key
    /// </summary>
    /// <param name="secret">20-byte secret key</param>
    /// <param name="messageSize">Expected message size</param>
    /// <returns>Public key bytes</returns>
    public static byte[][] GetPublicKey(ReadOnlySpan<byte> secret, MessageSize messageSize)
    {
        if (secret.Length != 20)
        {
            throw new ArgumentException("Secret must be 20 bytes", nameof(secret));
        }

        var secretBytes = secret.ToArray();
        return WithNative(() =>
        {
            unsafe
            {
                fixed (byte* secretPtr = secretBytes)
                {
                    var result = BitVMNative.crypto_winternitz_pubkey_from_secret(
                        secretPtr,
                        (nuint)secretBytes.Length,
                        (uint)messageSize);
                    var jsonBytes = FfiHelpers.ReadBytes(result);
                    return FfiHelpers.DeserializeByteMatrix(jsonBytes);
                }
            }
        });
    }

    /// <summary>
    /// Signs a message using Winternitz signature scheme
    /// </summary>
    /// <param name="secret">20-byte secret key</param>
    /// <param name="message">Message to sign (must match messageSize)</param>
    /// <param name="messageSize">Expected message size</param>
    /// <returns>Signature bytes</returns>
    public static byte[][] Sign(ReadOnlySpan<byte> secret, ReadOnlySpan<byte> message, MessageSize messageSize)
    {
        if (secret.Length != 20)
        {
            throw new ArgumentException("Secret must be 20 bytes", nameof(secret));
        }

        if (message.Length != (int)messageSize)
        {
            throw new ArgumentException($"Message must be {(int)messageSize} bytes", nameof(message));
        }

        var secretBytes = secret.ToArray();
        var messageBytes = message.ToArray();
        return WithNative(() =>
        {
            unsafe
            {
                fixed (byte* secretPtr = secretBytes)
                fixed (byte* messagePtr = messageBytes)
                {
                    var result = BitVMNative.crypto_winternitz_sign(
                        secretPtr,
                        (nuint)secretBytes.Length,
                        messagePtr,
                        (nuint)messageBytes.Length,
                        (uint)messageSize);
                    var jsonBytes = FfiHelpers.ReadBytes(result);
                    return FfiHelpers.DeserializeByteMatrix(jsonBytes);
                }
            }
        });
    }

    /// <summary>
    /// Generates a script to verify a Winternitz signature
    /// </summary>
    /// <param name="publicKey">Public key</param>
    /// <param name="messageSize">Expected message size</param>
    /// <param name="compact">Use compact signature variant</param>
    /// <returns>Verification script bytecode</returns>
    public static byte[] GetChecksigScript(byte[][] publicKey, MessageSize messageSize, bool compact = false)
    {
        if (publicKey == null || publicKey.Length == 0)
        {
            throw new ArgumentException("Public key is required", nameof(publicKey));
        }

        foreach (var entry in publicKey)
        {
            if (entry == null || entry.Length != 20)
            {
                throw new ArgumentException("Each public key entry must be 20 bytes", nameof(publicKey));
            }
        }

        return WithNative(() =>
        {
            var pubkeyJson = FfiHelpers.SerializeByteMatrix(publicKey);
            var pubkeyJsonNullTerminated = FfiHelpers.WithNullTerminator(pubkeyJson);

            unsafe
            {
                fixed (byte* pubkeyPtr = pubkeyJsonNullTerminated)
                {
                    var result = BitVMNative.crypto_winternitz_checksig_script(
                        pubkeyPtr,
                        (uint)messageSize,
                        compact);
                    return FfiHelpers.ReadBytes(result);
                }
            }
        });
    }

    private static T WithNative<T>(Func<T> action)
    {
        BitVmNativeRuntime.AddRef();
        try
        {
            return action();
        }
        finally
        {
            BitVmNativeRuntime.Release();
        }
    }
}
