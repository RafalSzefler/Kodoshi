using System.Collections;
using System.IO.Pipelines;
using Kodoshi.Core.BuiltIns;
using Kodoshi.Core.Tests.Extensions;

namespace Kodoshi.Core.Tests;

public class ArraySerializationTestsData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] {
            new int[] { 123456, -13, 0, -256, 12 },
            new byte[] { 136, 0, 9, 143, 153, 128, 127, 131, 152 },
        };
        yield return new object[] {
            new int[] { 0, 1, 2, 3 },
            new byte[] { 132, 128, 130, 132, 134 },
        };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class ArraySerializationTests
{
    private ISerializer<ReadOnlyArray<int>> BuildIntArraySerializer()
    {
        var numericSerializer = new NumericSerializer();
        return new ReadOnlyArraySerializer<int>(numericSerializer, numericSerializer);
    }

    private ISerializer<ReadOnlyArray<long>> BuildLongArraySerializer()
    {
        var numericSerializer = new NumericSerializer();
        return new ReadOnlyArraySerializer<long>(numericSerializer, numericSerializer);
    }

    [Fact]
    public async Task TestSerialization()
    {
        var data = new int[] { 0, 7, 11, 1024, 10000 };
        var pipe = new Pipe();
        var serializer = BuildIntArraySerializer();
        await serializer.SerializeAsync(ReadOnlyArray.Copy(data), pipe.Writer, default);
        var result = await serializer.DeserializeAsync(pipe.Reader, default);
        Assert.True(result.AsMemory().Span.SequenceEqual(data));
    }

    [Fact]
    public async Task TestSerializationBigData()
    {
        const int size = 1 << 16;
        var data = new long[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = i;
        }
        using var stream = new MemoryStream();
        var pipeWriter = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        var serializer = BuildLongArraySerializer();
        await serializer.SerializeAsync(ReadOnlyArray.Copy(data), pipeWriter, default);
        await pipeWriter.FlushAsync();
        await pipeWriter.CompleteAsync();
        stream.Seek(0, SeekOrigin.Begin);
        var pipeReader = PipeReader.Create(stream);
        var result = await serializer.DeserializeAsync(pipeReader, default);
        Assert.True(result.AsMemory().Span.SequenceEqual(data));
    }

    [Theory]
    [ClassData(typeof(ArraySerializationTestsData))]
    public async Task TestConcreteSerialization(int[] input, byte[] expectedOutput)
    {
        var arr = ReadOnlyArray.Copy(input);
        var serializer = BuildIntArraySerializer();
        var result = await serializer.SerializeToArray(arr);
        Assert.True(expectedOutput.AsSpan().SequenceEqual(result));
    }
}
