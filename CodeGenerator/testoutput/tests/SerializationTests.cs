using Kodoshi.Core;
using KodoshiGenerated.Core;
using KodoshiGenerated.Models;
using KodoshiGenerated.Models.Abcd;
using KodoshiGenerated.Models.MyNmspc;

namespace tests;

public class SerializationTests
{
    private readonly ISerializerCollection _serializerCollection =
        new SerializerCollectionBuilder().Build();

    [Fact]
    public async Task TestFooSerialization0()
    {
        var foo = new Foo(0, Guid.Empty);
        var serializer = _serializerCollection.GetSerializer<Foo>();
        var serializedArray = await serializer.SerializeToArray(foo);
        var expectedArray = new byte[] { 128 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(foo, result);
    }

    [Fact]
    public async Task TestFooSerialization1()
    {
        var foo = new Foo(5, Guid.Empty);
        var serializer = _serializerCollection.GetSerializer<Foo>();
        var serializedArray = await serializer.SerializeToArray(foo);
        var expectedArray = new byte[] { 130, 136, 138 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(foo, result);
    }

    [Fact]
    public async Task TestFooSerialization2()
    {
        var foo = new Foo(5, Guid.Parse("14dce590-8a03-4ebd-b3bd-6a65e88391f4"));
        var serializer = _serializerCollection.GetSerializer<Foo>();
        var serializedArray = await serializer.SerializeToArray(foo);
        var expectedArray = new byte[] { 147, 136, 138, 148, 144, 229, 220, 20, 3, 138, 189, 78, 179, 189, 106, 101, 232, 131, 145, 244 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(foo, result);
    }

    [Fact]
    public async Task TestFooSerialization3()
    {
        var foo = new Foo(-5, Guid.NewGuid());
        var serializer = _serializerCollection.GetSerializer<Foo>();
        var serializedArray = await serializer.SerializeToArray(foo);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(foo, result);
    }

    [Fact]
    public async Task TestBazSerialization0()
    {
        var baz = new Baz("", new Foo(0, Guid.Empty), "", "", ReadOnlyArray.Empty<Foo>());
        var serializer = _serializerCollection.GetSerializer<Baz>();
        var serializedArray = await serializer.SerializeToArray(baz);
        var expectedArray = new byte[] { 128 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(baz, result);
    }

    [Fact]
    public async Task TestBazSerialization1()
    {
        var baz = new Baz("", new Foo(36, Guid.Empty), "", "", ReadOnlyArray.Empty<Foo>());
        var serializer = _serializerCollection.GetSerializer<Baz>();
        var serializedArray = await serializer.SerializeToArray(baz);
        var expectedArray = new byte[] { 132, 145, 130, 136, 200 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(baz, result);
    }

    [Fact]
    public async Task TestBazSerialization2()
    {
        var baz = new Baz("abcd TTEESSTT", new Foo(36, Guid.Empty), "", "xyz", ReadOnlyArray.Empty<Foo>());
        var serializer = _serializerCollection.GetSerializer<Baz>();
        var serializedArray = await serializer.SerializeToArray(baz);
        var expectedArray = new byte[] { 152, 137, 141, 97, 98, 99, 100, 32, 84, 84, 69, 69, 83, 83, 84, 84, 145, 130, 136, 200, 169, 131, 120, 121, 122 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(baz, result);
    }

    [Fact]
    public async Task TestBazSerialization3()
    {
        var arr = new Foo[5];
        for (var i = 0; i < arr.Length; i++)
        {
            arr[i] = new Foo(i, Guid.Empty);
        }
        var baz = new Baz("abcd TTEESSTT", new Foo(36, Guid.Empty), "", "xyz", ReadOnlyArray.Move(arr));
        var serializer = _serializerCollection.GetSerializer<Baz>();
        var serializedArray = await serializer.SerializeToArray(baz);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(baz, result);
    }

    [Fact]
    public async Task TestSimpleTagSerialization()
    {
        var instance = SimpleTag.CreateValue();
        var serializer = _serializerCollection.GetSerializer<SimpleTag>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var expectedArray = new byte[] { 129, 129 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestSimpleTagSerialization2()
    {
        var instance = SimpleTag.CreateZeroValue(new Baz("abc", new Foo(1, Guid.Empty), "", "", ReadOnlyArray.Empty<Foo>()));
        var serializer = _serializerCollection.GetSerializer<SimpleTag>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var expectedArray = new byte[] { 139, 128, 137, 137, 131, 97, 98, 99, 145, 130, 136, 130, };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestSimpleTagSerialization3()
    {
        var instance = SimpleTag.CreateZeroValue(new Baz("", new Foo(0, Guid.Empty), "", "", ReadOnlyArray.Empty<Foo>()));
        var serializer = _serializerCollection.GetSerializer<SimpleTag>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var expectedArray = new byte[] { 128 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestMainTaggySerialization()
    {
        var instance = MainTaggy.CreateEmpty<int>();
        var serializer = _serializerCollection.GetSerializer<MainTaggy<int>>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var expectedArray = new byte[] { 128 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestMainTaggySerialization2()
    {
        var instance = MainTaggy.CreateValue<int>(14);
        var serializer = _serializerCollection.GetSerializer<MainTaggy<int>>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var expectedArray = new byte[] { 130, 129, 156 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestMainTaggySerialization3()
    {
        var instance = MainTaggy.CreateValue<string>("abc");
        var serializer = _serializerCollection.GetSerializer<MainTaggy<string>>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var expectedArray = new byte[] { 133, 129, 131, 97, 98, 99 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }

    [Fact]
    public async Task TestMainTaggySerialization4()
    {
        var instance = MainTaggy.CreateValue<Foo>(new Foo(3, Guid.Empty));
        var serializer = _serializerCollection.GetSerializer<MainTaggy<Foo>>();
        var serializedArray = await serializer.SerializeToArray(instance);
        var expectedArray = new byte[] { 132, 129, 130, 136, 134 };
        Assert.Equal(expectedArray, serializedArray);
        var result = await serializer.DeserializeFromArray(serializedArray);
        Assert.Equal(instance, result);
    }
}
