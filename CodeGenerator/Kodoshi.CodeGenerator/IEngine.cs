using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator;

public interface IEngine
{
    public ValueTask Run(CancellationToken ct);
}
