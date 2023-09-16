import 'package:kodoshi_core/lib.dart';
import 'package:test/test.dart';

void main() {
  group('ReadOnlyMap tests', () {
    setUp(() {
      // Additional setup goes here.
    });

    test('Test1', () {
      final map = ReadOnlyMap.empty<int, String>();
      expect(map.length, 0);
      final map2 = ReadOnlyMap.empty<int, String>();
      expect(map.hashCode, map2.hashCode);
      expect(map == map2, true);
    });

    test('Test2', () {
      final map = ReadOnlyMap.move({1: "test", 5: "foo"});
      expect(map.length, 2);
      expect(map[1], "test");
      expect(map[5], "foo");

      final map2 = ReadOnlyMap.move({5: "foo", 1: "test"});
      expect(map.hashCode, map2.hashCode);
      expect(map == map2, true);
    });

    test('Test3', () {
      final map = ReadOnlyMap.empty<int, String>();
      final map2 = ReadOnlyMap.move({2: "baz"});
      expect(map.hashCode != map2.hashCode, true);
      expect(map == map2, false);
    });
  });
}
