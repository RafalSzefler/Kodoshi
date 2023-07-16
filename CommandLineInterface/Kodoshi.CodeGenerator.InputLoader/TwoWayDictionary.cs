using System.Collections.Generic;

namespace Kodoshi.CodeGenerator.InputLoader;

internal sealed class TwoWayDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> _forward
        = new Dictionary<TKey, TValue>();
    private readonly Dictionary<TValue, TKey> _backward
        = new Dictionary<TValue, TKey>();

    public void Add(TKey key, TValue value)
    {
        _forward[key] = value;
        _backward[value] = key;
    }

    public bool ContainsKey(TKey key) => _forward.ContainsKey(key);
    
    public bool ContainsValue(TValue value) => _backward.ContainsKey(value);

    public TValue GetByKey(TKey key) => _forward[key];

    public bool TryGetByKey(TKey key, out TValue result) => _forward.TryGetValue(key, out result);

    public TKey GetByValue(TValue value) => _backward[value];

    public bool TryGetByValue(TValue value, out TKey result) => _backward.TryGetValue(value, out result);

    public IEnumerable<KeyValuePair<TKey, TValue>> Items() => _forward;
}
