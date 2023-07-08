using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Kodoshi.Core
{
    public readonly struct ReadOnlyMap<TKey, TValue>
            : IReadOnlyDictionary<TKey, TValue>,
            IEnumerable<KeyValuePair<TKey, TValue>>,
            IEquatable<ReadOnlyMap<TKey, TValue>>
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue>
    {
        internal static readonly ReadOnlyMap<TKey, TValue> Empty = new ReadOnlyMap<TKey, TValue>(FrozenDictionary<TKey, TValue>.Empty);

        private readonly FrozenDictionary<TKey, TValue> _instance;
        internal ReadOnlyMap(FrozenDictionary<TKey, TValue> instance)
        {
            this._instance = instance;
        }

        public TValue this[TKey key] => _instance[key];
        public IEnumerable<TKey> Keys => _instance.Keys;
        public IEnumerable<TValue> Values => _instance.Values;
        public int Count => _instance.Count;

        public bool ContainsKey(TKey key) => _instance.ContainsKey(key);

        public bool Equals(ReadOnlyMap<TKey, TValue> other)
        {
            if (this._instance.Count != other._instance.Count)
            {
                return false;
            }

            foreach (var kvp in this._instance)
            {
                if (!other._instance.TryGetValue(kvp.Key, out var otherValue))
                {
                    return false;
                }

                if (!kvp.Value.Equals(otherValue))
                {
                    return false;
                }
            }

            return true;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _instance.GetEnumerator();

        public bool TryGetValue(TKey key, out TValue value)
            => _instance.TryGetValue(key, out value!);

        IEnumerator IEnumerable.GetEnumerator() => _instance.GetEnumerator();

        public override bool Equals(object? obj) => (obj is ReadOnlyMap<TKey, TValue> map) && Equals(map);

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 2166136261;
                var e = _instance.GetEnumerator();
                while (e.MoveNext())
                {
                    var curr = e.Current;
                    hash = (hash ^ Utils.CalculateHashCode(curr.Key)) * 16777619;
                    hash = (hash ^ Utils.CalculateHashCode(curr.Value)) * 16777619;
                }

                return (int)hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FrozenDictionary<TKey, TValue> AsDict() => _instance;
    }

    public static class ReadOnlyMap
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMap<TKey, TValue> Empty<TKey, TValue>()
                where TKey : System.IEquatable<TKey>
                where TValue : System.IEquatable<TValue>
            => ReadOnlyMap<TKey, TValue>.Empty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMap<TKey, TValue> Copy<TKey, TValue>(Dictionary<TKey, TValue> dict)
                where TKey : IEquatable<TKey>
                where TValue : IEquatable<TValue>
            => Move(dict.ToFrozenDictionary());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMap<TKey, TValue> Copy<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values)
                where TKey : IEquatable<TKey>
                where TValue : IEquatable<TValue>
            => Move(values.ToFrozenDictionary());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMap<TKey, TValue> Move<TKey, TValue>(FrozenDictionary<TKey, TValue>? dict)
                where TKey : IEquatable<TKey>
                where TValue : IEquatable<TValue>
            => (dict == null) ? (Empty<TKey, TValue>()) : (new ReadOnlyMap<TKey, TValue>(dict));
    }
}
