using System.IO.Pipelines;
using Kodoshi.Core.BuiltIns;

namespace Kodoshi.Core.Tests;

public class MapSerializationTests
{
    private ISerializer<ReadOnlyMap<string, int>> BuildSerializer1()
    {
        var numericSerializer = new NumericSerializer();
        var byteArraySerializer = new ByteArraySerializer(numericSerializer);
        return new ReadOnlyMapSerializer<string, int>(
            new StringSerializer(byteArraySerializer),
            numericSerializer,
            numericSerializer
        );
    }

    private ISerializer<ReadOnlyMap<Guid, string>> BuildSerializer2()
    {
        var numericSerializer = new NumericSerializer();
        var byteArraySerializer = new ByteArraySerializer(numericSerializer);
        return new ReadOnlyMapSerializer<Guid, string>(
            new GuidSerializer(),
            new StringSerializer(byteArraySerializer),
            numericSerializer
        );
    }

    private ISerializer<ReadOnlyMap<Guid, ReadOnlyArray<int>>> BuildSerializer3()
    {
        var numericSerializer = new NumericSerializer();
        var arraySerializer = new ReadOnlyArraySerializer<int>(
            numericSerializer, numericSerializer
        );
        return new ReadOnlyMapSerializer<Guid, ReadOnlyArray<int>>(
            new GuidSerializer(),
            arraySerializer,
            numericSerializer
        );
    }

    [Fact]
    public async Task TestSerialization()
    {
        var data = new Dictionary<string, int>();
        data["test"] = 1;
        data["foo"] = -10;
        var map = ReadOnlyMap.Copy(data);
        var pipe = new Pipe();
        var serializer = BuildSerializer1();
        await serializer.SerializeAsync(map, pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.True(map.Equals(result));
    }

    [Fact]
    public async Task TestGuidSerialization()
    {
        var data = new Dictionary<Guid, string>();
        data[Guid.NewGuid()] = "foo";
        data[Guid.NewGuid()] = "baz";
        data[Guid.Empty] = "empty";
        var map = ReadOnlyMap.Copy(data);
        var pipe = new Pipe();
        var serializer = BuildSerializer2();
        await serializer.SerializeAsync(map, pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.True(map.Equals(result));
    }

    [Fact]
    public async Task TestNestedArraySerialization()
    {
        var data = new Dictionary<Guid, ReadOnlyArray<int>>();
        data[Guid.NewGuid()] = ReadOnlyArray.Empty<int>();
        data[Guid.NewGuid()] = ReadOnlyArray.Move(new int[] { 1, 2, 3 });
        data[Guid.Empty] = ReadOnlyArray.Move(new int[] { 1024 });
        var map = ReadOnlyMap.Copy(data);
        var pipe = new Pipe();
        var serializer = BuildSerializer3();
        await serializer.SerializeAsync(map, pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.True(map.Equals(result));
    }
}
