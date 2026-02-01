using System;
using PonziTech.BitVM.Core;

namespace PonziTech.BitVM.IntegrationTests;

internal static class NativeTestSupport
{
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
