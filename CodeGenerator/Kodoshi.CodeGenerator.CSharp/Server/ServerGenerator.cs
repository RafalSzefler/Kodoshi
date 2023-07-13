using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.CSharp.Server;

internal sealed class ServerGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public ServerGenerator(
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
