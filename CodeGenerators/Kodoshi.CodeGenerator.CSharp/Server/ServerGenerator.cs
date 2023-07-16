using System.Text;
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

    public async Task Generate(CancellationToken ct)
    {
        if (!_context.CreateServer)
        {
            return;
        }
        await Task.Yield();

        var rpcBuilderFileGenerator = new RPCBuilderFileGenerator(_intputContext, _context, _helpers);
        var rpcHandlerFileGenerator = new RPCHandlerFileGenerator(_intputContext, _context, _helpers);
        var interfacesFileGenerator = new InterfacesFileGenerator(_intputContext, _context, _helpers);
        var tasks = new Task[]
        {
            GenerateCSProj(ct),
            rpcBuilderFileGenerator.Generate(ct),
            rpcHandlerFileGenerator.Generate(ct),
            interfacesFileGenerator.Generate(ct),
        };
        foreach (var task in tasks)
        {
            await task;
        }
    }

    private async Task GenerateCSProj(CancellationToken ct)
    {
        await Task.Yield();
        var content = new StringBuilder()
            .Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n")
            .Append("  <PropertyGroup>\n")
            .Append("    <OutputType>Library</OutputType>\n")
            .Append("    <Version>").Append(_intputContext.Project.Version).Append("</Version>\n")
            .Append("    <TargetFramework>net7.0</TargetFramework>\n")
            .Append("    <LangVersion>9.0</LangVersion>\n")
            .Append("    <ImplicitUsings>disable</ImplicitUsings>\n")
            .Append("    <Nullable>enable</Nullable>\n")
            .Append("    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n")
            .Append("    <InvariantGlobalization>true</InvariantGlobalization>\n")
            .Append("  </PropertyGroup>\n\n")
            .Append("  <ItemGroup>\n")
            .Append("    <FrameworkReference Include=\"Microsoft.AspNetCore.App\" />\n")
            .Append($"    <ProjectReference Include=\"..\\{_context.ModelsNamespace}\\{_context.ModelsNamespace}.csproj\" />\n")
            .Append("  </ItemGroup>\n\n")
            .Append("</Project>\n")
            .ToString();
        var folder = await _context.ServerFolder;
        var file = await folder.CreateFile(_context.ServerNamespace + ".csproj", ct);
        await file.Write(Encoding.UTF8.GetBytes(content), ct);
    }
}
