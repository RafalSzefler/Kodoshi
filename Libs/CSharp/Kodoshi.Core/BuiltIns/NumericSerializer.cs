using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class NumericSerializer
        : ISerializer<long>,
        ISerializer<int>,
        ISerializer<short>,
        ISerializer<sbyte>,
        ISerializer<ulong>,
        ISerializer<uint>,
        ISerializer<ushort>,
        ISerializer<byte>
    {
        private const int _maxBufferSize = 10;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ZigZagEncode(long value) => unchecked((ulong)((value << 1) ^ (value >> 63)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ZigZagDecode(ulong value) => unchecked((long)(value >> 1) ^ -((long)value & 1));

        private async ValueTask<ulong> DeserializeUlong(PipeReader pipeReader, CancellationToken ct)
        {
            ulong currentResult = 0;
            var totalIndex = 0;
            var consumedBytes = 0;
            bool tryConsume(ref ReadOnlySequence<byte> seq)
            {
                var enumerator = seq.GetEnumerator();
                if (!enumerator.MoveNext())
                    return false;
                var current = enumerator.Current.Span;
                var idx = 0;
                while (true)
                {
                    if (current.Length == idx)
                    {
                        if (!enumerator.MoveNext())
                            return false;
                        current = enumerator.Current.Span;
                        idx = 0;
                    }

                    var tmpValue = (ulong)current[idx];
                    var isFinal = (tmpValue & 0b10000000) == 0b10000000;
                    tmpValue = tmpValue & 0b01111111;
                    currentResult += (tmpValue << (7 * totalIndex));
                    totalIndex++;
                    consumedBytes++;
                    idx++;
                    if (isFinal)
                        return true;
                    if (consumedBytes >= _maxBufferSize)
                        throw new NumberOutOfRangeException();
                }
            }

            while (true)
            {
                var result = await pipeReader.ReadAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (result.IsCanceled)
                    throw new OperationCanceledException();
                var buffer = result.Buffer;
                var consumationResult = tryConsume(ref buffer);
                pipeReader.AdvanceTo(buffer.GetPosition(consumedBytes));
                consumedBytes = 0;
                if (consumationResult)
                    return currentResult;
                if (result.IsCompleted)
                    throw new StreamClosedException();
            }
        }

        private async ValueTask SerializeUlong(ulong instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(_maxBufferSize);
            try
            {
                var idx = 0;
                do
                {
                    var tmp = (byte)(instance & 0b01111111);
                    instance >>= 7;
                    if (instance == 0)
                    {
                        tmp = (byte)(tmp | 0b10000000);
                    }

                    buffer[idx] = tmp;
                    idx++;
                }
                while (instance > 0);
                var memory = buffer.AsMemory(0, idx);
                await pipeWriter.WriteAsync(memory, ct).ConfigureAwait(false);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        ValueTask ISerializer<long>.SerializeAsync(long instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(ZigZagEncode(instance), pipeWriter, ct);
        }

        async ValueTask<long> ISerializer<long>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await DeserializeUlong(pipeReader, ct).ConfigureAwait(false);
            return ZigZagDecode(result);
        }

        ValueTask ISerializer<int>.SerializeAsync(int instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(ZigZagEncode(instance), pipeWriter, ct);
        }

        async ValueTask<int> ISerializer<int>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await DeserializeUlong(pipeReader, ct).ConfigureAwait(false);
            var signed = ZigZagDecode(result);
            if (signed < int.MinValue || signed > int.MaxValue)
                throw new NumberOutOfRangeException();
            return (int)signed;
        }

        ValueTask ISerializer<short>.SerializeAsync(short instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(ZigZagEncode(instance), pipeWriter, ct);
        }

        async ValueTask<short> ISerializer<short>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await DeserializeUlong(pipeReader, ct).ConfigureAwait(false);
            var signed = ZigZagDecode(result);
            if (signed < short.MinValue || signed > short.MaxValue)
                throw new NumberOutOfRangeException();
            return (short)signed;
        }

        ValueTask ISerializer<sbyte>.SerializeAsync(sbyte instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(ZigZagEncode(instance), pipeWriter, ct);
        }

        async ValueTask<sbyte> ISerializer<sbyte>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await DeserializeUlong(pipeReader, ct).ConfigureAwait(false);
            var signed = ZigZagDecode(result);
            if (signed < sbyte.MinValue || signed > sbyte.MaxValue)
                throw new NumberOutOfRangeException();
            return (sbyte)signed;
        }

        ValueTask ISerializer<ulong>.SerializeAsync(ulong instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(instance, pipeWriter, ct);
        }

        ValueTask<ulong> ISerializer<ulong>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            return DeserializeUlong(pipeReader, ct);
        }

        ValueTask ISerializer<uint>.SerializeAsync(uint instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(instance, pipeWriter, ct);
        }

        async ValueTask<uint> ISerializer<uint>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await DeserializeUlong(pipeReader, ct).ConfigureAwait(false);
            if (result < uint.MinValue || result > uint.MaxValue)
                throw new NumberOutOfRangeException();
            return (uint)result;
        }

        ValueTask ISerializer<ushort>.SerializeAsync(ushort instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(instance, pipeWriter, ct);
        }

        async ValueTask<ushort> ISerializer<ushort>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await DeserializeUlong(pipeReader, ct).ConfigureAwait(false);
            if (result < ushort.MinValue || result > ushort.MaxValue)
                throw new NumberOutOfRangeException();
            return (ushort)result;
        }

        ValueTask ISerializer<byte>.SerializeAsync(byte instance, PipeWriter pipeWriter, CancellationToken ct)
        {
            return SerializeUlong(instance, pipeWriter, ct);
        }

        async ValueTask<byte> ISerializer<byte>.DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await DeserializeUlong(pipeReader, ct).ConfigureAwait(false);
            if (result < byte.MinValue || result > byte.MaxValue)
                throw new NumberOutOfRangeException();
            return (byte)result;
        }
    }
}
