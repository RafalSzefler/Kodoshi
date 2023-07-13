using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.Core
{
    public static class Extensions
    {
        private static readonly StreamPipeWriterOptions _writerOptions = new StreamPipeWriterOptions(leaveOpen: true);
        public static async ValueTask<byte[]> SerializeToArray<T>(this ISerializer<T> serializer, T instance, CancellationToken ct)
            where T : IEquatable<T>
        {
            using (var memory = new MemoryStream())
            {
                var writer = PipeWriter.Create(memory, _writerOptions);
                await serializer.SerializeAsync(instance, writer, ct);
                await writer.FlushAsync();
                await memory.FlushAsync();
                memory.Seek(0, SeekOrigin.Begin);
                return memory.ToArray();
            }
        }

        public static ValueTask<T> DeserializeFromMemory<T>(this ISerializer<T> serializer, Memory<byte> data, CancellationToken ct)
            where T : IEquatable<T>
        {
            var sequence = new ReadOnlySequence<byte>(data);
            var reader = PipeReader.Create(sequence);
            return serializer.DeserializeAsync(reader, ct);
        }
    }
}
