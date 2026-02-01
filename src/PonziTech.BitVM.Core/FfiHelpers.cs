using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using PonziTech.BitVM.Native;

namespace PonziTech.BitVM.Core;

internal static class BitVmNativeRuntime
{
    private static int _refCount;

    internal static void AddRef()
    {
        if (Interlocked.Increment(ref _refCount) != 1)
        {
            return;
        }

        var result = BitVMNative.bitvm_init();
        if (result != 0)
        {
            Interlocked.Decrement(ref _refCount);
            throw new InvalidOperationException("Failed to initialize BitVM FFI");
        }
    }

    internal static void Release()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
        {
            BitVMNative.bitvm_cleanup();
        }
    }
}

internal static class FfiHelpers
{
    internal static byte[] ReadBytes(BitVMNative.FfiResult result)
    {
        try
        {
            if (!result.success)
            {
                var error = ReadUtf8(result.error_message);
                throw new FFIException(error ?? "FFI operation failed");
            }

            if (result.data == null || result.data_len == 0)
            {
                return Array.Empty<byte>();
            }

            var len = checked((int)result.data_len);
            var bytes = new byte[len];
            Marshal.Copy((IntPtr)result.data, bytes, 0, len);
            return bytes;
        }
        finally
        {
            BitVMNative.bitvm_free_result(result);
        }
    }

    internal static unsafe string? ReadUtf8(byte* ptr)
    {
        if (ptr == null)
        {
            return null;
        }

        var length = 0;
        var cursor = ptr;
        while (*cursor != 0)
        {
            length++;
            cursor++;
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var bytes = new byte[length];
        Marshal.Copy((IntPtr)ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    internal static unsafe string? ReadUtf8AndFree(byte* ptr)
    {
        var value = ReadUtf8(ptr);
        if (ptr != null)
        {
            BitVMNative.bitvm_free_string(ptr);
        }
        return value;
    }

    internal static byte[] SerializeByteMatrix(byte[][] matrix)
    {
        if (matrix == null)
        {
            throw new ArgumentNullException(nameof(matrix));
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();
        foreach (var row in matrix)
        {
            writer.WriteStartArray();
            if (row != null)
            {
                foreach (var value in row)
                {
                    writer.WriteNumberValue(value);
                }
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.Flush();

        return stream.ToArray();
    }

    internal static byte[][] DeserializeByteMatrix(byte[] json)
    {
        if (json == null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FFIException("Expected JSON array for byte matrix");
        }

        var rows = new byte[doc.RootElement.GetArrayLength()][];
        var rowIndex = 0;
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array)
            {
                throw new FFIException("Expected JSON array entries for byte matrix rows");
            }

            var bytes = new byte[row.GetArrayLength()];
            var index = 0;
            foreach (var element in row.EnumerateArray())
            {
                if (!element.TryGetByte(out var value))
                {
                    throw new FFIException("Byte matrix entries must be numbers between 0 and 255");
                }
                bytes[index++] = value;
            }

            rows[rowIndex++] = bytes;
        }

        return rows;
    }

    internal static byte[] WithNullTerminator(byte[] utf8Bytes)
    {
        var output = new byte[utf8Bytes.Length + 1];
        Buffer.BlockCopy(utf8Bytes, 0, output, 0, utf8Bytes.Length);
        return output;
    }

    internal static byte[] GetNullTerminatedUtf8(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WithNullTerminator(Encoding.UTF8.GetBytes(value));
    }
}
