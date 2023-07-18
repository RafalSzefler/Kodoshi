using System.Text;
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

    public async Task Generate(CancellationToken ct)
    {
        if (!_context.CreateClient)
        {
            return;
        }
        await Task.Yield();

        var clientFileGenerator = new ClientFileGenerator(_intputContext, _context, _helpers);
        var clientFileBuilderGenerator = new ClientFileBuilderGenerator(_intputContext, _context, _helpers);
        var tasks = new Task[]
        {
            GenerateCSProj(ct),
            clientFileGenerator.Generate(ct),
            clientFileBuilderGenerator.Generate(ct),
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
            .Append("    <TargetFramework>netstandard2.1</TargetFramework>\n")
            .Append("    <LangVersion>9.0</LangVersion>\n")
            .Append("    <ImplicitUsings>disable</ImplicitUsings>\n")
            .Append("    <Nullable>enable</Nullable>\n")
            .Append("    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n")
            .Append("    <InvariantGlobalization>true</InvariantGlobalization>\n")
            .Append("    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>\n")
            .Append("  </PropertyGroup>\n\n")
            .Append("  <ItemGroup>\n")
            .Append($"    <ProjectReference Include=\"..\\{_context.ModelsNamespace}\\{_context.ModelsNamespace}.csproj\" />\n")
            .Append("  </ItemGroup>\n\n")
            .Append("</Project>\n")
            .ToString();
        var folder = await _context.ClientFolder;
        var file = await folder.CreateFile(_context.ClientNamespace + ".csproj", ct);
        await file.Write(Encoding.UTF8.GetBytes(content), ct);
    }
}
