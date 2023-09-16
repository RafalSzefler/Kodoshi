import 'dart:typed_data';


abstract class Serializer<T> {
  Uint8List Serialize(T instance);
  T Deserialize(Uint8List buffer);
}