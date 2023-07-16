using Kodoshi.Core;
using TestProjectBase.Core;
using TestProjectBase.Models.v1;

namespace Tests.TestProjectBase.Models;

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
    public void TestUserAttributes()
    {
        var foo = new UserAttributes("", "");
        Assert.Equal(foo, defaultValuesCollection.GetDefaultValue<UserAttributes>());
    }

    [Fact]
    public void TestUserAttributesFalse()
    {
        var instance = new UserAttributes("abcd", "");
        Assert.NotEqual(instance, defaultValuesCollection.GetDefaultValue<UserAttributes>());
    }

    [Fact]
    public void TestUserData()
    {
        var instance = new UserData<UserAttributes>(Guid.Empty, new UserAttributes("", ""));
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<UserData<UserAttributes>>());
    }

    [Fact]
    public void TestUserDataFalse()
    {
        var instance = new UserData<UserAttributes>(Guid.NewGuid(), new UserAttributes("", ""));
        Assert.NotEqual(instance, defaultValuesCollection.GetDefaultValue<UserData<UserAttributes>>());
    }

    [Fact]
    public void TestEmptyable()
    {
        var instance = Emptyable.CreateEmpty<int>();
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<Emptyable<int>>());
    }

    [Fact]
    public void TestEvents()
    {
        var instance = Events.CreateUNKNOWN();
        Assert.Equal(instance, defaultValuesCollection.GetDefaultValue<Events>());
    }
}
