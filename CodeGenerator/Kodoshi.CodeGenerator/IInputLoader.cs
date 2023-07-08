using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator;

public interface IInputLoader
{
    public ValueTask<InputContext> Parse(
        IFolder inputFolder,
        CancellationToken ct);
}
