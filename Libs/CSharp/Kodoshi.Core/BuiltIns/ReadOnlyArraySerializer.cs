using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class ReadOnlyArraySerializer<T> : ISerializer<ReadOnlyArray<T>>
        where T : IEquatable<T>
    {
        public ReadOnlyArraySerializer(
            ISerializer<T> internalSerializer,
            ISerializer<uint> uintSerializer)
        {
            _internalSerializer = internalSerializer;
            _uintSerializer = uintSerializer;
        }

        private readonly ISerializer<T> _internalSerializer;
        private readonly ISerializer<uint> _uintSerializer;
        private readonly StreamPipeWriterOptions _writerOptions = new StreamPipeWriterOptions(leaveOpen: true);
        public async ValueTask SerializeAsync(ReadOnlyArray<T> instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            using var stream = new MemoryStream();
            var tmpWriter = PipeWriter.Create(stream, _writerOptions);
            foreach (var item in instance)
                await _internalSerializer.SerializeAsync(item, tmpWriter, ct).ConfigureAwait(false);
            await tmpWriter.FlushAsync(ct).ConfigureAwait(false);
            await tmpWriter.CompleteAsync().ConfigureAwait(false);
            var writtenBytes = (int)stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            await _uintSerializer.SerializeAsync((uint)writtenBytes, pipeWriter, ct).ConfigureAwait(false);
            var tmpReader = PipeReader.Create(stream);
            await tmpReader.CopyToAsync(pipeWriter, ct).ConfigureAwait(false);
            await tmpReader.CompleteAsync().ConfigureAwait(false);
        }

        public async ValueTask<ReadOnlyArray<T>> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var size = (int)(await _uintSerializer.DeserializeAsync(pipeReader, ct).ConfigureAwait(false));
            if (size == 0) return ReadOnlyArray.Empty<T>();
            var result = await pipeReader.ReadAtLeastAsync(size, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (result.IsCanceled) throw new System.OperationCanceledException();
            if (result.IsCompleted && result.Buffer.Length < size)
                throw new StreamClosedException();
            var tmpReader = PipeReader.Create(result.Buffer.Slice(0, size));
            var resultList = new T[4];
            var idx = 0;
            while (true)
            {
                T instance;
                try
                {
                    instance = await _internalSerializer.DeserializeAsync(tmpReader, ct).ConfigureAwait(false);
                }
                catch (Exceptions.StreamClosedException)
                {
                    break;
                }

                if (idx == resultList.Length)
                {
                    var newSize = (idx <= 32768) ? 2 * idx : (int)(1.5 * idx);
                    var newResultList = new T[newSize];
                    Array.Copy(resultList, newResultList, idx);
                    resultList = newResultList;
                }

                resultList[idx] = instance;
                idx++;
            }

            pipeReader.AdvanceTo(result.Buffer.GetPosition(size));
            return ReadOnlyArray.Move(resultList, length: idx);
        }
    }

}