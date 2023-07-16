using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kodoshi.CodeGenerator.CSharp.Models;

internal sealed class BuildInfoGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public BuildInfoGenerator(
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
        var compilationUnit = BuildDefaultValuesClass(ct)
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);

        var folder = await _context.ModelsFolder;
        var file = await folder.CreateFile("BuildInfo.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildDefaultValuesClass(CancellationToken ct)
    {
        var code = $@"
namespace NAMESPACE
{{
    public static class BuildInfo
    {{
        public static string Hash {{ get; }} = ""{_intputContext.Project.Build}"";
    }}
}}";
        var unit = SyntaxFactory.ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(_context.CoreNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        return unit.ReplaceNode(nmspc, newNmspc);
    }
}
