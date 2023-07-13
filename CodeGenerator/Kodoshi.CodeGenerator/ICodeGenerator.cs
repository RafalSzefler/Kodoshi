using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator;

public interface ICodeGenerator
{
    public string Name { get; }
    public string Version { get; }
    public ValueTask GenerateFromContext(
        ProjectContext context,
        CancellationToken ct);
}
