using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class StringSerializer : ISerializer<string>
    {
        private readonly ISerializer<ReadOnlyArray<byte>> _byteArraySerializer;
        public StringSerializer(ISerializer<ReadOnlyArray<byte>> byteArraySerializer)
        {
            _byteArraySerializer = byteArraySerializer;
        }

        public async ValueTask SerializeAsync(string instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            var pool = ArrayPool<byte>.Shared;
            var size = Encoding.UTF8.GetByteCount(instance);
            var buffer = pool.Rent(size);
            Encoding.UTF8.GetBytes(instance, buffer);
            try
            {
                await _byteArraySerializer.SerializeAsync(ReadOnlyArray.Move(buffer, length: size), pipeWriter, ct).ConfigureAwait(false);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public async ValueTask<string> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await _byteArraySerializer.DeserializeAsync(pipeReader, ct).ConfigureAwait(false);
            return Encoding.UTF8.GetString(result.AsSpan());
        }
    }
}
