using System.Collections;
using System.IO.Pipelines;
using Kodoshi.Core.BuiltIns;

namespace Kodoshi.Core.Tests;

public class FloatSerializationTestsData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var rng = new Random();
        for (var i = 0; i < 100; i++)
            yield return new object[] { rng.NextSingle() };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class FloatSerializationTests
{
    [Theory]
    [ClassData(typeof(FloatSerializationTestsData))]
    public async Task TestSerialization(float data)
    {
        var pipe = new Pipe();
        var serializer = new FloatSerializer();
        var t1 = serializer.SerializeAsync(data, pipe.Writer, default);
        var t2 = serializer.DeserializeAsync(pipe.Reader, default);

        await t1;
        var result = await t2;
        Assert.Equal(data, result);
    }
}
