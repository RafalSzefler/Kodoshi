using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator;

public interface IProjectLoader
{
    public ValueTask<ProjectContext> Parse(
        IInputContext context,
        CancellationToken ct);
}
