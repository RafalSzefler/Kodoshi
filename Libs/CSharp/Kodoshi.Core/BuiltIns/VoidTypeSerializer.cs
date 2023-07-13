using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class VoidTypeSerializer : ISerializer<VoidType>
    {
        public static VoidTypeSerializer Instance { get; } = new VoidTypeSerializer();

        private VoidTypeSerializer()
        { }

        public ValueTask<VoidType> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
            => new ValueTask<VoidType>(VoidType.Instance);

        public ValueTask SerializeAsync(VoidType instance, PipeWriter pipeWriter, CancellationToken ct)
            => new ValueTask();
    }
}
