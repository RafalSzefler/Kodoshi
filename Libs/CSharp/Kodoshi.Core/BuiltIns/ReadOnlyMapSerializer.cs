using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class ReadOnlyMapSerializer<TKey, TValue>
            : ISerializer<ReadOnlyMap<TKey, TValue>>
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue>
    {

        private readonly ISerializer<TKey> _internalKeySerializer;
        private readonly ISerializer<TValue> _internalValueSerializer;
        private readonly ISerializer<uint> _uintSerializer;
        private readonly StreamPipeWriterOptions _writerOptions = new StreamPipeWriterOptions(leaveOpen: true);

        public ReadOnlyMapSerializer(
            ISerializer<TKey> internalKeySerializer,
            ISerializer<TValue> internalValueSerializer,
            ISerializer<uint> uintSerializer)
        {
            _internalKeySerializer = internalKeySerializer;
            _internalValueSerializer = internalValueSerializer;
            _uintSerializer = uintSerializer;
        }

        public async ValueTask SerializeAsync(ReadOnlyMap<TKey, TValue> instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            using var stream = new MemoryStream();
            var tmpWriter = PipeWriter.Create(stream, _writerOptions);
            foreach (var item in instance)
            {
                await _internalKeySerializer.SerializeAsync(item.Key, tmpWriter, ct).ConfigureAwait(false);
                await _internalValueSerializer.SerializeAsync(item.Value, tmpWriter, ct).ConfigureAwait(false);
            }

            await tmpWriter.FlushAsync(ct).ConfigureAwait(false);
            await tmpWriter.CompleteAsync().ConfigureAwait(false);
            var writtenBytes = (int)stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            await _uintSerializer.SerializeAsync((uint)writtenBytes, pipeWriter, ct).ConfigureAwait(false);
            var tmpReader = PipeReader.Create(stream);
            await tmpReader.CopyToAsync(pipeWriter, ct).ConfigureAwait(false);
            await tmpReader.CompleteAsync().ConfigureAwait(false);
        }

        public async ValueTask<ReadOnlyMap<TKey, TValue>> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var size = (int)(await _uintSerializer.DeserializeAsync(pipeReader, ct).ConfigureAwait(false));
            if (size == 0)
                return ReadOnlyMap.Empty<TKey, TValue>();
            var result = await pipeReader.ReadAtLeastAsync(size, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (result.IsCanceled)
                throw new OperationCanceledException();
            if (result.IsCompleted && result.Buffer.Length < size)
                throw new StreamClosedException();
            var tmpReader = PipeReader.Create(result.Buffer.Slice(0, size));
            var resultMap = new Dictionary<TKey, TValue>();
            while (true)
            {
                try
                {
                    var key = await _internalKeySerializer.DeserializeAsync(tmpReader, ct).ConfigureAwait(false);
                    var value = await _internalValueSerializer.DeserializeAsync(tmpReader, ct).ConfigureAwait(false);
                    resultMap[key] = value;
                }
                catch (Exceptions.StreamClosedException)
                {
                    break;
                }
            }

            pipeReader.AdvanceTo(result.Buffer.GetPosition(size));
            return ReadOnlyMap.Copy(resultMap);
        }
    }
}
