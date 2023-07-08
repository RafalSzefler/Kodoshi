using System.IO.Pipelines;
using Kodoshi.Core.Tests.Extensions;
using Kodoshi.Core.BuiltIns;

namespace Kodoshi.Core.Tests;

public class ByteArraySerializationTests
{
    private ISerializer<ReadOnlyArray<byte>> BuildSerializer() => new ByteArraySerializer(new NumericSerializer());

    [Fact]
    public async Task TestSerialization()
    {
        var data = new byte[] { 0, 7, 11, 3, 128, 132 };
        var pipe = new Pipe();
        var serializer = BuildSerializer();
        await serializer.SerializeAsync(ReadOnlyArray.Copy(data), pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.True(result.AsMemory().Span.SequenceEqual(data));
    }

    [Fact]
    public async Task TestConcreteSerialization()
    {
        var data = new byte[] { 0, 7, 11, 3, 128, 132 };
        var arr = ReadOnlyArray.Copy(data);
        var serializer = BuildSerializer();
        var result = await serializer.SerializeToArray(arr);
        var expected = new byte[] { 0b10000110, 0, 7, 11, 3, 128, 132 };
        Assert.True(expected.AsSpan().SequenceEqual(result));
    }

    [Fact]
    public async Task TestConcreteSerializationBigger()
    {
        const int size = 200;
        var data = new byte[size];
        var expected = new byte[size+2];
        expected[0] = 72;
        expected[1] = 129;
        for (var i = 0; i < size; i++)
        {
            data[i] = (byte)i;
            expected[i+2] = (byte)i;
        }

        var arr = ReadOnlyArray.Copy(data);
        var serializer = BuildSerializer();
        var result = await serializer.SerializeToArray(arr);
        Assert.True(expected.AsSpan().SequenceEqual(result));
    }
}
