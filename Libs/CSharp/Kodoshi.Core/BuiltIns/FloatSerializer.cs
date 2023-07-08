using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class FloatSerializer : ISerializer<float>
    {
        private const int _floatSize = 4;
        public async ValueTask SerializeAsync(float instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            var unsignedValue = (uint)BitConverter.SingleToInt32Bits(instance);
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(_floatSize);
            buffer[0] = (byte)(unsignedValue & 0xff);
            unsignedValue >>= 8;
            buffer[1] = (byte)(unsignedValue & 0xff);
            unsignedValue >>= 8;
            buffer[2] = (byte)(unsignedValue & 0xff);
            unsignedValue >>= 8;
            buffer[3] = (byte)(unsignedValue & 0xff);
            try
            {
                await pipeWriter.WriteAsync(buffer.AsMemory(0, _floatSize), ct).ConfigureAwait(false);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public async ValueTask<float> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var readResult = await pipeReader.ReadAtLeastAsync(_floatSize, ct).ConfigureAwait(false);
            if (readResult.IsCanceled)
                throw new OperationCanceledException();
            if (readResult.IsCompleted && readResult.Buffer.Length < _floatSize)
                throw new StreamClosedException();
            var buffer = readResult.Buffer;
            int readValue(int offset)
            {
                return buffer.Slice(buffer.GetPosition(offset)).First.Span[0];
            }

            var result = readValue(0) + (readValue(1) << 8) + (readValue(2) << 16) + (readValue(3) << 24);
            pipeReader.AdvanceTo(readResult.Buffer.GetPosition(_floatSize));
            return BitConverter.Int32BitsToSingle(result);
        }
    }
}
