using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.Entities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kodoshi.CodeGenerator.CSharp.Models;

internal sealed class ModelsFileGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public ModelsFileGenerator(
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

        var topNodes = new List<SyntaxNode>(2);

        var namespaceNodes = new List<SyntaxNode>();

        var lastNamespaceName = _intputContext.Project.Models[0].FullName.Namespace;

        foreach (var model in Helpers.Chain(_intputContext.Project.Models, _context.ServicesTags))
        {
            var currentNamespaceName = model.FullName.Namespace;
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
            
            switch (model.Kind)
            {
                case ModelKind.Message:
                {
                    var m = (MessageDefinition)model;
                    var node = BuildNodeFromMessage(model, m.Fields, null);
                    namespaceNodes.Add(node);
                    break;
                }
                case ModelKind.MessageTemplate:
                {
                    var m = (MessageTemplateDefinition)model;
                    var node = BuildNodeFromMessage(model, m.Fields, m.TemplateArguments);
                    namespaceNodes.Add(node);
                    break;
                }
                case ModelKind.Tag:
                {
                    var m = (TagDefinition)model;
                    var nodes = BuildNodesFromTag(model, m.Fields, null);
                    namespaceNodes.AddRange(nodes);
                    break;
                }
                case ModelKind.TagTemplate:
                {
                    var m = (TagTemplateDefinition)model;
                    var nodes = BuildNodesFromTag(model, m.Fields, m.TemplateArguments);
                    namespaceNodes.AddRange(nodes);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
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

        var nmscp = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(_context.ModelsNamespace))
            .WithMembers(SyntaxFactory.List(topNodes))
            .WithNamespaceKeyword(Helpers.TopComment);

        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(nmscp))
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);
        var file = await (await _context.ModelsFolder).CreateFile("Models.generated.cs", ct);
        await file.Write(result, ct);
    }

    public IEnumerable<SyntaxNode> BuildNodesFromTag(
        ModelDefinition model,
        IReadOnlyList<TagFieldDefinition> tagFields,
        IReadOnlyList<TemplateArgumentReference>? templateArguments)
    {
        var modelName = SyntaxFactory.IdentifierName(model.FullName.Name);
        var enumSyntax = new List<SyntaxNodeOrToken>();
        var enumNameIdentifier = SyntaxFactory.IdentifierName("Values");

        void addEnum(TagFieldDefinition tagField)
        {
            enumSyntax!.Add(
                SyntaxFactory.EnumMemberDeclaration(
                    SyntaxFactory.Identifier(tagField.Name))
                .WithEqualsValue(
                    SyntaxFactory.EqualsValueClause(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(tagField.Value)))));
        }


        var tagFieldsCount = tagFields.Count;
        if (tagFieldsCount > 0)
        {
            addEnum(tagFields[0]);
            for (var i = 1; i < tagFieldsCount; i++)
            {
                enumSyntax.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                addEnum(tagFields[i]);
            }
        }

        var internalEnum = SyntaxFactory.EnumDeclaration(enumNameIdentifier.Identifier)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithMembers(
                    SyntaxFactory.SeparatedList<EnumMemberDeclarationSyntax>(enumSyntax));

        var nongenericMembers = new List<MemberDeclarationSyntax>();
        nongenericMembers.Add(internalEnum);

        var tagIdentifier = SyntaxFactory.IdentifierName("Tag");
        var tagProperty = SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.QualifiedName(
                    modelName,
                    enumNameIdentifier),
                tagIdentifier.Identifier)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.SingletonList<AccessorDeclarationSyntax>(
                        SyntaxFactory.AccessorDeclaration(
                            SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(
                            SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));
        
        var dataField = SyntaxFactory.IdentifierName("Data");
        var dataProperty = SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.NullableType(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.ObjectKeyword))),
                dataField.Identifier)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.InternalKeyword)))
            .WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.SingletonList<AccessorDeclarationSyntax>(
                        SyntaxFactory.AccessorDeclaration(
                            SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(
                            SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));

        var tagLocal = SyntaxFactory.IdentifierName("tag");
        var dataLocal = SyntaxFactory.IdentifierName("data");
        var constructor = SyntaxFactory.ConstructorDeclaration(
                modelName.Identifier)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.InternalKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList<ParameterSyntax>(
                        new SyntaxNodeOrToken[]{
                            SyntaxFactory.Parameter(
                                tagLocal.Identifier)
                            .WithType(
                                SyntaxFactory.QualifiedName(
                                    modelName,
                                    enumNameIdentifier)),
                            SyntaxFactory.Token(SyntaxKind.CommaToken),
                            SyntaxFactory.Parameter(
                                dataLocal.Identifier)
                            .WithType(
                                SyntaxFactory.NullableType(
                                    SyntaxFactory.PredefinedType(
                                        SyntaxFactory.Token(SyntaxKind.ObjectKeyword))))})))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ThisExpression(),
                                tagIdentifier),
                            tagLocal)),
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ThisExpression(),
                                dataField),
                            dataLocal))));

        var structMembers = new List<MemberDeclarationSyntax>()
        {
            tagProperty, dataProperty, constructor,
        };
        SimpleNameSyntax baseName = modelName;
        var structDeclaration = SyntaxFactory.StructDeclaration(modelName.Identifier)
            .WithModifiers(
                SyntaxFactory.TokenList(_publicReadonlyModifiers));
        
        List<SyntaxNodeOrToken>? baseNameTokens = null;
        List<SyntaxNodeOrToken>? templateArgsTokens = null;
        List<TypeParameterConstraintClauseSyntax>? constraints = null;
        if (templateArguments != null)
        {
            baseNameTokens = new List<SyntaxNodeOrToken>(templateArguments.Count);
            templateArgsTokens = new List<SyntaxNodeOrToken>(templateArguments.Count);
            var types = new List<TypeParameterSyntax>(templateArguments.Count);
            constraints = new List<TypeParameterConstraintClauseSyntax>(templateArguments.Count);
            foreach (var templateArgument in templateArguments)
            {
                var typeName = _helpers.TransformModelReferenceToSyntax(templateArgument).ToFullString();
                types.Add(
                    SyntaxFactory.TypeParameter(typeName));
                constraints.Add(
                   SyntaxFactory.TypeParameterConstraintClause(
                        SyntaxFactory.IdentifierName(typeName))
                    .WithConstraints(
                        SyntaxFactory.SingletonSeparatedList<TypeParameterConstraintSyntax>(
                            SyntaxFactory.TypeConstraint(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("System"),
                                    SyntaxFactory.GenericName(
                                        SyntaxFactory.Identifier("IEquatable"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.IdentifierName(typeName)))))))));
                baseNameTokens.Add(SyntaxFactory.IdentifierName(typeName));
                templateArgsTokens.Add(SyntaxFactory.TypeParameter(SyntaxFactory.Identifier(typeName)));
                baseNameTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                templateArgsTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            }
            var typeParams = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList<TypeParameterSyntax>(types));

            if (baseNameTokens.Count > 0)
            {
                baseNameTokens.RemoveAt(baseNameTokens.Count - 1);
                templateArgsTokens.RemoveAt(templateArgsTokens.Count - 1);
            }
            baseName = SyntaxFactory.GenericName(
                modelName.Identifier)
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        baseNameTokens)));
            
            structDeclaration = structDeclaration
                .WithTypeParameterList(typeParams)
                .WithConstraintClauses(
                    SyntaxFactory.List(constraints));
        }

        var equalsIdentifier = SyntaxFactory.IdentifierName("Equals");
        structMembers.Add(SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                equalsIdentifier.Identifier)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                        SyntaxFactory.Parameter(
                            SyntaxFactory.Identifier("other"))
                        .WithType(
                            baseName))))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        SyntaxFactory.BinaryExpression(
                            SyntaxKind.EqualsExpression,
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ThisExpression(),
                                SyntaxFactory.IdentifierName("Tag")),
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("other"),
                                SyntaxFactory.IdentifierName("Tag"))),
                        SyntaxFactory.ParenthesizedExpression(
                            SyntaxFactory.BinaryExpression(
                                SyntaxKind.LogicalOrExpression,
                                SyntaxFactory.ParenthesizedExpression(
                                    SyntaxFactory.BinaryExpression(
                                        SyntaxKind.LogicalAndExpression,
                                        SyntaxFactory.IsPatternExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.ThisExpression(),
                                                SyntaxFactory.IdentifierName("Data")),
                                            SyntaxFactory.ConstantPattern(
                                                SyntaxFactory.LiteralExpression(
                                                    SyntaxKind.NullLiteralExpression))),
                                        SyntaxFactory.IsPatternExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName("other"),
                                                SyntaxFactory.IdentifierName("Data")),
                                            SyntaxFactory.ConstantPattern(
                                                SyntaxFactory.LiteralExpression(
                                                    SyntaxKind.NullLiteralExpression))))),
                                SyntaxFactory.ParenthesizedExpression(
                                    SyntaxFactory.BinaryExpression(
                                        SyntaxKind.LogicalAndExpression,
                                        SyntaxFactory.BinaryExpression(
                                            SyntaxKind.LogicalAndExpression,
                                            SyntaxFactory.IsPatternExpression(
                                                SyntaxFactory.MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    SyntaxFactory.ThisExpression(),
                                                    SyntaxFactory.IdentifierName("Data")),
                                                SyntaxFactory.UnaryPattern(
                                                    SyntaxFactory.ConstantPattern(
                                                        SyntaxFactory.LiteralExpression(
                                                            SyntaxKind.NullLiteralExpression)))),
                                            SyntaxFactory.IsPatternExpression(
                                                SyntaxFactory.MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    SyntaxFactory.IdentifierName("other"),
                                                    SyntaxFactory.IdentifierName("Data")),
                                                SyntaxFactory.UnaryPattern(
                                                    SyntaxFactory.ConstantPattern(
                                                        SyntaxFactory.LiteralExpression(
                                                            SyntaxKind.NullLiteralExpression))))),
                                        SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    SyntaxFactory.ThisExpression(),
                                                    SyntaxFactory.IdentifierName("Data")),
                                                SyntaxFactory.IdentifierName("Equals")))
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                    SyntaxFactory.Argument(
                                                        SyntaxFactory.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SyntaxFactory.IdentifierName("other"),
                                                            SyntaxFactory.IdentifierName("Data")))))))))))))
            .WithSemicolonToken(
                SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

        structMembers.Add(SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                    equalsIdentifier.Identifier)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        new []{
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.OverrideKeyword)}))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                            SyntaxFactory.Parameter(
                                SyntaxFactory.Identifier("obj"))
                            .WithType(
                                SyntaxFactory.NullableType(SyntaxFactory.PredefinedType(
                                    SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))))))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(
                        SyntaxFactory.BinaryExpression(
                            SyntaxKind.LogicalAndExpression,
                            SyntaxFactory.IsPatternExpression(
                                SyntaxFactory.IdentifierName("obj"),
                                SyntaxFactory.DeclarationPattern(
                                    baseName,
                                    SyntaxFactory.SingleVariableDesignation(
                                        SyntaxFactory.Identifier("instance")))),
                            SyntaxFactory.InvocationExpression(
                                equalsIdentifier)
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName("instance"))))))))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

        structMembers.Add(SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                SyntaxFactory.Identifier("GetHashCode"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    new []{
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.OverrideKeyword)}))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.SingletonList<StatementSyntax>(
                        SyntaxFactory.CheckedStatement(
                            SyntaxKind.UncheckedStatement,
                            SyntaxFactory.Block(
                                SyntaxFactory.LocalDeclarationStatement(
                                    SyntaxFactory.VariableDeclaration(
                                        SyntaxFactory.PredefinedType(
                                            SyntaxFactory.Token(SyntaxKind.UIntKeyword)))
                                    .WithVariables(
                                        SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                            SyntaxFactory.VariableDeclarator(
                                                SyntaxFactory.Identifier("hash"))
                                            .WithInitializer(
                                                SyntaxFactory.EqualsValueClause(
                                                    SyntaxFactory.LiteralExpression(
                                                        SyntaxKind.NumericLiteralExpression,
                                                        SyntaxFactory.Literal(
                                                            "2166136261",
                                                            2166136261))))))),
                                SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        SyntaxFactory.IdentifierName("hash"),
                                        SyntaxFactory.BinaryExpression(
                                            SyntaxKind.MultiplyExpression,
                                            SyntaxFactory.ParenthesizedExpression(
                                                SyntaxFactory.BinaryExpression(
                                                    SyntaxKind.ExclusiveOrExpression,
                                                    SyntaxFactory.IdentifierName("hash"),
                                                    SyntaxFactory.InvocationExpression(
                                                        SyntaxFactory.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SyntaxFactory.MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                SyntaxFactory.MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    SyntaxFactory.IdentifierName("Kodoshi"),
                                                                    SyntaxFactory.IdentifierName("Core")),
                                                                SyntaxFactory.IdentifierName("Utils")),
                                                            SyntaxFactory.IdentifierName("CalculateHashCode")))
                                                    .WithArgumentList(
                                                        SyntaxFactory.ArgumentList(
                                                            SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                                SyntaxFactory.Argument(
                                                                    SyntaxFactory.MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        SyntaxFactory.ThisExpression(),
                                                                        SyntaxFactory.IdentifierName("Tag")))))))),
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(16777619))))),
                                SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        SyntaxFactory.IdentifierName("hash"),
                                        SyntaxFactory.BinaryExpression(
                                            SyntaxKind.MultiplyExpression,
                                            SyntaxFactory.ParenthesizedExpression(
                                                SyntaxFactory.BinaryExpression(
                                                    SyntaxKind.ExclusiveOrExpression,
                                                    SyntaxFactory.IdentifierName("hash"),
                                                    SyntaxFactory.InvocationExpression(
                                                        SyntaxFactory.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SyntaxFactory.MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                SyntaxFactory.MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    SyntaxFactory.IdentifierName("Kodoshi"),
                                                                    SyntaxFactory.IdentifierName("Core")),
                                                                SyntaxFactory.IdentifierName("Utils")),
                                                            SyntaxFactory.IdentifierName("CalculateHashCode")))
                                                    .WithArgumentList(
                                                        SyntaxFactory.ArgumentList(
                                                            SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                                SyntaxFactory.Argument(
                                                                    SyntaxFactory.MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        SyntaxFactory.ThisExpression(),
                                                                        SyntaxFactory.IdentifierName("Data")))))))),
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.NumericLiteralExpression,
                                                SyntaxFactory.Literal(16777619))))),
                                SyntaxFactory.ReturnStatement(
                                    SyntaxFactory.CastExpression(
                                        SyntaxFactory.PredefinedType(
                                            SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                                        SyntaxFactory.IdentifierName("hash")))))))));

        foreach (var tagField in tagFields)
        {
            var methodName = $"Create{tagField.Name}";
            var valueIdentifier = SyntaxFactory.IdentifierName("value");

            ExpressionSyntax constructorArg;
            if (tagField.AdditionalDataType is null)
            {
                constructorArg = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
            }
            else
            {
                constructorArg = valueIdentifier;
            }
            
            var methodDecl = SyntaxFactory.MethodDeclaration(
                    baseName,
                    SyntaxFactory.Identifier(methodName))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        new []{
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)}))
                .WithAttributeLists(
                    SyntaxFactory.SingletonList<AttributeListSyntax>(
                        SyntaxFactory.AttributeList(
                            SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(
                                SyntaxFactory.Attribute(
                                    SyntaxFactory.QualifiedName(
                                        SyntaxFactory.QualifiedName(
                                            SyntaxFactory.QualifiedName(
                                                SyntaxFactory.IdentifierName("System"),
                                                SyntaxFactory.IdentifierName("Runtime")),
                                            SyntaxFactory.IdentifierName("CompilerServices")),
                                        SyntaxFactory.IdentifierName("MethodImpl")))
                                .WithArgumentList(
                                    SyntaxFactory.AttributeArgumentList(
                                        SyntaxFactory.SingletonSeparatedList<AttributeArgumentSyntax>(
                                            SyntaxFactory.AttributeArgument(
                                                SyntaxFactory.MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    SyntaxFactory.MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        SyntaxFactory.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SyntaxFactory.MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                SyntaxFactory.IdentifierName("System"),
                                                                SyntaxFactory.IdentifierName("Runtime")),
                                                            SyntaxFactory.IdentifierName("CompilerServices")),
                                                        SyntaxFactory.IdentifierName("MethodImplOptions")),
                                                    SyntaxFactory.IdentifierName("AggressiveInlining"))))))))))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(
                        SyntaxFactory.ObjectCreationExpression(baseName)
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    SyntaxFactory.IdentifierName(modelName.Identifier),
                                                    enumNameIdentifier),
                                                SyntaxFactory.IdentifierName(tagField.Name))),
                                        SyntaxFactory.Token(SyntaxKind.CommaToken),
                                        SyntaxFactory.Argument(
                                            constructorArg)})))))
                .WithSemicolonToken(
                    SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            if (tagField.AdditionalDataType is not null)
            {
                var tagFieldType = _helpers.TransformModelReferenceToSyntax(tagField.AdditionalDataType);
                methodDecl = methodDecl.WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                            SyntaxFactory.Parameter(
                                valueIdentifier.Identifier)
                            .WithType(tagFieldType))));
                
                structMembers.Add(
                    SyntaxFactory.MethodDeclaration(
                        tagFieldType,
                        SyntaxFactory.Identifier($"Get{tagField.Name}"))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(
                        SyntaxFactory.Block(
                            SyntaxFactory.IfStatement(
                                SyntaxFactory.BinaryExpression(
                                    SyntaxKind.NotEqualsExpression,
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.ThisExpression(),
                                        tagIdentifier),
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            modelName,
                                            enumNameIdentifier),
                                        SyntaxFactory.IdentifierName(tagField.Name))),
                                SyntaxFactory.ThrowStatement(
                                    SyntaxFactory.ObjectCreationExpression(
                                        SyntaxFactory.QualifiedName(
                                            SyntaxFactory.IdentifierName("System"),
                                            SyntaxFactory.IdentifierName("ArgumentException")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.InterpolatedStringExpression(
                                                        SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken))
                                                    .WithContents(
                                                        SyntaxFactory.List<InterpolatedStringContentSyntax>(
                                                            new InterpolatedStringContentSyntax[]{
                                                                SyntaxFactory.InterpolatedStringText()
                                                                .WithTextToken(
                                                                    SyntaxFactory.Token(
                                                                        SyntaxFactory.TriviaList(),
                                                                        SyntaxKind.InterpolatedStringTextToken,
                                                                        "Tag is not ",
                                                                        "Tag is not ",
                                                                        SyntaxFactory.TriviaList())),
                                                                SyntaxFactory.Interpolation(
                                                                    SyntaxFactory.MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        SyntaxFactory.MemberAccessExpression(
                                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                                            modelName,
                                                                            enumNameIdentifier),
                                                                        SyntaxFactory.IdentifierName(tagField.Name))),
                                                                SyntaxFactory.InterpolatedStringText()
                                                                .WithTextToken(
                                                                    SyntaxFactory.Token(
                                                                        SyntaxFactory.TriviaList(),
                                                                        SyntaxKind.InterpolatedStringTextToken,
                                                                        ".",
                                                                        ".",
                                                                        SyntaxFactory.TriviaList()))})))))))),
                            SyntaxFactory.IfStatement(
                                SyntaxFactory.IsPatternExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.ThisExpression(),
                                        dataField),
                                    SyntaxFactory.ConstantPattern(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.NullLiteralExpression))),
                                SyntaxFactory.ThrowStatement(
                                    SyntaxFactory.ObjectCreationExpression(
                                        SyntaxFactory.QualifiedName(
                                            SyntaxFactory.IdentifierName("System"),
                                            SyntaxFactory.IdentifierName("ArgumentNullException")))
                                    .WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                SyntaxFactory.Argument(
                                                    SyntaxFactory.InvocationExpression(
                                                        SyntaxFactory.IdentifierName(
                                                            SyntaxFactory.Identifier(
                                                                SyntaxFactory.TriviaList(),
                                                                SyntaxKind.NameOfKeyword,
                                                                "nameof",
                                                                "nameof",
                                                                SyntaxFactory.TriviaList())))
                                                    .WithArgumentList(
                                                        SyntaxFactory.ArgumentList(
                                                            SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                                SyntaxFactory.Argument(
                                                                    SyntaxFactory.MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        SyntaxFactory.ThisExpression(),
                                                                        dataField))))))))))),
                            SyntaxFactory.ReturnStatement(
                                SyntaxFactory.CastExpression(
                                    tagFieldType,
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.ThisExpression(),
                                        dataField))))));
            }

            if (templateArgsTokens != null)
            {
                methodDecl = methodDecl
                    .WithTypeParameterList(SyntaxFactory.TypeParameterList(
                        SyntaxFactory.SeparatedList<TypeParameterSyntax>(templateArgsTokens)
                    ))
                    .WithConstraintClauses(SyntaxFactory.List(constraints!));
            }
            nongenericMembers.Add(methodDecl);
        }

        if (templateArguments != null)
        {
            var nongenericClass = SyntaxFactory.ClassDeclaration(modelName.Identifier)
                .WithModifiers(
                    SyntaxFactory.TokenList(_publicStaticModifiers))
                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(nongenericMembers));
        
            structDeclaration = structDeclaration.WithBaseList(
                SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                        SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.QualifiedName(
                                SyntaxFactory.IdentifierName("System"),
                                SyntaxFactory.GenericName(
                                    SyntaxFactory.Identifier("IEquatable"))
                                .WithTypeArgumentList(
                                    SyntaxFactory.TypeArgumentList(
                                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(baseName))))))))
                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(structMembers));

            return new SyntaxNode[] { nongenericClass, structDeclaration };
        }
        else
        {
            nongenericMembers.AddRange(structMembers);
            structDeclaration = structDeclaration.WithBaseList(
                SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                        SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.QualifiedName(
                                SyntaxFactory.IdentifierName("System"),
                                SyntaxFactory.GenericName(
                                    SyntaxFactory.Identifier("IEquatable"))
                                .WithTypeArgumentList(
                                    SyntaxFactory.TypeArgumentList(
                                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(baseName))))))))
                .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(nongenericMembers));
            return new SyntaxNode[] { structDeclaration };
        }
    }

    private SyntaxToken[] _publicSealedModifiers = new []
    {
        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
        SyntaxFactory.Token(SyntaxKind.SealedKeyword),
    };
    private SyntaxToken[] _publicStaticModifiers = new []
    {
        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
    };
    private SyntaxToken[] _publicModifiers = new []
    {
        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
    };
    private SyntaxToken[] _publicReadonlyModifiers = new []
    {
        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword),
    };
    private SyntaxToken[] _publicOverrideModifiers = new []
    {
        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
        SyntaxFactory.Token(SyntaxKind.OverrideKeyword),
    };

    private AccessorListSyntax _getter = SyntaxFactory.AccessorList(
        SyntaxFactory.SingletonList<AccessorDeclarationSyntax>(
            SyntaxFactory.AccessorDeclaration(
                SyntaxKind.GetAccessorDeclaration)
            .WithSemicolonToken(
                SyntaxFactory.Token(SyntaxKind.SemicolonToken))));

    private SyntaxNode BuildNodeFromMessage(
        ModelDefinition model,
        IReadOnlyList<MessageFieldDefinition> fields,
        IReadOnlyList<TemplateArgumentReference>? templateArguments)
    {
        var modelName = model.FullName.Name;
        var modelTypeName = SyntaxFactory.IdentifierName(modelName);
        var modelRefType = _helpers.TransformModelDefinitionToSyntax(model);
        var members = new List<MemberDeclarationSyntax>(fields.Count + 4);

        var baseList = SyntaxFactory.BaseList(
            SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.GenericName(
                            SyntaxFactory.Identifier("IEquatable"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    modelRefType)))))));

        var constructorParams = new List<ParameterSyntax>();
        var body = new List<StatementSyntax>();
        var constructor = SyntaxFactory
            .ConstructorDeclaration(modelTypeName.Identifier)
            .WithModifiers(SyntaxFactory.TokenList(_publicModifiers));
        var equalsIdentifier = SyntaxFactory.IdentifierName("Equals");
        var otherIdentifier = SyntaxFactory.IdentifierName("other");
        var equalsMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                equalsIdentifier.Identifier)
            .WithModifiers(SyntaxFactory.TokenList(_publicModifiers))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                        SyntaxFactory.Parameter(
                            otherIdentifier.Identifier)
                        .WithType(SyntaxFactory.NullableType(modelRefType)))));
        ExpressionSyntax? equalsReturn = SyntaxFactory.BinaryExpression(
                SyntaxKind.NotEqualsExpression,
                otherIdentifier,
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NullLiteralExpression));

        var getHashCodeIdentifier = SyntaxFactory.IdentifierName("GetHashCode");
        var getHashCodeMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                getHashCodeIdentifier.Identifier)
            .WithModifiers(SyntaxFactory.TokenList(_publicOverrideModifiers));
        var localHashIdentifier = SyntaxFactory.IdentifierName("hash");
        var getHashCodeBody = new List<StatementSyntax>()
        {
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.UIntKeyword)))
                .WithVariables(
                    SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                        SyntaxFactory.VariableDeclarator(
                            localHashIdentifier.Identifier)
                        .WithInitializer(
                            SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    SyntaxFactory.Literal(
                                        "2166136261",
                                        2166136261))))))),
        };
        
        var overrideEqualsMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                    equalsIdentifier.Identifier)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        new []{
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.OverrideKeyword)}))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                            SyntaxFactory.Parameter(
                                SyntaxFactory.Identifier("obj"))
                            .WithType(
                                SyntaxFactory.NullableType(SyntaxFactory.PredefinedType(
                                    SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))))))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(
                        SyntaxFactory.BinaryExpression(
                            SyntaxKind.LogicalAndExpression,
                            SyntaxFactory.IsPatternExpression(
                                SyntaxFactory.IdentifierName("obj"),
                                SyntaxFactory.DeclarationPattern(
                                    modelRefType,
                                    SyntaxFactory.SingleVariableDesignation(
                                        SyntaxFactory.Identifier("instance")))),
                            SyntaxFactory.InvocationExpression(
                                equalsIdentifier)
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName("instance"))))))))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        foreach (var field in fields)
        {
            var fieldType = _helpers.TransformModelReferenceToSyntax(field.Type);
            var fieldName = SyntaxFactory.IdentifierName(field.Name);
            var paramName = SyntaxFactory.IdentifierName(char.ToLower(field.Name[0]) + field.Name.Substring(1));
            var property = SyntaxFactory
                .PropertyDeclaration(
                    fieldType,
                    fieldName.Identifier)
                .WithModifiers(SyntaxFactory.TokenList(_publicModifiers))
                .WithAccessorList(_getter);
            var constructorParam = SyntaxFactory
                .Parameter(paramName.Identifier)
                .WithType(fieldType);
            var assignStatement = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        fieldName),
                    paramName));
            constructorParams.Add(constructorParam);
            members.Add(property);
            body.Add(assignStatement);
            
            var equalsInvocation = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.ThisExpression(),
                            fieldName),
                        equalsIdentifier))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                            SyntaxFactory.Argument(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    otherIdentifier,
                                    fieldName)))));

            equalsReturn = SyntaxFactory.BinaryExpression(
                SyntaxKind.LogicalAndExpression,
                equalsReturn,
                equalsInvocation);
            
            getHashCodeBody.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    localHashIdentifier,
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.MultiplyExpression,
                        SyntaxFactory.ParenthesizedExpression(
                            SyntaxFactory.BinaryExpression(
                                SyntaxKind.ExclusiveOrExpression,
                                localHashIdentifier,
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName("Kodoshi"),
                                                SyntaxFactory.IdentifierName("Core")),
                                            SyntaxFactory.IdentifierName("Utils")),
                                        SyntaxFactory.IdentifierName("CalculateHashCode")))
                                .WithArgumentList(
                                    SyntaxFactory.ArgumentList(
                                        SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                            SyntaxFactory.Argument(
                                                SyntaxFactory.MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    SyntaxFactory.ThisExpression(),
                                                    fieldName))))))),
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(16777619))))));
        }

        members.Add(constructor
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(constructorParams)))
            .WithBody(SyntaxFactory.Block(body)));
        members.Add(equalsMethod.WithBody(
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ReturnStatement(equalsReturn)))));
        members.Add(overrideEqualsMethod);

        getHashCodeBody.Add(
            SyntaxFactory.ReturnStatement(
                SyntaxFactory.CastExpression(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                    localHashIdentifier)));
        members.Add(
            getHashCodeMethod.WithBody(
            SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.CheckedStatement(
                                SyntaxKind.UncheckedStatement,
                                SyntaxFactory.Block(getHashCodeBody))))));

        var clsDeclaration = SyntaxFactory.ClassDeclaration(modelName)
            .WithBaseList(baseList)
            .WithModifiers(SyntaxFactory.TokenList(_publicSealedModifiers))
            .WithMembers(SyntaxFactory.List(members));
        
        if (templateArguments != null)
        {
            var types = new List<TypeParameterSyntax>(templateArguments.Count);
            var constraints = new List<TypeParameterConstraintClauseSyntax>();
            foreach (var templateArgument in templateArguments)
            {
                var typeName = _helpers.TransformModelReferenceToSyntax(templateArgument).ToFullString();
                types.Add(
                    SyntaxFactory.TypeParameter(typeName));
                constraints.Add(
                   SyntaxFactory.TypeParameterConstraintClause(
                        SyntaxFactory.IdentifierName(typeName))
                    .WithConstraints(
                        SyntaxFactory.SingletonSeparatedList<TypeParameterConstraintSyntax>(
                            SyntaxFactory.TypeConstraint(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("System"),
                                    SyntaxFactory.GenericName(
                                        SyntaxFactory.Identifier("IEquatable"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.IdentifierName(typeName)))))))));
            }
            var typeParams = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList<TypeParameterSyntax>(types));
            clsDeclaration = clsDeclaration
                .WithTypeParameterList(typeParams)
                .WithConstraintClauses(
                    SyntaxFactory.List(constraints));
        }
        return clsDeclaration;
    }
}
