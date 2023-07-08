using System.Collections;
using System.IO.Pipelines;
using Kodoshi.Core.BuiltIns;

namespace Kodoshi.Core.Tests;

public class IntegerSerializationTestsData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        for (var i = -1500; i < 1500; i++)
            yield return new object[] { 3*i };
        yield return new object[] { short.MaxValue };
        yield return new object[] { short.MinValue };
        yield return new object[] { int.MaxValue };
        yield return new object[] { int.MinValue };
        yield return new object[] { long.MaxValue };
        yield return new object[] { long.MinValue };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class IntegerSerializationTests
{
    [Theory]
    [ClassData(typeof(IntegerSerializationTestsData))]
    public async Task TestSerialization(long data)
    {
        var pipe = new Pipe();
        var serializer = (ISerializer<long>)new NumericSerializer();
        var t1 = serializer.SerializeAsync(data, pipe.Writer, default);
        var t2 = serializer.DeserializeAsync(pipe.Reader, default);

        await t1;
        var result = await t2;
        Assert.Equal(data, result);
    }

    [Fact]
    public async Task TestChaining()
    {
        var serializer = (ISerializer<int>)new NumericSerializer();
        var pipe = new Pipe();

        {
            var no = 3;
            for (var i = 0; i < 5; i++)
            {
                await serializer.SerializeAsync(no, pipe.Writer, default);
                no = unchecked(no << 6);
            }
        }

        var results = new int[5];
        var expected = new int[5];

        {
            var no = 3;
            for (var i = 0; i < 5; i++)
            {
                results[i] = await serializer.DeserializeAsync(pipe.Reader, default);
                expected[i] = no;
                no = unchecked(no << 6);
            }
        }

        Assert.Equal(expected, results);
    }
}