class ReadOnlyArray<T> {
  static ReadOnlyArray<T> empty<T>()
  {
    assert(T != dynamic);
    return (_emptyArrays[T] ??= ReadOnlyArray.move(<T>[])) as ReadOnlyArray<T>;
  }

  final List<T> _internal;

  ReadOnlyArray.copy(List<T> array) : _internal = List.from(array);
  ReadOnlyArray.move(List<T> array) : _internal = array;

  List<T> asList() => _internal;
  int get length => _internal.length;
  T operator [](int i) => _internal[i];

  bool equals(ReadOnlyArray<T> other) {
    if (_internal.length != other._internal.length) return false;
    final l = _internal.length;
    for (var i = 0; i < l; i++)
    {
      if (_internal[i] != other._internal[i]) return false;
    }
    return true;
  }
  
  @override
  bool operator ==(other) => other is ReadOnlyArray<T> && equals(other);

  @override
  int get hashCode {
    var hash = 2166136261;
    final buff = this._internal;
    final l = buff.length;
    for (var i = 0; i < l; i++)
    {
        hash = (hash ^ buff[i].hashCode) * 16777619;
    }
    return hash;
  }
}

final _emptyArrays = <Type, Object>{};
