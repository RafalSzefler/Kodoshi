using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kodoshi.CodeGenerator.CSharp.Client;

internal sealed class ClientFileBuilderGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public ClientFileBuilderGenerator(
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
        var compilationUnit = BuildBaseClientFile(ct)
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);

        var folder = await _context.ClientFolder;
        var file = await folder.CreateFile("RPCClientBuilder.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildBaseClientFile(CancellationToken ct)
    {
        var code = $@"
using System;
using System.Net.Http;
using Kodoshi.Core;

namespace NAMESPACE
{{
    public sealed class RPCClientBuilder
    {{
        private Uri? _apiUri;
        private HttpClient? _httpClient;
        private ISerializerCollection? _serializerCollection;

        public RPCClientBuilder SetApiUri(Uri uri)
        {{
            _apiUri = uri;
            return this;
        }}

        public RPCClientBuilder SetHttpClient(HttpClient client)
        {{
            _httpClient = client;
            return this;
        }}

        public RPCClientBuilder SetSerializerCollection(ISerializerCollection serializerCollection)
        {{
            _serializerCollection = serializerCollection;
            return this;
        }}

        public RPCClient Build()
        {{
            if (_apiUri is null)
                throw new ArgumentNullException($""ApiUri cannot be null."");

            var httpClient = _httpClient ?? new HttpClient();
            var serializerCollection = _serializerCollection ?? new {_context.CoreNamespace}.SerializerCollectionBuilder().Build();
            return new RPCClient(httpClient, _apiUri, serializerCollection);
        }}
    }}
}}";
        var unit = SyntaxFactory.ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(_context.ClientNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        return unit.ReplaceNode(nmspc, newNmspc);
    }
}