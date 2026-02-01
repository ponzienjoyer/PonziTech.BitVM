using Xunit;
using PonziTech.BitVM.Core;

namespace PonziTech.BitVM.UnitTests;

public class WinternitzTests
{
    [Fact]
    public void GenerateSecret_Returns32Bytes()
    {
        var secret = WinternitzSignatures.GenerateSecret();
        
        Assert.NotNull(secret);
        Assert.Equal(32, secret.Length);
    }

    [Fact]
    public void GetPublicKey_WithValidSecret_ReturnsPublicKey()
    {
        var secret = WinternitzSignatures.GenerateSecret();
        var pubkey = WinternitzSignatures.GetPublicKey(secret, WinternitzSignatures.MessageSize.Size16);
        
        Assert.NotNull(pubkey);
    }

    [Fact]
    public void Sign_WithValidInputs_ReturnsSignature()
    {
        var secret = WinternitzSignatures.GenerateSecret();
        var message = new byte[16];
        
        var sig = WinternitzSignatures.Sign(secret, message, WinternitzSignatures.MessageSize.Size16);
        
        Assert.NotNull(sig);
    }

    [Fact]
    public void Sign_WithWrongMessageSize_ThrowsArgumentException()
    {
        var secret = WinternitzSignatures.GenerateSecret();
        var message = new byte[32]; // Wrong size for Size16
        
        Assert.Throws<ArgumentException>(() =>
            WinternitzSignatures.Sign(secret, message, WinternitzSignatures.MessageSize.Size16));
    }

    [Fact]
    public void GetChecksigScript_ReturnsScript()
    {
        var pubkey = new byte[32];
        var script = WinternitzSignatures.GetChecksigScript(pubkey, WinternitzSignatures.MessageSize.Size16);
        
        Assert.NotNull(script);
    }
}
