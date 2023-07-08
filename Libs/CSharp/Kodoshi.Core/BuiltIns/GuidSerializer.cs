using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class GuidSerializer : ISerializer<Guid>
    {
        private const int _guidSize = 16;

        public async ValueTask SerializeAsync(Guid instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(_guidSize);
            if (!instance.TryWriteBytes(buffer))
            {
                pool.Return(buffer);
                throw new MiscException(new InvalidOperationException("Couldn't write guid to byte buffer"));
            }

            try
            {
                await pipeWriter.WriteAsync(buffer.AsMemory(0, _guidSize), ct).ConfigureAwait(false);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public async ValueTask<Guid> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await pipeReader.ReadAtLeastAsync(_guidSize, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (result.IsCanceled)
                throw new OperationCanceledException();
            if (result.IsCompleted && result.Buffer.Length < _guidSize)
                throw new StreamClosedException();
            var guid = Convert(result.Buffer);
            pipeReader.AdvanceTo(result.Buffer.GetPosition(_guidSize));
            return guid;
        }

        private static Guid Convert(ReadOnlySequence<byte> seq)
        {
            Span<byte> span = stackalloc byte[_guidSize];
            seq.Slice(0, _guidSize).CopyTo(span);
            return new Guid(span);
        }
    }
}
