class ReadOnlyMap<TKey, TValue> {
  static ReadOnlyMap<TKey, TValue> empty<TKey, TValue>()
  {
    assert(TKey != dynamic);
    assert(TValue != dynamic);
    final inner = _emptyMaps.putIfAbsent(TKey, _builder);
    return (inner[TValue] ??= ReadOnlyMap.move(<TKey, TValue>{})) as ReadOnlyMap<TKey, TValue>;
  }

  final Map<TKey, TValue> _internal;

  ReadOnlyMap.copy(Map<TKey, TValue> map) : _internal = Map.from(map);
  ReadOnlyMap.move(Map<TKey, TValue> map) : _internal = map;

  Map<TKey, TValue> asMap() => _internal;
  int get length => _internal.length;
  TValue? operator [](TKey key) => _internal[key];

  bool equals(ReadOnlyMap<TKey, TValue> other) {
    if (_internal.length != other._internal.length) return false;
    for (final entry in _internal.entries)
    {
      final otherEntry = other._internal[entry.key];
      if (otherEntry == null || otherEntry != entry.value) return false;
    }
    return true;
  }
  
  @override
  bool operator ==(other) => other is ReadOnlyMap<TKey, TValue> && equals(other);

  @override
  int get hashCode {
    var hash = 2166136261;
    final map = this._internal;
    final keys = map.keys.toList();
    keys.sort();
    for (final key in keys)
    {
      hash = (hash ^ key.hashCode) * 16777619;
      hash = (hash ^ map[key].hashCode) * 16777619;
    }
    return hash;
  }
}

final _emptyMaps = <Type, Map<Type, Object>>{};
Map<Type, Object> _builder() => <Type, Object>{};
