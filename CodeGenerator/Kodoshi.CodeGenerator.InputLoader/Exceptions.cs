using System;

namespace Kodoshi.CodeGenerator.InputLoader;

public abstract class BaseException : Exception
{
    protected BaseException(string? message = null, Exception? innerException = null)
        : base(message, innerException)
    { }
}

public sealed class MiscException : BaseException
{
    public MiscException(string message) : base(message)
    { }
}

public sealed class ParsingException : BaseException
{
    public ParsingException(string message) : base(message)
    { }
}
