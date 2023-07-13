using Kodoshi.Core;
using TestProjectBase.Core;

namespace tests;

public class DefaultValuesTests
{
    private readonly IDefaultValuesCollection defaultValuesCollection
        = new DefaultValuesCollectionBuilder().Build();

    [Fact]
    public void TestNumbers()
    {
        Assert.Equal((sbyte)0, defaultValuesCollection.GetDefaultValue<sbyte>());
        Assert.Equal((byte)0, defaultValuesCollection.GetDefaultValue<byte>());
        Assert.Equal((short)0, defaultValuesCollection.GetDefaultValue<short>());
        Assert.Equal((ushort)0, defaultValuesCollection.GetDefaultValue<ushort>());
        Assert.Equal((int)0, defaultValuesCollection.GetDefaultValue<int>());
        Assert.Equal((uint)0, defaultValuesCollection.GetDefaultValue<uint>());
        Assert.Equal((long)0, defaultValuesCollection.GetDefaultValue<long>());
        Assert.Equal((ulong)0, defaultValuesCollection.GetDefaultValue<ulong>());
    }

    [Fact]
    public void TestString()
    {
        Assert.Equal("", defaultValuesCollection.GetDefaultValue<string>());
    }

    [Fact]
    public void TestGuid()
    {
        Assert.Equal(Guid.Empty, defaultValuesCollection.GetDefaultValue<Guid>());
    }

    [Fact]
    public void TestBool()
    {
        Assert.False(defaultValuesCollection.GetDefaultValue<bool>());
    }

    [Fact]
    public void TestArrays()
    {
        void testIt<T>() where T : IEquatable<T>
        {
            Assert.Equal(ReadOnlyArray.Empty<T>(), defaultValuesCollection.GetDefaultValue<ReadOnlyArray<T>>());
        }
        testIt<byte>();
        testIt<int>();
        testIt<bool>();
        testIt<Guid>();
        testIt<string>();
        testIt<ReadOnlyArray<int>>();
        testIt<ReadOnlyMap<Guid, ReadOnlyArray<string>>>();
    }

    [Fact]
    public void TestMaps()
    {
        void testIt<TKey, TValue>()
            where TKey : IEquatable<TKey>
            where TValue : IEquatable<TValue>
        {
            Assert.Equal(ReadOnlyMap.Empty<TKey, TValue>(), defaultValuesCollection.GetDefaultValue<ReadOnlyMap<TKey, TValue>>());
        }
        
        testIt<int, int>();
        testIt<bool, string>();
        testIt<string, Guid>();
        testIt<Guid, ReadOnlyArray<int>>();
        testIt<ReadOnlyMap<ReadOnlyArray<int>, Guid>, ReadOnlyArray<int>>();
    }

    [Fact]
    public void TestFoo()
    {
        var foo = new Foo(0, Guid.Empty, Kodoshi.Core.VoidType.Instance);
        Assert.Equal(foo, defaultValuesCollection.GetDefaultValue<Foo>());
    }

    [Fact]
    public void TestBaz()
    {
        var baz = new Baz("", new Foo(0, Guid.Empty, Kodoshi.Core.VoidType.Instance), "", "", ReadOnlyArray.Empty<Foo>());
        Assert.Equal(baz, defaultValuesCollection.GetDefaultValue<Baz>());
    }

    
    [Fact]
    public void TestExample()
    {
        var instance = new Example<string>(0, "", ReadOnlyArray.Empty<ReadOnlyArray<string>>());
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<Example<string>>());
    }

    [Fact]
    public void TestAnotherExample()
    {
        var instance = new Example<byte>(0, 0, ReadOnlyArray.Empty<ReadOnlyArray<byte>>());
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<Example<byte>>());
    }

    [Fact]
    public void TestExample2()
    {
        var instance = new Example2<string, string>(0, "", ReadOnlyMap.Empty<string, string>());
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<Example2<string, string>>());
    }

    [Fact]
    public void TestAnotherExample2()
    {
        var instance = new Example2<int, bool>(0, false, ReadOnlyMap.Empty<int, bool>());
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<Example2<int, bool>>());
    }

    [Fact]
    public void TestMainTaggy()
    {
        var instance = MainTaggy.CreateEmpty<int>();
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<MainTaggy<int>>());
    }

    [Fact]
    public void TestMainTaggy2()
    {
        var instance = MainTaggy.CreateEmpty<string>();
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<MainTaggy<string>>());
    }
    
    [Fact]
    public void TestSimpleTag()
    {
        var instance = SimpleTag.CreateZeroValue(new Baz("", new Foo(0, Guid.Empty, Kodoshi.Core.VoidType.Instance), "", "", ReadOnlyArray.Empty<Foo>()));
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<SimpleTag>());
    }
}
