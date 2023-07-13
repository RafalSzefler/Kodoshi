using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kodoshi.CodeGenerator.CSharp.Models;

internal sealed class ModelsGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public ModelsGenerator(
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
        await Task.Yield();
        var modelsFileGenerator = new ModelsFileGenerator(_intputContext, _context, _helpers);
        var defaultValuesFileGenerator = new DefaultValuesFileGenerator(_intputContext, _context, _helpers);
        var serializerFileGenerator = new SerializerFileGenerator(_intputContext, _context, _helpers);
        var serializationHelpersFile = new SerializationHelpersFile(_intputContext, _context, _helpers);
        var tasks = new Task[]
        {
            GenerateCSProj(ct),
            modelsFileGenerator.Generate(ct),
            defaultValuesFileGenerator.Generate(ct),
            serializerFileGenerator.Generate(ct),
            serializationHelpersFile.Generate(ct),
        };
        await Task.WhenAll(tasks);
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
            .Append("  </PropertyGroup>\n\n")
            .Append("  <ItemGroup>\n")
            .Append("    <PackageReference Include=\"System.IO.Pipelines\" Version=\"6.*-*\" />\n")
            .Append("    <PackageReference Include=\"System.Collections.Immutable\" Version=\"8.*-*\" />\n")
            .Append("    <PackageReference Include=\"Kodoshi.Core\" Version=\"1.*-*\" />\n")
            .Append("  </ItemGroup>\n\n")
            .Append("</Project>\n")
            .ToString();
        var folder = await _context.ModelsFolder;
        var file = await folder.CreateFile(_context.ModelsNamespace + ".csproj", ct);
        await file.Write(Encoding.UTF8.GetBytes(content), ct);
    }
}
