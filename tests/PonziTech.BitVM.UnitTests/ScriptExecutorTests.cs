using Xunit;
using PonziTech.BitVM.Core;

namespace PonziTech.BitVM.UnitTests;

public class ScriptExecutorTests : IDisposable
{
    private readonly ScriptExecutor _executor;

    public ScriptExecutorTests()
    {
        _executor = new ScriptExecutor();
    }

    [Fact]
    public void Execute_EmptyScript_ReturnsSuccess()
    {
        var script = new byte[0];
        var result = _executor.Execute(script);
        
        Assert.True(result.Success);
    }

    [Fact]
    public void Execute_WithWitness_ReturnsResult()
    {
        var script = new byte[] { 0x51 }; // OP_TRUE
        var witness = new byte[][] { new byte[] { 0x01 } };
        
        var result = _executor.ExecuteWithWitness(script, witness);
        
        Assert.NotNull(result);
    }

    [Fact]
    public void GenerateSha256Script_ReturnsNonEmpty()
    {
        var script = _executor.GenerateSha256Script(32);
        
        Assert.NotNull(script);
        Assert.NotEmpty(script);
    }

    [Fact]
    public void GenerateU32PushScript_ReturnsNonEmpty()
    {
        var script = _executor.GenerateU32PushScript(42);
        
        Assert.NotNull(script);
        Assert.NotEmpty(script);
    }

    public void Dispose()
    {
        _executor.Dispose();
    }
}
