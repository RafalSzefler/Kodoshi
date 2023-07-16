using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Kodoshi.CodeGenerator.CSharp.Client;

internal sealed class ClientFileGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public ClientFileGenerator(
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
        var file = await folder.CreateFile("RPCClient.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildBaseClientFile(CancellationToken ct)
    {
        var code = $@"
namespace NAMESPACE
{{
    public sealed class RPCClient
    {{
        private static readonly System.IO.Pipelines.StreamPipeWriterOptions _writerOptions = new System.IO.Pipelines.StreamPipeWriterOptions(leaveOpen: true);
        private readonly System.Net.Http.HttpClient _http;
        private readonly System.Uri _apiUri;
        private readonly Kodoshi.Core.ISerializerCollection _serializerCollection;
        internal RPCClient(System.Net.Http.HttpClient httpClient, System.Uri apiUri, Kodoshi.Core.ISerializerCollection serializerCollection)
        {{
            this._http = httpClient;
            this._apiUri = apiUri;
            this._serializerCollection = serializerCollection;
        }}

        private async System.Threading.Tasks.Task<TResp> CallAsync<TReq, TResp>(TReq instance, System.Threading.CancellationToken ct)
            where TReq : System.IEquatable<TReq> where TResp : System.IEquatable<TResp>
        {{
            var serializer = _serializerCollection.GetSerializer<TReq>();
            System.Net.Http.HttpResponseMessage resp;
            {{
                using (var stream = new System.IO.MemoryStream())
                {{
                    var writer = System.IO.Pipelines.PipeWriter.Create(stream, _writerOptions);
                    await serializer.SerializeAsync(instance, writer, ct).ConfigureAwait(false);
                    await writer.FlushAsync(ct).ConfigureAwait(false);
                    await writer.CompleteAsync().ConfigureAwait(false);
                    var size = (int)stream.Position;
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    var content = new System.Net.Http.StreamContent(stream, size);
                    resp = await _http.PostAsync(_apiUri, content).ConfigureAwait(false);
                }}
            }}

            resp.EnsureSuccessStatusCode();
            {{
                var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var reader = System.IO.Pipelines.PipeReader.Create(stream);
                var deserializer = _serializerCollection.GetSerializer<TResp>();
                return await deserializer.DeserializeAsync(reader, ct).ConfigureAwait(false);
            }}
        }}
        
    }}
}}";
        var unit = SyntaxFactory.ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var client = (ClassDeclarationSyntax)nmspc.Members.First();

        var newMembers = client.Members;

        foreach (var service in _intputContext.Project.Services)
        {
            var methodName = ServiceHelpers.ServiceIdentifierToTag(service.FullName);
            var inputType = _helpers.TransformModelReferenceToSyntax(service.Input);
            var outputType = _helpers.TransformModelReferenceToSyntax(service.Output);
            var requestType = _helpers.TransformModelDefinitionToSyntax(_context.RequestsTag!);
            var methodDeclaration = MethodDeclaration(
                    QualifiedName(
                        QualifiedName(
                            QualifiedName(
                                IdentifierName("System"),
                                IdentifierName("Threading")),
                            IdentifierName("Tasks")),
                        GenericName(
                            Identifier("Task"))
                        .WithTypeArgumentList(
                            TypeArgumentList(
                                SingletonSeparatedList<TypeSyntax>(
                                    outputType)))),
                    Identifier($"Call{methodName}"))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(
                    ParameterList(
                        SeparatedList<ParameterSyntax>(
                            new SyntaxNodeOrToken[]{
                                Parameter(
                                    Identifier("instance"))
                                .WithType(inputType),
                                Token(SyntaxKind.CommaToken),
                                Parameter(
                                    Identifier("ct"))
                                .WithType(
                                    QualifiedName(
                                        QualifiedName(
                                            IdentifierName("System"),
                                            IdentifierName("Threading")),
                                        IdentifierName("CancellationToken")))})))
                .WithExpressionBody(
                    ArrowExpressionClause(
                        InvocationExpression(
                            GenericName(
                                Identifier("CallAsync"))
                            .WithTypeArgumentList(
                                TypeArgumentList(
                                    SeparatedList<TypeSyntax>(
                                        new SyntaxNodeOrToken[]{
                                            requestType,
                                            Token(SyntaxKind.CommaToken),
                                            outputType}))))
                        .WithArgumentList(
                            ArgumentList(
                                SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        Argument(
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    requestType,
                                                    IdentifierName($"Create{methodName}")))
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SingletonSeparatedList<ArgumentSyntax>(
                                                        Argument(
                                                            IdentifierName("instance")))))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(
                                            IdentifierName("ct"))})))))
                .WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken));
            
            newMembers = newMembers.Add(methodDeclaration);
        }

        var newNmspcMembers = new List<MemberDeclarationSyntax>();
        newNmspcMembers.Add(
            ClassDeclaration(client.Identifier)
                .WithModifiers(client.Modifiers)
                .WithMembers(newMembers));
        var newNmspc = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(_context.ClientNamespace))
            .WithMembers(List(newNmspcMembers))
            .WithNamespaceKeyword(Helpers.TopComment);
        return unit.ReplaceNode(nmspc, newNmspc);
    }
}