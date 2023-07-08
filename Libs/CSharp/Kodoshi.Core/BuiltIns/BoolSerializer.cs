using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.Core.Exceptions;

namespace Kodoshi.Core.BuiltIns
{
    public sealed class BoolSerializer : ISerializer<bool>
    {
        private readonly NumericSerializer _numericSerializer;

        public BoolSerializer(NumericSerializer numericSerializer)
        {
            _numericSerializer = numericSerializer;
        }

        public ValueTask SerializeAsync(bool instance, PipeWriter pipeWriter, CancellationToken ct)
            => ((ISerializer<byte>)_numericSerializer).SerializeAsync((byte)(instance ? 1 : 0), pipeWriter, ct);

        public async ValueTask<bool> DeserializeAsync(PipeReader pipeReader, CancellationToken ct)
        {
            var result = await ((ISerializer<byte>)_numericSerializer).DeserializeAsync(pipeReader, ct);
            switch (result)
            {
                case 0: return false;
                case 1: return true;
                default: throw new InvalidBoolValueException();
            }
        }
    }
}
