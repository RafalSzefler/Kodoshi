using System.Collections;
using System.IO.Pipelines;
using Kodoshi.Core.BuiltIns;

namespace Kodoshi.Core.Tests;

public class DoubleSerializationTestsData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var rng = new Random();
        for (var i = 0; i < 100; i++)
            yield return new object[] { rng.NextDouble() };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class DoubleSerializationTests
{
    [Theory]
    [ClassData(typeof(DoubleSerializationTestsData))]
    public async Task TestSerialization(double data)
    {
        var pipe = new Pipe();
        var serializer = new DoubleSerializer();
        var t1 = serializer.SerializeAsync(data, pipe.Writer, default);
        var t2 = serializer.DeserializeAsync(pipe.Reader, default);

        await t1;
        var result = await t2;
        Assert.Equal(data, result);
    }
}
