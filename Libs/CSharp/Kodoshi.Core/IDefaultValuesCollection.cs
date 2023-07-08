using System;

namespace Kodoshi.Core
{
    public interface IDefaultValuesCollection : IDisposable
    {
        T GetDefaultValue<T>() where T : IEquatable<T>;
    }
}
