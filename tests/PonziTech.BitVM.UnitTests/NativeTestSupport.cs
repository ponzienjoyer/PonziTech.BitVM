using System;
using PonziTech.BitVM.Core;

namespace PonziTech.BitVM.UnitTests;

internal static class NativeTestSupport
{
    internal static ScriptExecutor? CreateExecutorOrSkip()
    {
        if (!EnsureNativeAvailable())
        {
            return null;
        }

        return new ScriptExecutor();
    }

    internal static bool EnsureNativeAvailable()
    {
        try
        {
            using var executor = new ScriptExecutor();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
