using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class DoubleSerializer : ISerializer<double>
    {
        private const int _doubleSize = 8;
        public async ValueTask SerializeAsync(double instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            var unsignedValue = (ulong)BitConverter.DoubleToInt64Bits(instance);
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(_doubleSize);
            for (var i = 0; i < _doubleSize; i++)
            {
                buffer[i] = (byte)(unsignedValue & 0xff);
                unsignedValue >>= 8;
            }

            try
            {
                await pipeWriter.WriteAsync(buffer.AsMemory(0, _doubleSize), ct).ConfigureAwait(false);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public async ValueTask<double> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var readResult = await pipeReader.ReadAtLeastAsync(_doubleSize, ct).ConfigureAwait(false);
            if (readResult.IsCanceled)
                throw new OperationCanceledException();
            if (readResult.IsCompleted && readResult.Buffer.Length < _doubleSize)
                throw new StreamClosedException();
            var buffer = readResult.Buffer;
            long readValue(int offset)
            {
                return buffer.Slice(buffer.GetPosition(offset)).First.Span[0];
            }

            var result = (long)0;
            for (var i = 0; i < _doubleSize; i++)
            {
                result += readValue(i) << (8 * i);
            }

            pipeReader.AdvanceTo(readResult.Buffer.GetPosition(_doubleSize));
            return BitConverter.Int64BitsToDouble(result);
        }
    }
}
