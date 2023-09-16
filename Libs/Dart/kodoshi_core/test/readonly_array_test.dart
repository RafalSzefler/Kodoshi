import 'package:kodoshi_core/lib.dart';
import 'package:test/test.dart';

void main() {
  group('ReadOnlyArray tests', () {
    setUp(() {
      // Additional setup goes here.
    });

    test('Test1', () {
      final array = ReadOnlyArray.empty<int>();
      expect(array.length, 0);
      final array2 = ReadOnlyArray.empty<int>();
      expect(array.hashCode, array2.hashCode);
      expect(array == array2, true);
    });

    test('Test2', () {
      final array = ReadOnlyArray.move([1, 2, 3]);
      expect(array.length, 3);
      expect(array[0], 1);
      expect(array[1], 2);
      expect(array[2], 3);

      final array2 = ReadOnlyArray.move([1, 2, 3]);
      expect(array.hashCode, array2.hashCode);
      expect(array == array2, true);
    });

    test('Test3', () {
      final array = ReadOnlyArray.empty<int>();
      final array2 = ReadOnlyArray.move([1, 2, 3]);
      expect(array.hashCode != array2.hashCode, true);
      expect(array == array2, false);
    });
  });
}
