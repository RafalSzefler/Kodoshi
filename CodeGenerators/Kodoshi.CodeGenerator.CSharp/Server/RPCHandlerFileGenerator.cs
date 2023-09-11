using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Kodoshi.CodeGenerator.CSharp.Server;

internal sealed class RPCHandlerFileGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public RPCHandlerFileGenerator(
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
        var compilationUnit = BuildHandlerClass(ct)
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);

        var folder = await _context.ServerFolder;
        var file = await folder.CreateFile("RPCHandler.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildHandlerClass(CancellationToken ct)
    {
        var code = $@"
namespace NAMESPACE
{{
    internal sealed class RPCHandler
    {{
        private readonly string _path;

        public RPCHandler(string _path)
        {{
            this._path = _path;
        }}

        public async System.Threading.Tasks.Task Handle(Microsoft.AspNetCore.Http.HttpContext _context, System.Func<System.Threading.Tasks.Task> _next)
        {{
            if (_context.Request.Path != this._path)
            {{
                await _next().ConfigureAwait(false);
                return;
            }}

            if (_context.Request.Method != Microsoft.AspNetCore.Http.HttpMethods.Post)
            {{
                _context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status405MethodNotAllowed;
                return;
            }}

            var _serializersCollection = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Kodoshi.Core.ISerializerCollection>(_context.RequestServices);

            var _serializer = _serializersCollection.GetSerializer<REQUEST_TYPE>();
            REQUEST_TYPE _req;

            try
            {{
                _req = await _serializer.DeserializeAsync(_context.Request.BodyReader, _context.RequestAborted).ConfigureAwait(false);
            }}
            catch (Kodoshi.Core.Exceptions.BaseException exc)
            {{
                var _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILogger<RPCHandler>>(_context.RequestServices);
                if (_logger is not null)
                    Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(_logger, exc, ""RPCHandler: deserialization error, returning 400."");
                _context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest;
                return;
            }}

            try
            {{
                await InternalHandle(_context, _serializersCollection, _req).ConfigureAwait(false);
            }}
            catch (System.Exception exc)
            {{
                var _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILogger<RPCHandler>>(_context.RequestServices);
                if (_logger is not null)
                    Microsoft.Extensions.Logging.LoggerExtensions.LogError(_logger, exc, ""RPCHandler: unhandled exception on RPC request."");
                _context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError;
                return;
            }}
        }}

        private static async System.Threading.Tasks.Task InternalHandle(Microsoft.AspNetCore.Http.HttpContext _context, Kodoshi.Core.ISerializerCollection _serializersCollection, {_context.ModelsNamespace}._Services.Request _req)
        {{ }}
    }}
}}";
        var unit = ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;

        var _firstToReplace = nmspc
            .Members.Where(m => m is ClassDeclarationSyntax)
            .SelectMany(m => ((ClassDeclarationSyntax)m).Members).Where(m => m is MethodDeclarationSyntax method && method.Identifier.ToFullString() == "Handle")
            .SelectMany(m => ((MethodDeclarationSyntax)m).Body!.Statements)
            .Where(m => m is LocalDeclarationStatementSyntax)
            .Take(2).Last();
        
        var requestTagType = _helpers.TransformModelDefinitionToSyntax(_context.RequestsTag!);

        var _firstReplacement = LocalDeclarationStatement(
                VariableDeclaration(
                    IdentifierName(
                        Identifier(
                            TriviaList(),
                            SyntaxKind.VarKeyword,
                            "var",
                            "var",
                            TriviaList())))
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier("_serializer"))
                        .WithInitializer(
                            EqualsValueClause(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("_serializersCollection"),
                                        GenericName(
                                            Identifier("GetSerializer"))
                                        .WithTypeArgumentList(
                                            TypeArgumentList(
                                                SingletonSeparatedList<TypeSyntax>(
                                                   requestTagType))))))))));
        nmspc = nmspc.ReplaceNode(_firstToReplace, _firstReplacement);

        var _secondToReplace = nmspc
            .Members.Where(m => m is ClassDeclarationSyntax)
            .SelectMany(m => ((ClassDeclarationSyntax)m).Members).Where(m => m is MethodDeclarationSyntax method && method.Identifier.ToFullString() == "Handle")
            .SelectMany(m => ((MethodDeclarationSyntax)m).Body!.Statements)
            .Where(m => m is LocalDeclarationStatementSyntax)
            .Take(3).Last();
        var _secondReplacement = LocalDeclarationStatement(
                VariableDeclaration(requestTagType)
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier("_req")))));
        
        nmspc = nmspc.ReplaceNode(_secondToReplace, _secondReplacement);

        nmspc = UpdateInternalHandle(nmspc);

        var newNmspc = NamespaceDeclaration(
                ParseName(_context.ServerNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);

        var members = new List<MemberDeclarationSyntax>() { newNmspc };
        return CompilationUnit().WithMembers(List<MemberDeclarationSyntax>(members));
    }

    private NamespaceDeclarationSyntax UpdateInternalHandle(NamespaceDeclarationSyntax nmspc)
    {
         var _method = nmspc
            .Members.Where(m => m is ClassDeclarationSyntax)
            .SelectMany(m => ((ClassDeclarationSyntax)m).Members).Where(m => m is MethodDeclarationSyntax method && method.Identifier.ToFullString() == "InternalHandle")
            .First();
        var body = ((MethodDeclarationSyntax)_method).Body!;

        var switchSections = new List<SwitchSectionSyntax>();
        var requestTag = _context.RequestsTag!;
        var staticName = _helpers.TransformModelDefinitionToSyntax(requestTag);
        var servicesById = _intputContext.Project.Services.ToDictionary(
            x => x.Id, x => x);

        foreach (var field in requestTag.Fields)
        {
            if (field.Value == 0) continue;
            var service = servicesById[field.Value];
            var serviceType = _helpers.TransformServiceDefinitionToInterfaceSyntax(service);
            var statements = new List<StatementSyntax>();
            statements.Add(
                LocalDeclarationStatement(
                VariableDeclaration(
                    IdentifierName(
                        Identifier(
                            TriviaList(),
                            SyntaxKind.VarKeyword,
                            "var",
                            "var",
                            TriviaList())))
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier("_service"))
                        .WithInitializer(
                            EqualsValueClause(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("Microsoft"),
                                                    IdentifierName("Extensions")),
                                                IdentifierName("DependencyInjection")),
                                            IdentifierName("ServiceProviderServiceExtensions")),
                                        GenericName(
                                            Identifier("GetRequiredService"))
                                        .WithTypeArgumentList(
                                            TypeArgumentList(
                                                SingletonSeparatedList<TypeSyntax>(
                                                    serviceType)))))
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList<ArgumentSyntax>(
                                            Argument(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("_context"),
                                                    IdentifierName("RequestServices"))))))))))));
            statements.Add(
                LocalDeclarationStatement(
                VariableDeclaration(
                    IdentifierName(
                        Identifier(
                            TriviaList(),
                            SyntaxKind.VarKeyword,
                            "var",
                            "var",
                            TriviaList())))
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier("_result"))
                        .WithInitializer(
                            EqualsValueClause(
                                AwaitExpression(
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("_service"),
                                            IdentifierName("HandleAsync")))
                                    .WithArgumentList(
                                        ArgumentList(
                                            SeparatedList<ArgumentSyntax>(
                                                new SyntaxNodeOrToken[]{
                                                    Argument(
                                                        InvocationExpression(
                                                            MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                IdentifierName("_req"),
                                                                IdentifierName($"Get{field.Name}")))),
                                                    Token(SyntaxKind.CommaToken),
                                                    Argument(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName("_context"),
                                                            IdentifierName("RequestAborted")))}))))))))));

            var responseTypeSyntax = _helpers.TransformModelReferenceToSyntax(service.Output);
            statements.Add(
                            LocalDeclarationStatement(
                VariableDeclaration(
                    IdentifierName(
                        Identifier(
                            TriviaList(),
                            SyntaxKind.VarKeyword,
                            "var",
                            "var",
                            TriviaList())))
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier("_resultSerializer"))
                        .WithInitializer(
                            EqualsValueClause(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("_serializersCollection"),
                                        GenericName(
                                            Identifier("GetSerializer"))
                                        .WithTypeArgumentList(
                                            TypeArgumentList(
                                                SingletonSeparatedList<TypeSyntax>(
                                                    responseTypeSyntax)))))))))));

            statements.Add(
                ExpressionStatement(
                    AwaitExpression(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("_resultSerializer"),
                                IdentifierName("SerializeAsync")))
                        .WithArgumentList(
                            ArgumentList(
                                SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        Argument(
                                            IdentifierName("_result")),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("_context"),
                                                    IdentifierName("Response")),
                                                IdentifierName("BodyWriter"))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("_context"),
                                                IdentifierName("RequestAborted")))}))))));

            statements.Add(ReturnStatement());

            switchSections.Add(
                SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            CaseSwitchLabel(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    staticName,
                                    IdentifierName($"Values.{field.Name}")))))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            Block(statements))));
        }

        switchSections.Add(
            SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        Block(
                            ExpressionStatement(
                                AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("_context"),
                                            IdentifierName("Response")),
                                        IdentifierName("StatusCode")),
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("Microsoft"),
                                                    IdentifierName("AspNetCore")),
                                                IdentifierName("Http")),
                                            IdentifierName("StatusCodes")),
                                        IdentifierName("Status400BadRequest")))),
                            ReturnStatement()))));

        var newBody = Block(
            SingletonList<StatementSyntax>(
                SwitchStatement(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("_req"),
                        IdentifierName("Tag")))
                .WithSections(List(switchSections))));
        return nmspc.ReplaceNode(body, newBody);
    }
}
