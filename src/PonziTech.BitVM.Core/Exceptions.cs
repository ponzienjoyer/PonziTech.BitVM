using System;

namespace PonziTech.BitVM.Core;

/// <summary>
/// Exception thrown when a BitVM operation fails
/// </summary>
public class BitVMException : Exception
{
    public BitVMException(string message) : base(message) { }
    public BitVMException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when script execution fails
/// </summary>
public class ScriptExecutionException : BitVMException
{
    public ScriptExecutionException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when FFI operations fail
/// </summary>
public class FFIException : BitVMException
{
    public FFIException(string message) : base(message) { }
}
