using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.Core
{
    public interface ISerializer<T> where T : IEquatable<T>
    {
        ValueTask SerializeAsync(T instance, PipeWriter pipeWriter, CancellationToken ct);
        ValueTask<T> DeserializeAsync(PipeReader pipeReader, CancellationToken ct);
    }
}