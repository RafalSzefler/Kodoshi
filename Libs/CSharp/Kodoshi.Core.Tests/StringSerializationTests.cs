using System.IO.Pipelines;
using Kodoshi.Core.Tests.Extensions;
using Kodoshi.Core.BuiltIns;

namespace Kodoshi.Core.Tests;

public class StringSerializationTests
{
    private ISerializer<string> BuildSerializer()
    {
        return new StringSerializer(new ByteArraySerializer(new NumericSerializer()));
    }

    [Fact]
    public async Task TestSerialization()
    {
        var data = "abcd";
        var pipe = new Pipe();
        var serializer = BuildSerializer();
        await serializer.SerializeAsync(data, pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.Equal(data, result);
    }

    [Fact]
    public async Task TestLongerSerializationWithUTF()
    {
        var data = "abąćdęfg".PadRight(300, ';');
        var pipe = new Pipe();
        var serializer = BuildSerializer();
        await serializer.SerializeAsync(data, pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.Equal(data, result);
    }

    
    [Fact]
    public async Task TestConcreteSerialization()
    {
        var data = "abąćdęfg";
        var serializer = BuildSerializer();
        var result = await serializer.SerializeToArray(data);
        var expected = new byte[] { 139, 97, 98, 196, 133, 196, 135, 100, 196, 153, 102, 103 };
        Assert.Equal(expected, result);
    }
}
