using Xunit;

namespace PonziTech.BitVM.UnitTests;

public class ScriptExecutorTests
{
    [Fact]
    public void Execute_OpTrue_ReturnsSuccess()
    {
        using var executor = NativeTestSupport.CreateExecutorOrSkip();
        var script = new byte[] { 0x51 }; // OP_TRUE
        var result = executor.Execute(script);
        
        Assert.True(result.Success);
    }

    [Fact]
    public void Execute_WithWitness_ReturnsSuccess()
    {
        using var executor = NativeTestSupport.CreateExecutorOrSkip();
        var script = new byte[0];
        var witness = new byte[][] { new byte[] { 0x01 } };
        
        var result = executor.ExecuteWithWitness(script, witness);
        
        Assert.True(result.Success);
    }

    [Fact]
    public void GenerateSha256Script_ReturnsNonEmpty()
    {
        using var executor = NativeTestSupport.CreateExecutorOrSkip();
        var script = executor.GenerateSha256Script(32);
        
        Assert.NotNull(script);
        Assert.NotEmpty(script);
    }

    [Fact]
    public void GenerateU32PushScript_ReturnsNonEmpty()
    {
        using var executor = NativeTestSupport.CreateExecutorOrSkip();
        var script = executor.GenerateU32PushScript(42);
        
        Assert.NotNull(script);
        Assert.NotEmpty(script);
    }
}
