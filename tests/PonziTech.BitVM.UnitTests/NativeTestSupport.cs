using System;
using PonziTech.BitVM.Core;
using Xunit.Sdk;

namespace PonziTech.BitVM.UnitTests;

internal static class NativeTestSupport
{
    internal static ScriptExecutor CreateExecutorOrSkip()
    {
        EnsureNativeAvailable();
        return new ScriptExecutor();
    }

    internal static void EnsureNativeAvailable()
    {
        try
        {
            using var executor = new ScriptExecutor();
        }
        catch (DllNotFoundException)
        {
            throw new SkipException("Native BitVM FFI library not found. Build the ffi project first.");
        }
        catch (BadImageFormatException)
        {
            throw new SkipException("Native BitVM FFI library is incompatible with the current runtime.");
        }
        catch (EntryPointNotFoundException)
        {
            throw new SkipException("Native BitVM FFI library is missing required exports.");
        }
    }
}
