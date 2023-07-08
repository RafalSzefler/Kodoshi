using System.Collections;
using System.IO.Pipelines;
using Kodoshi.Core.BuiltIns;
using Kodoshi.Core.Tests.Extensions;

namespace Kodoshi.Core.Tests;

public class GuidSerializationTestsData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] { Guid.Empty };
        for (var i = 0; i < 100; i++)
            yield return new object[] { Guid.NewGuid() };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class GuidSerializationTests
{
    [Theory]
    [ClassData(typeof(GuidSerializationTestsData))]
    public async Task TestSerialization(Guid data)
    {
        var pipe = new Pipe();
        var serializer = new GuidSerializer();
        await serializer.SerializeAsync(data, pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.Equal(data, result);
    }

    [Fact]
    public async Task TestSize()
    {
        var data = Guid.NewGuid();
        var serializer = new GuidSerializer();
        var result = await serializer.SerializeToArray(data);
        Assert.Equal(16, result.Length);
    }

    [Fact]
    public async Task TestConcreteSerialization()
    {
        var expected = new byte[] { 0x29, 0xe9, 0x21, 0x21, 0xf7, 0x39, 0x4a, 0x14, 0xb1, 0x47, 0xd4, 0x93, 0xc9, 0x40, 0xa1, 0xc9 };
        var data = new Guid(expected);
        var serializer = new GuidSerializer();
        var result = await serializer.SerializeToArray(data);
        Assert.Equal(expected, result);
    }
}
