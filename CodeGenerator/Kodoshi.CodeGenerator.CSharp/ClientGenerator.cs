using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.CSharp;

internal sealed class ClientGenerator
{
    private readonly InputContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public ClientGenerator(
        InputContext inputContext,
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
