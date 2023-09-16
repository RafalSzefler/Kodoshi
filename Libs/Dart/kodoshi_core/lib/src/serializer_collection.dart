import 'serializer.dart';


abstract class SerializerCollection {
  Serializer<T> GetSerializer<T>();
}