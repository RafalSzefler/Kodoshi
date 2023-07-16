using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.Entities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Kodoshi.CodeGenerator.CSharp.Server;

internal sealed class InterfacesFileGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public InterfacesFileGenerator(
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
        var compilationUnit = BuildInterfaces(ct)
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);

        var folder = await _context.ServerFolder;
        var file = await folder.CreateFile("Interfaces.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildInterfaces(CancellationToken ct)
    {
        var topNodes = new List<MemberDeclarationSyntax>(2);
        var namespaceNodes = new List<MemberDeclarationSyntax>(8);

        var lastNamespaceName = _intputContext.Project.Services[0].FullName.Namespace;

        foreach (var service in _intputContext.Project.Services)
        {
            var currentNamespaceName = service.FullName.Namespace;
            if (currentNamespaceName != lastNamespaceName)
            {
                if (string.IsNullOrEmpty(lastNamespaceName))
                {
                    topNodes.AddRange(namespaceNodes);
                }
                else
                {
                    var localNsmspc = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(lastNamespaceName))
                        .WithMembers(SyntaxFactory.List(namespaceNodes));
                    topNodes.Add(localNsmspc);
                }
                namespaceNodes.Clear();
                lastNamespaceName = currentNamespaceName;
            }

            namespaceNodes.Add(BuildInterfaceForService(service));
        }

        if (namespaceNodes.Count > 0)
        {
            if (string.IsNullOrEmpty(lastNamespaceName))
            {
                topNodes.AddRange(namespaceNodes);
            }
            else
            {
                var localNsmspc = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(lastNamespaceName))
                    .WithMembers(SyntaxFactory.List(namespaceNodes));
                topNodes.Add(localNsmspc);
            }
        }

        var nmspc = NamespaceDeclaration(IdentifierName(_context.ServerNamespace))
            .WithMembers(List(topNodes))
            .WithNamespaceKeyword(Helpers.TopComment);
        var members = new List<MemberDeclarationSyntax>() { nmspc };
        return CompilationUnit().WithMembers(List<MemberDeclarationSyntax>(members));
    }

    private MemberDeclarationSyntax BuildInterfaceForService(ServiceDefinition service)
    {
        var inputType = _helpers.TransformModelReferenceToSyntax(service.Input);
        var outputType = _helpers.TransformModelReferenceToSyntax(service.Output);
        return InterfaceDeclaration("I" + service.FullName.Name)
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)))
            .WithMembers(
                SingletonList<MemberDeclarationSyntax>(
                    MethodDeclaration(
                        QualifiedName(
                            QualifiedName(
                                QualifiedName(
                                    IdentifierName("System"),
                                    IdentifierName("Threading")),
                                IdentifierName("Tasks")),
                            GenericName(
                                Identifier("ValueTask"))
                            .WithTypeArgumentList(
                                TypeArgumentList(
                                    SingletonSeparatedList<TypeSyntax>(
                                        outputType)))),
                        Identifier("HandleAsync"))
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
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken))));
    }
}