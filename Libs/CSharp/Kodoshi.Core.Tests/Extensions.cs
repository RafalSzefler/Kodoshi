using System.IO.Pipelines;

namespace Kodoshi.Core.Tests.Extensions;

public static class Extensions
{
    public static async ValueTask<byte[]> SerializeToArray<T>(this ISerializer<T> serializer, T instance)
        where T : IEquatable<T>
    {
        byte[] result;
        using (var memory = new MemoryStream())
        {
            var writer = PipeWriter.Create(memory);
            await serializer.SerializeAsync(instance, writer, default);
            await writer.FlushAsync();
            await memory.FlushAsync();
            memory.Seek(0, SeekOrigin.Begin);
            result = memory.ToArray();
        }
        return result;
    }
}