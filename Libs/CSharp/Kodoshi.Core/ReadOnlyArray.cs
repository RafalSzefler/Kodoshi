using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Kodoshi.Core
{
    public readonly struct ReadOnlyArray<T> : IReadOnlyList<T>, IEquatable<ReadOnlyArray<T>> where T : IEquatable<T>
    {
        internal static readonly ReadOnlyArray<T> Empty = new ReadOnlyArray<T>(Array.Empty<T>(), 0, 0);
        internal readonly T[] _buffer;
        internal readonly int _startIdx;
        internal readonly int _length;
        internal ReadOnlyArray(T[] buffer, int startIdx, int length)
        {
            this._buffer = buffer;
            this._startIdx = startIdx;
            this._length = length;
        }

        public T this[int index] => _buffer[_startIdx + index];
        public int Count => _length;

        public bool Equals(ReadOnlyArray<T> other)
        {
            var left = _buffer.AsSpan(_startIdx, _length);
            var right = other._buffer.AsSpan(other._startIdx, other._length);
            return left.SequenceEqual(right);
        }

        public IEnumerator<T> GetEnumerator()
        {
            var l = _startIdx + _length;
            for (var i = _startIdx; i < l; i++)
                yield return _buffer[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override bool Equals(object? obj) => (obj is ReadOnlyArray<T> arr) && Equals(arr);

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 2166136261;
                var l = _startIdx + _length;
                for (var i = _startIdx; i < l; i++)
                {
                    hash = (hash ^ Utils.CalculateHashCode(_buffer[i])) * 16777619;
                }

                return (int)hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan() => _buffer.AsSpan(_startIdx, _length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<T> AsMemory() => _buffer.AsMemory(_startIdx, _length);
    }

    public static class ReadOnlyArray
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<T> Empty<T>() where T : IEquatable<T>
            => ReadOnlyArray<T>.Empty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<T> Copy<T>(IEnumerable<T>? enumerable)
            where T : IEquatable<T>
        {
            if (enumerable is null)
            {
                return Empty<T>();
            }

            if (enumerable is T[] array)
            {
                return ReadOnlyArray.Copy(array);
            }

            return Move(enumerable.ToArray());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<T> Copy<T>(T[]? array, int startIdx = 0, int length = -1)
            where T : IEquatable<T>
        {
            if (array is null)
            {
                return Empty<T>();
            }

            var l = array.Length;
            if (l == 0)
            {
                return Empty<T>();
            }

            if (length < 0) length = array.Length;
            if (l < length) length = l;
            var copy = new T[length];
            Array.Copy(array, copy, length);
            return Move(copy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<T> Move<T>(T[]? array, int startIdx = 0, int length = -1)
            where T : IEquatable<T>
        {
            if (array == null)
            {
                return Empty<T>();
            }

            if (length < 0)
                length = array.Length;
            return new ReadOnlyArray<T>(array, startIdx, length);
        }
    }
}
