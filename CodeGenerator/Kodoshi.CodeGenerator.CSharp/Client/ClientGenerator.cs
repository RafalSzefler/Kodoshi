using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.CSharp.Client;

internal sealed class ClientGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public ClientGenerator(
        ProjectContext inputContext,
        GenerationContext context,
        Helpers helpers)
    {
        _intputContext = inputContext;
        _context = context;
        _helpers = helpers;
    }

    public Task Generate(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
