using System;

namespace Kodoshi.Core
{
    public interface ISerializerCollection : IDisposable
    {
        ISerializer<T> GetSerializer<T>() where T : IEquatable<T>;
    }
}
