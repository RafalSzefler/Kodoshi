namespace Kodoshi.Core.Exceptions
{
    public abstract class BaseException : System.Exception
    {
        protected BaseException(string? message = null, System.Exception? inner = null) : base(message, inner)
        { }
    }

    public sealed class MiscException : BaseException
    {
        public MiscException(System.Exception? inner) : base(null, inner)
        { }
    }

    public sealed class InvalidBoolValueException : BaseException
    {
        public InvalidBoolValueException(string? message = null) : base(message)
        { }
    }

    public sealed class InvalidTagValueException : BaseException
    {
        public InvalidTagValueException(string? message = null) : base(message)
        { }
    }

    public sealed class NumberOutOfRangeException : BaseException
    {
        public NumberOutOfRangeException(string? message = null) : base(message)
        { }
    }

    public sealed class StreamClosedException : BaseException
    {
        public StreamClosedException(string? message = null) : base(message)
        { }
    }
}
