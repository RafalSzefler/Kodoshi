using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class ByteArraySerializer : ISerializer<ReadOnlyArray<byte>>
    {
        private readonly ISerializer<uint> _uintSerializer;
        public ByteArraySerializer(ISerializer<uint> uintSerializer)
        {
            _uintSerializer = uintSerializer;
        }

        
        public async ValueTask SerializeAsync(ReadOnlyArray<byte> instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            await _uintSerializer.SerializeAsync((uint)instance._length, pipeWriter, ct).ConfigureAwait(false);
            await pipeWriter.WriteAsync(instance._buffer.AsMemory(instance._startIdx, instance._length), ct).ConfigureAwait(false);
        }

        public async ValueTask<ReadOnlyArray<byte>> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var size = await _uintSerializer.DeserializeAsync(pipeReader, ct).ConfigureAwait(false);
            if (size == 0) return ReadOnlyArray.Empty<byte>();
            var result = await pipeReader.ReadAtLeastAsync((int)size, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (result.IsCanceled)
                throw new OperationCanceledException();
            if (result.IsCompleted && result.Buffer.Length < size)
                throw new StreamClosedException();
            var array = result.Buffer.Slice(0, size).ToArray();
            pipeReader.AdvanceTo(result.Buffer.GetPosition(array.Length));
            return ReadOnlyArray.Move(array);
        }
    }
}
