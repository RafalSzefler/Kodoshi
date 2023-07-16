using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kodoshi.CodeGenerator.CSharp.Server;

internal sealed class RPCBuilderFileGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public RPCBuilderFileGenerator(
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
        var compilationUnit = BuildBuilderClass(ct)
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);

        var folder = await _context.ServerFolder;
        var file = await folder.CreateFile("RPCBuilder.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildBuilderClass(CancellationToken ct)
    {
        var allInterfaces = string.Join(", ", _intputContext.Project.Services.Select(
            x => $"typeof({_helpers.TransformServiceDefinitionToInterfaceSyntax(x)})"));
        var code = $@"
namespace NAMESPACE
{{
    public sealed class RPCBuilder
    {{
        private readonly string _path;
        private System.Collections.Generic.Dictionary<System.Type, System.Type>? _scannedTypes;

        public RPCBuilder(string _path = ""/rpc"")
        {{
            this._path = _path;
        }}

        public void ScanForHandlers(System.Reflection.Assembly? _asm = null)
        {{
            if (_asm is null)
                _asm = System.Reflection.Assembly.GetCallingAssembly();
            var _handlerTypes = new System.Collections.Generic.HashSet<System.Type>()
                {{ {allInterfaces} }};
            var _foundTypes = new System.Collections.Generic.Dictionary<System.Type, System.Type>();
            foreach (var type in _asm.GetTypes())
            {{
                foreach (var @interface in type.GetInterfaces())
                {{
                    if (_handlerTypes.Contains(@interface))
                    {{
                        if (_foundTypes.ContainsKey(@interface))
                            throw new System.ArgumentException($""Found multiple implementations for interface {{@interface.FullName}}"");
                        _foundTypes[@interface] = type;
                        _handlerTypes.Remove(@interface);
                    }}
                }}
            }}
            if (_handlerTypes.Count > 0)
            {{
                var types = string.Join("", "", System.Linq.Enumerable.Select(_handlerTypes, x => x.FullName));
                throw new System.ArgumentException($""Following interces require implementation: {{types}}"");
            }}
            this._scannedTypes = _foundTypes;
        }}

        public void ApplyToServiceCollection(Microsoft.Extensions.DependencyInjection.IServiceCollection _serviceCollection)
        {{
            var _serializersCollection = new {_context.CoreNamespace}.SerializerCollectionBuilder()
                .Build();

            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Kodoshi.Core.ISerializerCollection>(_serviceCollection, _serializersCollection);
            if (_scannedTypes is not null)
                foreach (var (@interface, type) in _scannedTypes)
                    Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient(_serviceCollection, @interface, type);
        }}

        public void ApplyToWebApplication(Microsoft.AspNetCore.Builder.WebApplication _app)
        {{
            var _handler = new RPCHandler(_path);
            Microsoft.AspNetCore.Builder.UseExtensions.Use(_app, _handler.Handle);
        }}
    }}
}}";
        var unit = SyntaxFactory.ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(_context.ServerNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        return unit.ReplaceNode(nmspc, newNmspc);
    }
}
