using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.Entities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Kodoshi.CodeGenerator.CSharp.Models;

internal sealed class DefaultValuesFileGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;

    public DefaultValuesFileGenerator(
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
        var tasks = new Task[]
        {
            GenerateDefaultValuesCollection(ct),
            GenerateDefaultValuesCollectionBuilder(ct),
        };
        await Task.WhenAll(tasks);
    }

    private async Task GenerateDefaultValuesCollection(CancellationToken ct)
    {
        await Task.Yield();

        var compilationUnit = BuildDefaultValuesClass(ct)
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);
        var file = await (await _context.ModelsFolder).CreateFile("DefaultValuesCollection.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildDefaultValuesClass(CancellationToken ct)
    {
        var code = @"
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NAMESPACE
{
    internal sealed class DefaultValuesCollection : Kodoshi.Core.IDefaultValuesCollection
    {
        private readonly ConcurrentDictionary<Type, object> _valuesHolder;
        private readonly Dictionary<Type, Func<Type[], Kodoshi.Core.IDefaultValuesCollection, object>> _valuesBuilders;

        public DefaultValuesCollection(
            ConcurrentDictionary<Type, object> _valuesHolder,
            Dictionary<Type, Func<Type[], Kodoshi.Core.IDefaultValuesCollection, object>> _valuesBuilders)
        {
            this._valuesHolder = _valuesHolder;
            this._valuesBuilders = _valuesBuilders;
        }

        public void Dispose() { }

        public T GetDefaultValue<T>() where T : IEquatable<T> => (T)GetDefaultValue(typeof(T));

        public object GetDefaultValue(Type type)
            => _valuesHolder.GetOrAdd(type, (t) => {
                Func<Type[], Kodoshi.Core.IDefaultValuesCollection, object> builder;
                if (_valuesBuilders.TryGetValue(t, out builder!))
                    return builder(Array.Empty<Type>(), this);

                if (t.IsGenericType && _valuesBuilders.TryGetValue(t.GetGenericTypeDefinition(), out builder!))
                    return builder(t.GetGenericArguments(), this);

                throw new NotImplementedException($""Don't know how to default build type [{t}]."");
            });
    }
}";
        var unit = ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = NamespaceDeclaration(ParseName(_context.CoreNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        return unit.ReplaceNode(nmspc, newNmspc);
    }

    private async Task GenerateDefaultValuesCollectionBuilder(CancellationToken ct)
    {
        await Task.Yield();

        var compilationUnit = BuildDefaultValuesBuilderClass(ct)
            .NormalizeWhitespace(eol: "\n");

        var result = await Helpers.SerializeNode(compilationUnit);
        var file = await (await _context.ModelsFolder).CreateFile("DefaultValuesCollectionBuilder.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildDefaultValuesBuilderClass(CancellationToken ct)
    {
        var code = @"
namespace NAMESPACE
{
    public readonly struct DefaultValuesCollectionBuilder
    {
        public Kodoshi.Core.IDefaultValuesCollection Build()
        {
            var _defaultValues = new System.Collections.Concurrent.ConcurrentDictionary<System.Type, object>();
            _defaultValues[typeof(bool)] = false;
            _defaultValues[typeof(sbyte)] = (sbyte)0;
            _defaultValues[typeof(byte)] = (byte)0;
            _defaultValues[typeof(short)] = (short)0;
            _defaultValues[typeof(ushort)] = (ushort)0;
            _defaultValues[typeof(int)] = (int)0;
            _defaultValues[typeof(uint)] = (uint)0;
            _defaultValues[typeof(long)] = (long)0;
            _defaultValues[typeof(ulong)] = (ulong)0;
            _defaultValues[typeof(string)] = """";
            _defaultValues[typeof(System.Guid)] = System.Guid.Empty;
            _defaultValues[typeof(Kodoshi.Core.VoidType)] = Kodoshi.Core.VoidType.Instance;
            _defaultValues[typeof(Kodoshi.Core.ReadOnlyArray<byte>)] = Kodoshi.Core.ReadOnlyArray.Empty<byte>();
            System.Reflection.MethodInfo? _method = null;
            foreach (var m in typeof(DefaultValuesCollection).GetMethods())
            {
                if (m.Name == nameof(DefaultValuesCollection.GetDefaultValue) && !m.IsGenericMethod)
                {
                    _method = m;
                    break;
                }
            }
            if (_method == null)
            {
                throw new System.ArgumentNullException(nameof(_method));
            }
            var _builders = new System.Collections.Generic.Dictionary<System.Type, System.Func<System.Type[], Kodoshi.Core.IDefaultValuesCollection, object>>();
            
            _builders[typeof(Kodoshi.Core.ReadOnlyArray<>)] = static (_types, _col) => typeof(Kodoshi.Core.ReadOnlyArray).GetMethod(nameof(Kodoshi.Core.ReadOnlyArray.Empty)).MakeGenericMethod(_types).Invoke(null, System.Array.Empty<System.Type>());
            _builders[typeof(Kodoshi.Core.ReadOnlyMap<, >)] = static (_types, _col) => typeof(Kodoshi.Core.ReadOnlyMap).GetMethod(nameof(Kodoshi.Core.ReadOnlyMap.Empty)).MakeGenericMethod(_types).Invoke(null, System.Array.Empty<System.Type>());

            { }

            _builders.TrimExcess();
            return new DefaultValuesCollection(
                _defaultValues,
                _builders);
        }
    }
}
";
        var unit = ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = NamespaceDeclaration(ParseName(_context.CoreNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        var block = ((newNmspc.Members.First() as StructDeclarationSyntax)!.Members.First() as MethodDeclarationSyntax)!.Body!.Statements.Where(x => x is BlockSyntax).Single();
        var newBlock = Block(List(BuildDefaultValuesStatements()));
        newNmspc = newNmspc.ReplaceNode(block, newBlock);
        return unit.ReplaceNode(nmspc, newNmspc);
    }

    private IEnumerable<StatementSyntax> BuildDefaultValuesStatements()
    {
        var models = Helpers.Chain(_intputContext.Project.Models, _context.ServicesTags);
        foreach (var model in models)
        {
            switch (model.Kind)
            {
                case ModelKind.Message:
                {
                    var realModel = (MessageDefinition)model;
                    yield return BuildSyntaxFromModel(model, realModel.Fields, null);
                    break;
                }
                case ModelKind.MessageTemplate:
                {
                    var realModel = (MessageTemplateDefinition)model;
                    yield return BuildSyntaxFromModel(model, realModel.Fields, realModel.TemplateArguments);
                    break;
                }
                case ModelKind.Tag:
                {
                    var realModel = (TagDefinition)model;
                    yield return BuildSyntaxFromTag(model, realModel.Fields, null);
                    break;
                }
                case ModelKind.TagTemplate:
                {
                    var realModel = (TagTemplateDefinition)model;
                    yield return BuildSyntaxFromTag(model, realModel.Fields, realModel.TemplateArguments);
                    break;
                }
                default: throw new NotImplementedException();
            }
        }
    }

    private StatementSyntax BuildSyntaxFromTag(
        ModelDefinition model,
        IReadOnlyList<TagFieldDefinition> fields,
        IReadOnlyList<TemplateArgumentReference>? templateArguments)
    {
        TypeSyntax keyName;
        TypeSyntax nongenericName;
        switch (model.Kind)
        {
            case ModelKind.Tag:
            {
                keyName = _helpers.TransformModelDefinitionToSyntax(model);
                nongenericName = keyName;
                break;
            }
            case ModelKind.TagTemplate:
            {
                var @def = (TagTemplateDefinition)model;
                keyName = _helpers.BuildOmittedGenericNameDefinition(def);
                nongenericName = _helpers.BuildNonGenericVariantNameDefinition(def);
                break;
            }
            default: throw new NotImplementedException();
        }

        var statements = new List<StatementSyntax>();

        var defaultTagField = fields.Where(t => t.Value == 0).Single()!;

        ExpressionSyntax staticMethodExpr = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    TypeOfExpression(nongenericName),
                    IdentifierName("GetMethod")))
            .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList<ArgumentSyntax>(
                        Argument(
                            InvocationExpression(
                                IdentifierName(
                                    Identifier(
                                        TriviaList(),
                                        SyntaxKind.NameOfKeyword,
                                        "nameof",
                                        "nameof",
                                        TriviaList())))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList<ArgumentSyntax>(
                                        Argument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                nongenericName,
                                                IdentifierName($"Create{defaultTagField.Name}"))))))))));
        
        if (templateArguments is not null)
        {
            staticMethodExpr = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    staticMethodExpr,
                    IdentifierName("MakeGenericMethod")))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList<ArgumentSyntax>(
                            Argument(
                                IdentifierName("_types")))));
        }

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
                            Identifier("_static"))
                        .WithInitializer(
                            EqualsValueClause(staticMethodExpr))))));
        
        var objectArgs = new List<SyntaxNodeOrToken>(1);
        if (defaultTagField.AdditionalDataType is not null)
        {
            var templateArgsMap = new Dictionary<TemplateArgumentReference, int>();
            if (templateArguments is not null)
            {
                for (var i = 0; i < templateArguments.Count; i++)
                {
                    templateArgsMap[templateArguments[i]] = i;
                }
            }
            objectArgs.Add(BuildGetDefaultValueInvocation(defaultTagField.AdditionalDataType, templateArgsMap));
        }

        var argsName = IdentifierName("_args");
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
                        VariableDeclarator(argsName.Identifier)
                        .WithInitializer(
                            EqualsValueClause(
                                ArrayCreationExpression(
                                    ArrayType(
                                        PredefinedType(
                                            Token(SyntaxKind.ObjectKeyword)))
                                    .WithRankSpecifiers(
                                        SingletonList<ArrayRankSpecifierSyntax>(
                                            ArrayRankSpecifier(
                                                SingletonSeparatedList<ExpressionSyntax>(
                                                    OmittedArraySizeExpression())))))
                                .WithInitializer(
                                    InitializerExpression(
                                        SyntaxKind.ArrayInitializerExpression,
                                        SeparatedList<ExpressionSyntax>(
                                            objectArgs)))))))));

        statements.Add(ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("_static"),
                        IdentifierName("Invoke")))
                .WithArgumentList(
                    ArgumentList(
                        SeparatedList<ArgumentSyntax>(
                            new SyntaxNodeOrToken[]{
                                Argument(
                                    LiteralExpression(
                                        SyntaxKind.NullLiteralExpression)),
                                Token(SyntaxKind.CommaToken),
                                Argument(
                                    IdentifierName("_args"))})))));

        return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    ElementAccessExpression(
                        IdentifierName("_builders"))
                    .WithArgumentList(
                        BracketedArgumentList(
                            SingletonSeparatedList<ArgumentSyntax>(
                                Argument(
                                    TypeOfExpression(keyName))))),
                    ParenthesizedLambdaExpression()
                    .WithParameterList(
                        ParameterList(
                            SeparatedList<ParameterSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Parameter(
                                        Identifier("_types")),
                                    Token(SyntaxKind.CommaToken),
                                    Parameter(
                                        Identifier("_col"))})))
                    .WithBlock(
                        Block(List(statements)))));
    }

    private StatementSyntax BuildSyntaxFromModel(
        ModelDefinition model,
        IReadOnlyList<MessageFieldDefinition> fields,
        IReadOnlyList<TemplateArgumentReference>? templateArguments)
    {
        TypeSyntax keyName;
        switch (model.Kind)
        {
            case ModelKind.Message:
            {
                keyName = _helpers.TransformModelDefinitionToSyntax(model);
                break;
            }
            case ModelKind.MessageTemplate:
            {
                keyName = _helpers.BuildOmittedGenericNameDefinition((MessageTemplateDefinition)model);
                break;
            }
            default: throw new NotImplementedException();
        }

        var statements = new List<StatementSyntax>();
        var objectArgs = new List<SyntaxNodeOrToken>();
        var templateArgsMap = new Dictionary<TemplateArgumentReference, int>();
        if (templateArguments is not null)
        {
            for (var i = 0; i < templateArguments.Count; i++)
            {
                templateArgsMap[templateArguments[i]] = i;
            }
        }

        foreach (var field in fields)
        {
            objectArgs.Add(BuildGetDefaultValueInvocation(field.Type, templateArgsMap));
            objectArgs.Add(Token(SyntaxKind.CommaToken));
        }
        objectArgs.RemoveAt(objectArgs.Count-1);

        var argsName = IdentifierName("_args");
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
                        VariableDeclarator(argsName.Identifier)
                        .WithInitializer(
                            EqualsValueClause(
                                ArrayCreationExpression(
                                    ArrayType(
                                        PredefinedType(
                                            Token(SyntaxKind.ObjectKeyword)))
                                    .WithRankSpecifiers(
                                        SingletonList<ArrayRankSpecifierSyntax>(
                                            ArrayRankSpecifier(
                                                SingletonSeparatedList<ExpressionSyntax>(
                                                    OmittedArraySizeExpression())))))
                                .WithInitializer(
                                    InitializerExpression(
                                        SyntaxKind.ArrayInitializerExpression,
                                        SeparatedList<ExpressionSyntax>(
                                            objectArgs)))))))));

        ExpressionSyntax genericTypeForBuilding;
        if (templateArguments is not null)
        {
            genericTypeForBuilding = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    TypeOfExpression(keyName),
                    IdentifierName("MakeGenericType")))
            .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList<ArgumentSyntax>(
                        Argument(
                            IdentifierName("_types")))));
        }
        else
        {
            genericTypeForBuilding = TypeOfExpression(keyName);
        }

        statements.Add(
            ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("System.Activator"),
                        IdentifierName("CreateInstance")))
                .WithArgumentList(
                    ArgumentList(
                        SeparatedList<ArgumentSyntax>(
                            new SyntaxNodeOrToken[]{
                                Argument(genericTypeForBuilding),
                                Token(SyntaxKind.CommaToken),
                                Argument(argsName)})))));

        return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    ElementAccessExpression(
                        IdentifierName("_builders"))
                    .WithArgumentList(
                        BracketedArgumentList(
                            SingletonSeparatedList<ArgumentSyntax>(
                                Argument(
                                    TypeOfExpression(keyName))))),
                    ParenthesizedLambdaExpression()
                    .WithParameterList(
                        ParameterList(
                            SeparatedList<ParameterSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Parameter(
                                        Identifier("_types")),
                                    Token(SyntaxKind.CommaToken),
                                    Parameter(
                                        Identifier("_col"))})))
                    .WithBlock(
                        Block(List(statements)))));
    }

    private ExpressionSyntax BuildGetDefaultValueInvocation(
        ModelReference reference,
        Dictionary<TemplateArgumentReference, int> templateArgumentsMap)
    {
        var expr = ArrayCreationExpression(
                ArrayType(
                    PredefinedType(
                        Token(SyntaxKind.ObjectKeyword)))
                .WithRankSpecifiers(
                    SingletonList<ArrayRankSpecifierSyntax>(
                        ArrayRankSpecifier(
                            SingletonSeparatedList<ExpressionSyntax>(
                                OmittedArraySizeExpression())))))
            .WithInitializer(
                InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SingletonSeparatedList<ExpressionSyntax>(
                        BuildMakeGenericType(reference, templateArgumentsMap))));

        return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("_method"),
                    IdentifierName("Invoke")))
            .WithArgumentList(
                ArgumentList(
                    SeparatedList<ArgumentSyntax>(
                        new SyntaxNodeOrToken[]{
                            Argument(
                                IdentifierName("_col")),
                            Token(SyntaxKind.CommaToken),
                            Argument(expr)
                        })));
    }

    private ExpressionSyntax BuildMakeGenericType(
        ModelReference modelReference,
        Dictionary<TemplateArgumentReference, int> templateArgumentsMap)
    {
        switch (modelReference.Kind)
        {
            case ModelReferenceKind.Message:
            {
                var typeSyntax = _helpers.TransformModelReferenceToSyntax(modelReference);
                return TypeOfExpression(typeSyntax);
            }
            case ModelReferenceKind.MessageTemplate:
            {
                var @ref = (MessageTemplateReference)modelReference;
                var typeSyntax = _helpers.BuildOmittedGenericNameDefinition(@ref.Definition);
                var nodes = new List<SyntaxNodeOrToken>(2 * @ref.ModelArguments.Count);
                foreach (var subRef in @ref.ModelArguments)
                {
                    nodes.Add(Argument(BuildMakeGenericType(subRef, templateArgumentsMap)));
                    nodes.Add(Token(SyntaxKind.CommaToken));
                }
                nodes.RemoveAt(nodes.Count-1);

                return InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            TypeOfExpression(typeSyntax),
                            IdentifierName("MakeGenericType")))
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(nodes)));
            }
            case ModelReferenceKind.Tag:
            {
                var typeSyntax = _helpers.TransformModelReferenceToSyntax(modelReference);
                return TypeOfExpression(typeSyntax);
            }
            case ModelReferenceKind.TagTemplate:
            {
                var @ref = (TagTemplateReference)modelReference;
                var typeSyntax = _helpers.BuildOmittedGenericNameDefinition(@ref.Definition);
                var nodes = new List<SyntaxNodeOrToken>();
                foreach (var subRef in @ref.ModelArguments)
                {
                    nodes.Add(BuildMakeGenericType(subRef, templateArgumentsMap));
                    nodes.Add(Token(SyntaxKind.CommaToken));
                }
                nodes.RemoveAt(nodes.Count-1);
                return InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            TypeOfExpression(typeSyntax),
                            IdentifierName("MakeGenericType")))
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(nodes)));
            }
            case ModelReferenceKind.TemplateArgument:
            {
                return ElementAccessExpression(
                    IdentifierName("_types"))
                .WithArgumentList(
                    BracketedArgumentList(
                        SingletonSeparatedList<ArgumentSyntax>(
                            Argument(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(templateArgumentsMap[(TemplateArgumentReference)modelReference]))))));
            }
            default: throw new NotImplementedException();
        }
    }
}
