using Xunit;
using PonziTech.BitVM.Core;

namespace PonziTech.BitVM.UnitTests;

public class WinternitzTests
{
    [Fact]
    public void GenerateSecret_Returns20Bytes()
    {
        NativeTestSupport.EnsureNativeAvailable();
        var secret = WinternitzSignatures.GenerateSecret();
        
        Assert.NotNull(secret);
        Assert.Equal(20, secret.Length);
    }

    [Fact]
    public void GetPublicKey_WithValidSecret_ReturnsPublicKey()
    {
        NativeTestSupport.EnsureNativeAvailable();
        var secret = WinternitzSignatures.GenerateSecret();
        var pubkey = WinternitzSignatures.GetPublicKey(secret, WinternitzSignatures.MessageSize.Size16);
        
        Assert.NotNull(pubkey);
        Assert.NotEmpty(pubkey);
        Assert.All(pubkey, entry => Assert.Equal(20, entry.Length));
    }

    [Fact]
    public void Sign_WithValidInputs_ReturnsSignature()
    {
        NativeTestSupport.EnsureNativeAvailable();
        var secret = WinternitzSignatures.GenerateSecret();
        var message = new byte[16];
        
        var sig = WinternitzSignatures.Sign(secret, message, WinternitzSignatures.MessageSize.Size16);
        
        Assert.NotNull(sig);
        Assert.NotEmpty(sig);
        Assert.All(sig, entry => Assert.Equal(21, entry.Length));
    }

    [Fact]
    public void Sign_WithWrongMessageSize_ThrowsArgumentException()
    {
        NativeTestSupport.EnsureNativeAvailable();
        var secret = WinternitzSignatures.GenerateSecret();
        var message = new byte[32]; // Wrong size for Size16
        
        Assert.Throws<ArgumentException>(() =>
            WinternitzSignatures.Sign(secret, message, WinternitzSignatures.MessageSize.Size16));
    }

    [Fact]
    public void GetChecksigScript_ReturnsScript()
    {
        NativeTestSupport.EnsureNativeAvailable();
        var secret = WinternitzSignatures.GenerateSecret();
        var pubkey = WinternitzSignatures.GetPublicKey(secret, WinternitzSignatures.MessageSize.Size16);
        var script = WinternitzSignatures.GetChecksigScript(pubkey, WinternitzSignatures.MessageSize.Size16);
        
        Assert.NotNull(script);
        Assert.NotEmpty(script);
    }
}
