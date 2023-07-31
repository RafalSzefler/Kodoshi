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

internal sealed class SerializerFileGenerator
{
    private readonly ProjectContext _intputContext;
    private readonly GenerationContext _context;
    private readonly Helpers _helpers;
    private readonly Dictionary<Entities.Identifier, string> _modelNameToSerializerNameMap;
    private readonly Dictionary<Entities.Identifier, Dictionary<string, int>> _serializersProperties;
    private readonly Dictionary<string, ModelReference> _serializersToReferences = new Dictionary<string, ModelReference>();

    public SerializerFileGenerator(
        ProjectContext inputContext,
        GenerationContext context,
        Helpers helpers)
    {
        _intputContext = inputContext;
        _context = context;
        _helpers = helpers;
        var modelNameToSerializerMap = new Dictionary<Entities.Identifier, string>();
        var serializersProperties = new Dictionary<Entities.Identifier, Dictionary<string, int>>();
        var counter = 0;
        foreach (var model in Helpers.Chain(_intputContext.Project.Models, _context.ServicesTags))
        {
            modelNameToSerializerMap[model.FullName] = $"_XSerializer{counter}";
            serializersProperties[model.FullName] = CalculatePropertiesForModel(model);
            counter++;
        }
        _modelNameToSerializerNameMap = modelNameToSerializerMap;
        _serializersProperties = serializersProperties;
    }

    private Dictionary<string, int> CalculatePropertiesForModel(ModelDefinition model)
    {
        switch (model.Kind)
        {
            case ModelKind.Message:
                return CalculatePropertiesFromFields(((MessageDefinition)model).Fields);
            case ModelKind.MessageTemplate:
                return CalculatePropertiesFromFields(((MessageTemplateDefinition)model).Fields);
            case ModelKind.Tag:
                return CalculatePropertiesFromTagFields(((TagDefinition)model).Fields);
            case ModelKind.TagTemplate:
                return CalculatePropertiesFromTagFields(((TagTemplateDefinition)model).Fields);
            default: throw new NotImplementedException();
        }
    }

    private Dictionary<string, int> CalculatePropertiesFromTagFields(IReadOnlyList<TagFieldDefinition> fields)
    {
        var uintName = QualifiedName(
            QualifiedName(
                IdentifierName("Kodoshi"),
                IdentifierName("Core")),
            GenericName(
                Identifier("ISerializer"))
            .WithTypeArgumentList(
                TypeArgumentList(
                    SingletonSeparatedList<TypeSyntax>(
                        PredefinedType(
                            Token(SyntaxKind.UIntKeyword)))))).ToFullString();

        var seenTypes = new HashSet<string>()
        {
            uintName,
        };

        var parameters = new Dictionary<string, int>();
        parameters[QualifiedName(
            QualifiedName(
                IdentifierName("Kodoshi"),
                IdentifierName("Core")),
            IdentifierName("IDefaultValuesCollection")).ToFullString()] = 0;
        parameters[uintName] = 1;

        var counter = 2;
        foreach (var field in fields)
        {
            if (field.AdditionalDataType is null)
            {
                continue;
            }
            var typeName = QualifiedName(
            QualifiedName(
                IdentifierName("Kodoshi"),
                IdentifierName("Core")),
            GenericName(
                Identifier("ISerializer"))
            .WithTypeArgumentList(
                TypeArgumentList(
                    SingletonSeparatedList<TypeSyntax>(
                        _helpers.TransformModelReferenceToSyntax(field.AdditionalDataType))))).ToFullString();
            if (!seenTypes.Contains(typeName))
            {
                seenTypes.Add(typeName);
                parameters[typeName] = counter;
                counter++;
            }
            _serializersToReferences[typeName] = field.AdditionalDataType;
        }
        return parameters;
    }

    private Dictionary<string, int> CalculatePropertiesFromFields(IReadOnlyList<MessageFieldDefinition> fields)
    {
        var uintType = QualifiedName(
            QualifiedName(
                IdentifierName("Kodoshi"),
                IdentifierName("Core")),
            GenericName(
                Identifier("ISerializer"))
            .WithTypeArgumentList(
                TypeArgumentList(
                    SingletonSeparatedList<TypeSyntax>(
                        PredefinedType(
                            Token(SyntaxKind.UIntKeyword)))))).ToFullString();

        var seenTypes = new HashSet<string>()
        {
            uintType,
        };

        var parameters = new Dictionary<string, int>();
        parameters[QualifiedName(
            QualifiedName(
                IdentifierName("Kodoshi"),
                IdentifierName("Core")),
            IdentifierName("IDefaultValuesCollection")).ToFullString()] = 0;
        parameters[uintType] = 1;

        var counter = 2;
        foreach (var field in fields)
        {
            var typeName = QualifiedName(
            QualifiedName(
                IdentifierName("Kodoshi"),
                IdentifierName("Core")),
            GenericName(
                Identifier("ISerializer"))
            .WithTypeArgumentList(
                TypeArgumentList(
                    SingletonSeparatedList<TypeSyntax>(
                        _helpers.TransformModelReferenceToSyntax(field.Type))))).ToFullString();

            if (!seenTypes.Contains(typeName))
            {
                seenTypes.Add(typeName);
                parameters[typeName] = counter;
                counter++;
            }
            _serializersToReferences[typeName] = field.Type;
        }
        return parameters;
    }

    public async Task Generate(CancellationToken ct)
    {
        await Task.Yield();
        var tasks = new Task[]
        {
            GenerateSerializerCollectionFile(ct),
            GenerateSerializerCollectionFactoryFile(ct),
            GenerateSerializersFile(ct),
        };
        await Task.WhenAll(tasks);
    }

    private async Task GenerateSerializerCollectionFile(CancellationToken ct)
    {
        await Task.Yield();
        var compilationUnit = BuildSerializerCollectionFile().NormalizeWhitespace(eol: "\n");
        var result = await Helpers.SerializeNode(compilationUnit);
        var file = await (await _context.ModelsFolder).CreateFile("SerializerCollection.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildSerializerCollectionFile()
    {
        var code = @"
using System;
using Kodoshi.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NAMESPACE
{
    internal sealed class SerializerCollection : ISerializerCollection
    {
        private readonly ConcurrentDictionary<Type, object> _serializers;
        private readonly Dictionary<Type, Func<Type[], SerializerCollection, object>> _serializerBuilders;

        public SerializerCollection(
            ConcurrentDictionary<Type, object> serializers,
            Dictionary<Type, Func<Type[], SerializerCollection, object>> serializerBuilders)
        {
            _serializers = serializers;
            _serializerBuilders = serializerBuilders;
        }

        public void Dispose() { }

        public ISerializer<T> GetSerializer<T>() where T : IEquatable<T> => (ISerializer<T>)GetSerializer(typeof(T));

        internal object GetSerializer(Type type) => _serializers.GetOrAdd(type, BuildSerializer);

        private object BuildSerializer(Type t)
        {
            Func<Type[], SerializerCollection, object> builder;
            if (_serializerBuilders.TryGetValue(t, out builder!))
                return builder(Array.Empty<Type>(), this);
            if (t.IsGenericType && _serializerBuilders.TryGetValue(t.GetGenericTypeDefinition(), out builder!))
                return builder(t.GetGenericArguments(), this);
            throw new NotImplementedException($""Don't know how to construct serializer for type [{t}]."");
        }
    }
}
";
        var unit = ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = NamespaceDeclaration(ParseName(_context.CoreNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        return unit.ReplaceNode(nmspc, newNmspc);
    }

    private async Task GenerateSerializerCollectionFactoryFile(CancellationToken ct)
    {
        await Task.Yield();
        var compilationUnit = BuildSerializerCollectionFactoryFile().NormalizeWhitespace(eol: "\n");
        var result = await Helpers.SerializeNode(compilationUnit);
        var file = await (await _context.ModelsFolder).CreateFile("SerializerCollectionBuilder.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildSerializerCollectionFactoryFile()
    {
        var code = @"
namespace KodoshiGenerated.Core
{
    public sealed class SerializerCollectionBuilder
    {
        private Kodoshi.Core.IDefaultValuesCollection? _defaultValuesCollection = null;

        public SerializerCollectionBuilder SetDefaultValuesCollection(Kodoshi.Core.IDefaultValuesCollection defaultValuesCollection)
        {
            this._defaultValuesCollection = defaultValuesCollection;
            return this;
        }

        public Kodoshi.Core.ISerializerCollection Build()
        {
            var _defaultValuesCollection = this._defaultValuesCollection ?? new DefaultValuesCollectionBuilder().Build();

            var _serializers = new System.Collections.Concurrent.ConcurrentDictionary<System.Type, object>();
            var _numericSerializer = new Kodoshi.Core.BuiltIns.NumericSerializer();
            var _boolSerializer = new Kodoshi.Core.BuiltIns.BoolSerializer(_numericSerializer);
            var _byteArraySerializer = new Kodoshi.Core.BuiltIns.ByteArraySerializer(_numericSerializer);
            var _stringSerializer = new Kodoshi.Core.BuiltIns.StringSerializer(_byteArraySerializer);
            var _floatSerializer = new Kodoshi.Core.BuiltIns.FloatSerializer();
            var _doubleSerializer = new Kodoshi.Core.BuiltIns.DoubleSerializer();
            var _guidSerializer = new Kodoshi.Core.BuiltIns.GuidSerializer();

            _serializers[typeof(Kodoshi.Core.VoidType)] = Kodoshi.Core.BuiltIns.VoidTypeSerializer.Instance;
            _serializers[typeof(sbyte)] = _numericSerializer;
            _serializers[typeof(byte)] = _numericSerializer;
            _serializers[typeof(short)] = _numericSerializer;
            _serializers[typeof(ushort)] = _numericSerializer;
            _serializers[typeof(int)] = _numericSerializer;
            _serializers[typeof(uint)] = _numericSerializer;
            _serializers[typeof(long)] = _numericSerializer;
            _serializers[typeof(ulong)] = _numericSerializer;
            _serializers[typeof(bool)] = _boolSerializer;
            _serializers[typeof(Kodoshi.Core.ReadOnlyArray<byte>)] = _byteArraySerializer;
            _serializers[typeof(string)] = _stringSerializer;
            _serializers[typeof(float)] = _floatSerializer;
            _serializers[typeof(double)] = _doubleSerializer;
            _serializers[typeof(System.Guid)] = _guidSerializer;

            var _builders = new System.Collections.Generic.Dictionary<System.Type, System.Func<System.Type[], SerializerCollection, object>>();
            _builders[typeof(Kodoshi.Core.ReadOnlyArray<>)] = (_types, _ser) =>
            {
                var _serializerType = typeof(Kodoshi.Core.BuiltIns.ReadOnlyArraySerializer<>).MakeGenericType(_types);
                return System.Activator.CreateInstance(_serializerType, _ser.GetSerializer(_types[0]), _numericSerializer);
            };
            _builders[typeof(Kodoshi.Core.ReadOnlyMap<, >)] = (_types, _ser) =>
            {
                var _serializerType = typeof(Kodoshi.Core.BuiltIns.ReadOnlyMapSerializer<, >).MakeGenericType(_types);
                return System.Activator.CreateInstance(_serializerType, _ser.GetSerializer(_types[0]), _ser.GetSerializer(_types[1]), _numericSerializer);
            };

            {}

            _builders.TrimExcess();
            return new SerializerCollection(_serializers, _builders);
        }
    }
}
";
        var unit = ParseCompilationUnit(code);
        var nmspc = (NamespaceDeclarationSyntax)unit.ChildNodes().Where(x => x is NamespaceDeclarationSyntax).Single()!;
        var newNmspc = NamespaceDeclaration(ParseName(_context.CoreNamespace))
            .WithMembers(nmspc.Members)
            .WithNamespaceKeyword(Helpers.TopComment);
        var block = ((newNmspc.Members.First() as ClassDeclarationSyntax)!.Members
            .Where(x => x is MethodDeclarationSyntax m && m.Identifier.ToFullString() == "Build").First() as MethodDeclarationSyntax)!.Body!.Statements.Where(x => x is BlockSyntax).Single();
        var newBlock = Block(List(BuildSerializerStatements()));
        newNmspc = newNmspc.ReplaceNode(block, newBlock);
        return unit.ReplaceNode(nmspc, newNmspc);
    }

    private IEnumerable<StatementSyntax> BuildSerializerStatements()
    {
        var models = Helpers.Chain(_intputContext.Project.Models, _context.ServicesTags);
        foreach (var model in models)
        {
            TypeSyntax modelTypeName;
            IReadOnlyList<TemplateArgumentReference>? templateArguments = null;
            switch (model.Kind)
            {
                case ModelKind.MessageTemplate:
                {
                    var realModel = (MessageTemplateDefinition)model;
                    modelTypeName = _helpers.BuildOmittedGenericNameDefinition(realModel);
                    templateArguments = realModel.TemplateArguments;
                    break;
                }
                case ModelKind.TagTemplate:
                {
                    var realModel = (TagTemplateDefinition)model;
                    modelTypeName = _helpers.BuildOmittedGenericNameDefinition(realModel);
                    templateArguments = realModel.TemplateArguments;
                    break;
                }
                default:
                {
                    modelTypeName = _helpers.TransformModelDefinitionToSyntax(model);
                    break;
                }
            }

            var templateArgsMap = new Dictionary<TemplateArgumentReference, int>();
            var body = new List<StatementSyntax>();
            var serializerName = _modelNameToSerializerNameMap[model.FullName];

            ExpressionSyntax typeDeclaration;
            if (templateArguments is null)
            {
                typeDeclaration = TypeOfExpression(IdentifierName(serializerName));
            }
            else
            {
                var ommitedArgs = new List<SyntaxNodeOrToken>(2*templateArguments.Count);
                for (var i = 0; i < templateArguments.Count; i++)
                {
                    templateArgsMap[templateArguments[i]] = i;
                    ommitedArgs.Add(OmittedTypeArgument());
                    ommitedArgs.Add(Token(SyntaxKind.CommaToken));
                }
                ommitedArgs.RemoveAt(ommitedArgs.Count - 1);
                var genericName = GenericName(Identifier(serializerName))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SeparatedList<TypeSyntax>(ommitedArgs)));
                typeDeclaration = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        TypeOfExpression(genericName),
                        IdentifierName("MakeGenericType")))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList<ArgumentSyntax>(
                            Argument(
                                IdentifierName("_types")))));
            }

            body.Add(
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
                                Identifier("_serializerType"))
                            .WithInitializer(
                                EqualsValueClause(typeDeclaration))))));

            var objectParamsLst = new List<SyntaxNodeOrToken>()
            {
                IdentifierName("_defaultValuesCollection"),
                Token(SyntaxKind.CommaToken),
                IdentifierName("_numericSerializer"),
            };

            var props = _serializersProperties[model.FullName].OrderBy(x => x.Value).Select(x => x.Key).ToList();
            for (var i = 2; i < props.Count; i++)
            {
                var @ref = _serializersToReferences[props[i]];
                objectParamsLst.Add(Token(SyntaxKind.CommaToken));
                objectParamsLst.Add(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("_ser"),
                            IdentifierName("GetSerializer")))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList<ArgumentSyntax>(
                                Argument(BuildMakeGenericType(@ref, templateArgsMap))))));
            }

            body.Add(
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
                            Identifier("_params"))
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
                                        SeparatedList<ExpressionSyntax>(objectParamsLst)))))))));
            
            body.Add(ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("System"),
                            IdentifierName("Activator")),
                        IdentifierName("CreateInstance")))
                .WithArgumentList(
                    ArgumentList(
                        SeparatedList<ArgumentSyntax>(
                            new SyntaxNodeOrToken[]{
                                Argument(
                                    IdentifierName("_serializerType")),
                                Token(SyntaxKind.CommaToken),
                                Argument(
                                    IdentifierName("_params"))})))));

            yield return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    ElementAccessExpression(
                        IdentifierName("_builders"))
                    .WithArgumentList(
                        BracketedArgumentList(
                            SingletonSeparatedList<ArgumentSyntax>(
                                Argument(
                                    TypeOfExpression(modelTypeName))))),
                    ParenthesizedLambdaExpression()
                    .WithParameterList(
                        ParameterList(
                            SeparatedList<ParameterSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Parameter(
                                        Identifier("_types")),
                                    Token(SyntaxKind.CommaToken),
                                    Parameter(
                                        Identifier("_ser"))})))
                    .WithBlock(
                        Block(List(body)))));
        }
    }

    private async Task GenerateSerializersFile(CancellationToken ct)
    {
        await Task.Yield();
        var compilationUnit = BuildSerializersFile().NormalizeWhitespace(eol: "\n");
        var result = await Helpers.SerializeNode(compilationUnit);
        var file = await (await _context.ModelsFolder).CreateFile("Serializers.generated.cs", ct);
        await file.Write(result, ct);
    }

    private CompilationUnitSyntax BuildSerializersFile()
    {
        var topNodes = new List<SyntaxNode>(_intputContext.Project.Models.Count+2);

        var models = Helpers.Chain(_intputContext.Project.Models, _context.ServicesTags);
        foreach (var model in models)
        {
            topNodes.Add(GenerateSerializer(model));
        }

        var nmscp = NamespaceDeclaration(ParseName(_context.CoreNamespace))
            .WithMembers(List(topNodes))
            .WithNamespaceKeyword(Helpers.TopComment);

        return CompilationUnit()
            .WithMembers(SingletonList<MemberDeclarationSyntax>(nmscp));
    }

    private SyntaxNode GenerateSerializer(ModelDefinition model)
    {
        var serializerName = _modelNameToSerializerNameMap[model.FullName];
        var modelName = _helpers.TransformModelDefinitionToSyntax(model);
        IReadOnlyList<TemplateArgumentReference>? templateArgs = null;
        switch (model.Kind)
        {
            case ModelKind.MessageTemplate:
                {
                    templateArgs = ((MessageTemplateDefinition)model).TemplateArguments;
                    break;
                }
            case ModelKind.TagTemplate:
                {
                    templateArgs = ((TagTemplateDefinition)model).TemplateArguments;
                    break;
                }
            default: break;
        }

        var members = new List<MemberDeclarationSyntax>();

        var parameters = _serializersProperties[model.FullName];
        var ctrParams = new List<SyntaxNodeOrToken>(2 * parameters.Count);
        var ctrBody = new List<StatementSyntax>(parameters.Count);
        foreach (var param in parameters)
        {
            var typeId = IdentifierName(param.Key);
            var fieldId = IdentifierName($"_z{param.Value}");
            var paramId = IdentifierName($"_p{param.Value}");
            members.Add(
                FieldDeclaration(
                    VariableDeclaration(typeId)
                        .WithVariables(
                            SingletonSeparatedList<VariableDeclaratorSyntax>(
                                VariableDeclarator(fieldId.Identifier))))
                    .WithModifiers(
                        TokenList(
                            new[]{
                                Token(SyntaxKind.PrivateKeyword),
                                Token(SyntaxKind.ReadOnlyKeyword)}))
            );
            ctrParams.Add(Parameter(paramId.Identifier).WithType(typeId));
            ctrParams.Add(Token(SyntaxKind.CommaToken));
            ctrBody.Add(ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        fieldId),
                    paramId)));
        }
        ctrParams.RemoveAt(ctrParams.Count - 1);

        var ctr = ConstructorDeclaration(
                Identifier(serializerName))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(ctrParams)))
            .WithBody(Block(ctrBody));

        members.Add(ctr);
        members.Add(GenerateSerializerMethod(model));
        members.Add(GenerateDeserializerMethod(model));

        var classDefinition = ClassDeclaration(serializerName)
            .WithBaseList(
                BaseList(
                    SingletonSeparatedList<BaseTypeSyntax>(
                        SimpleBaseType(
                            QualifiedName(
                                QualifiedName(
                                    IdentifierName("Kodoshi"),
                                    IdentifierName("Core")),
                                GenericName(
                                    Identifier("ISerializer"))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SingletonSeparatedList<TypeSyntax>(
                                            modelName))))))))
            .WithModifiers(TokenList(
                    Token(SyntaxKind.InternalKeyword),
                    Token(SyntaxKind.SealedKeyword)))
            .WithMembers(List(members));

        if (templateArgs != null)
        {
            var typeParams = new List<SyntaxNodeOrToken>();
            var constraints = new List<TypeParameterConstraintClauseSyntax>();
            foreach (var templateArg in templateArgs)
            {
                var @id = (IdentifierNameSyntax)_helpers.TransformModelReferenceToSyntax(templateArg);
                typeParams.Add(TypeParameter(@id.Identifier));
                typeParams.Add(Token(SyntaxKind.CommaToken));
                constraints.Add(
                    TypeParameterConstraintClause(@id)
                    .WithConstraints(
                        SingletonSeparatedList<TypeParameterConstraintSyntax>(
                            TypeConstraint(
                                QualifiedName(
                                    IdentifierName("System"),
                                    GenericName(
                                        Identifier("IEquatable"))
                                    .WithTypeArgumentList(
                                        TypeArgumentList(
                                            SingletonSeparatedList<TypeSyntax>(
                                                @id))))))));
            }
            typeParams.RemoveAt(typeParams.Count - 1);

            classDefinition = classDefinition
                .WithTypeParameterList(
                    TypeParameterList(
                        SeparatedList<TypeParameterSyntax>(typeParams)))
                .WithConstraintClauses(
                    List<TypeParameterConstraintClauseSyntax>(constraints));
        }

        return classDefinition;
    }

    private MethodDeclarationSyntax GenerateDeserializerMethod(ModelDefinition model)
    {
        switch (model.Kind)
        {
            case ModelKind.Message:
            {
                var realModel = (MessageDefinition)model;
                return GenerateDeserializerMethodForModel(model, realModel.Fields);
            }
            case ModelKind.MessageTemplate:
            {
                var realModel = (MessageTemplateDefinition)model;
                return GenerateDeserializerMethodForModel(model, realModel.Fields);
            }
            case ModelKind.Tag:
            {
                var realModel = (TagDefinition)model;
                return GenerateDeserializerMethodForTag(model, realModel.Fields);
            }
            case ModelKind.TagTemplate:
            {
                var realModel = (TagTemplateDefinition)model;
                return GenerateDeserializerMethodForTag(model, realModel.Fields);
            }
            default: throw new NotImplementedException();
        }
    }

    private MethodDeclarationSyntax GenerateDeserializerMethodForTag(ModelDefinition model, IReadOnlyList<TagFieldDefinition> fields)
    {
        var modelName = _helpers.TransformModelDefinitionToSyntax(model);
        var genericArgs = new List<SyntaxNodeOrToken>();
        if (model.Kind == ModelKind.TagTemplate)
        {
            var templateArgs = ((TagTemplateDefinition)model).TemplateArguments;
            foreach (var arg in templateArgs)
            {
                genericArgs.Add(_helpers.TransformModelReferenceToSyntax(arg));
                genericArgs.Add(Token(SyntaxKind.CommaToken));
            }
            genericArgs.RemoveAt(genericArgs.Count - 1);
        }
        var instanceId = IdentifierName("_instance");
        var pipeWriterId = IdentifierName("_pipeWriter");
        var pipeReaderId = IdentifierName("_pipeReader");
        var ctId = IdentifierName("_ct");

        string nonGenericName;
        switch (model.Kind)
        {
            case ModelKind.TagTemplate:
            {
                nonGenericName = _helpers.BuildNonGenericVariantNameDefinition((TagTemplateDefinition)model).ToFullString();
                break;
            }
            default:
            {
                nonGenericName = _helpers.TransformModelDefinitionToSyntax(model).ToFullString();
                break;
            }
        }
        var enumName = $"{nonGenericName}.Values";
        var deserializerBody = new List<StatementSyntax>(10);

        var textBody = $@"
        {{
            var _size = (int)(await _z1.DeserializeAsync(_pipeReader, _ct).ConfigureAwait(false));
            if (_size == 0) return _z0.GetDefaultValue<{modelName.ToFullString()}>();
            
            var _readResult = await _pipeReader.ReadAtLeastAsync(_size, _ct).ConfigureAwait(false);
            _ct.ThrowIfCancellationRequested();
            if (_readResult.IsCanceled)
                throw new System.OperationCanceledException();
            if (_readResult.IsCompleted && _readResult.Buffer.Length < _size)
                throw new Kodoshi.Core.Exceptions.StreamClosedException();
            var _tmpReader = System.IO.Pipelines.PipeReader.Create(_readResult.Buffer.Slice(0, _size));
            var _tag = ({enumName})(await _z1.DeserializeAsync(_tmpReader, _ct).ConfigureAwait(false));
        }}";

        var code = ParseCompilationUnit(textBody);
        deserializerBody.AddRange(code.ChildNodes().First().ChildNodes().First().ChildNodes().Select(x => (StatementSyntax)x));

        var parameters = _serializersProperties[model.FullName];
        

        {
            var switchCases = new List<SwitchSectionSyntax>();

            foreach (var field in fields)
            {
                var enumValue = $"{enumName}.{field.Name}";
                var caseStatements = new List<StatementSyntax>(3);
                TypeSyntax _fieldType;
                string serializerField;
                
                if (field.AdditionalDataType is not null)
                {
                    _fieldType = _helpers.TransformModelReferenceToSyntax(field.AdditionalDataType);
                    var serializerType = QualifiedName(
                        QualifiedName(
                            IdentifierName("Kodoshi"),
                            IdentifierName("Core")),
                        GenericName(
                            Identifier("ISerializer"))
                        .WithTypeArgumentList(
                            TypeArgumentList(
                                SingletonSeparatedList<TypeSyntax>(_fieldType)))).ToFullString();                
                    serializerField = $"_z{parameters[serializerType]}";
                    caseStatements.Add(
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
                                        Identifier("_value"))
                                    .WithInitializer(
                                        EqualsValueClause(
                                            AwaitExpression(
                                                InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        InvocationExpression(
                                                            MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                IdentifierName(serializerField),
                                                                IdentifierName("DeserializeAsync")))
                                                        .WithArgumentList(
                                                            ArgumentList(
                                                                SeparatedList<ArgumentSyntax>(
                                                                    new SyntaxNodeOrToken[]{
                                                                        Argument(
                                                                            IdentifierName("_tmpReader")),
                                                                        Token(SyntaxKind.CommaToken),
                                                                        Argument(
                                                                            IdentifierName("_ct"))}))),
                                                        IdentifierName("ConfigureAwait")))
                                                .WithArgumentList(
                                                    ArgumentList(
                                                        SingletonSeparatedList<ArgumentSyntax>(
                                                            Argument(
                                                                LiteralExpression(
                                                                    SyntaxKind.FalseLiteralExpression))))))))))));
                }

                caseStatements.Add(
                    ExpressionStatement(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("_pipeReader"),
                                IdentifierName("AdvanceTo")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("_readResult"),
                                                    IdentifierName("Buffer")),
                                                IdentifierName("GetPosition")))
                                        .WithArgumentList(
                                            ArgumentList(
                                                SingletonSeparatedList<ArgumentSyntax>(
                                                    Argument(
                                                        IdentifierName("_size")))))))))));
                
                var argumentList = new List<ArgumentSyntax>();
                if (field.AdditionalDataType is not null)
                {
                    argumentList.Add(Argument(IdentifierName("_value")));
                }

                ExpressionSyntax invocationStatement;
                
                if (genericArgs.Count != 0)
                {
                    invocationStatement = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(nonGenericName),
                            GenericName($"Create{field.Name}").WithTypeArgumentList(TypeArgumentList(SeparatedList<TypeSyntax>(genericArgs)))))
                    .WithArgumentList(
                        ArgumentList(SeparatedList<ArgumentSyntax>(argumentList)));
                }
                else
                {
                    invocationStatement = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(nonGenericName),
                            IdentifierName($"Create{field.Name}")))
                    .WithArgumentList(
                        ArgumentList(SeparatedList<ArgumentSyntax>(argumentList)));
                }
                caseStatements.Add(ReturnStatement(invocationStatement));
                     

                switchCases.Add(
                    SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            CaseSwitchLabel(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(enumName),
                                    IdentifierName(field.Name)))))
                    .WithStatements(SingletonList<StatementSyntax>(Block(List(caseStatements)))));
            }

            switchCases.Add(SwitchSection()
                .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            DefaultSwitchLabel()))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            ThrowStatement(
                                ObjectCreationExpression(
                                    QualifiedName(
                                        QualifiedName(
                                            QualifiedName(
                                                IdentifierName("Kodoshi"),
                                                IdentifierName("Core")),
                                            IdentifierName("Exceptions")),
                                        IdentifierName("InvalidTagValueException")))
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList<ArgumentSyntax>(
                                            Argument(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    TypeOfExpression(modelName),
                                                    IdentifierName("FullName"))))))))));

            var switchNode = SwitchStatement(
                IdentifierName("_tag"))
            .WithSections(List(switchCases));

            deserializerBody.Add(switchNode);
        }

        return MethodDeclaration(
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
                            SingletonSeparatedList<TypeSyntax>(modelName)))),
                Identifier("DeserializeAsync"))
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AsyncKeyword)))
            .WithParameterList(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                        new SyntaxNodeOrToken[]{
                            Parameter(pipeReaderId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        QualifiedName(
                                            IdentifierName("System"),
                                            IdentifierName("IO")),
                                        IdentifierName("Pipelines")),
                                    IdentifierName("PipeReader"))),
                            Token(SyntaxKind.CommaToken),
                            Parameter(ctId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("System"),
                                        IdentifierName("Threading")),
                                    IdentifierName("CancellationToken")))})))
            .WithBody(
                Block(List(deserializerBody)));
    }

    private MethodDeclarationSyntax GenerateDeserializerMethodForModel(ModelDefinition model, IReadOnlyList<MessageFieldDefinition> fields)
    {
        var modelName = _helpers.TransformModelDefinitionToSyntax(model);
        var instanceId = IdentifierName("_instance");
        var pipeWriterId = IdentifierName("_pipeWriter");
        var pipeReaderId = IdentifierName("_pipeReader");
        var ctId = IdentifierName("_ct");
        var deserializerBody = new List<StatementSyntax>(10);
        deserializerBody.Add(LocalDeclarationStatement(
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
                        Identifier("_size"))
                    .WithInitializer(
                        EqualsValueClause(
                            CastExpression(
                                    PredefinedType(
                                        Token(SyntaxKind.IntKeyword)),
                                    ParenthesizedExpression(
                                        AwaitExpression(
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName("_z1"),
                                                            IdentifierName("DeserializeAsync")))
                                                    .WithArgumentList(
                                                        ArgumentList(
                                                            SeparatedList<ArgumentSyntax>(
                                                                new SyntaxNodeOrToken[]{
                                                                    Argument(
                                                                        IdentifierName("_pipeReader")),
                                                                    Token(SyntaxKind.CommaToken),
                                                                    Argument(
                                                                        IdentifierName("_ct"))}))),
                                                    IdentifierName("ConfigureAwait")))
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SingletonSeparatedList<ArgumentSyntax>(
                                                        Argument(
                                                            LiteralExpression(
                                                                SyntaxKind.FalseLiteralExpression))))))))))))));
        
        deserializerBody.Add(
             IfStatement(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        IdentifierName("_size"),
                        LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            Literal(0))),
                    Block(
                        SingletonList<StatementSyntax>(
                            ReturnStatement(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("_z0"),
                                        GenericName(
                                            Identifier("GetDefaultValue"))
                                        .WithTypeArgumentList(
                                            TypeArgumentList(
                                                SingletonSeparatedList<TypeSyntax>(modelName))))))))));

        var pre1 = ParseCompilationUnit(@"
        {
            var _readResult = await _pipeReader.ReadAtLeastAsync(_size, _ct).ConfigureAwait(false);
            _ct.ThrowIfCancellationRequested();
            if (_readResult.IsCanceled) throw new System.OperationCanceledException();
            if (_readResult.IsCompleted && _readResult.Buffer.Length < _size) throw new Kodoshi.Core.Exceptions.StreamClosedException();
            var _tmpReader = System.IO.Pipelines.PipeReader.Create(_readResult.Buffer.Slice(0, _size));
        }");
        deserializerBody.AddRange(pre1.ChildNodes().First().ChildNodes().First().ChildNodes().Select(x => (StatementSyntax)x));

        foreach (var field in fields)
        {
            var fieldType = _helpers.TransformModelReferenceToSyntax(field.Type);
            deserializerBody.Add(
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
                                Identifier($"_arg{field.Id}"))
                            .WithInitializer(
                                EqualsValueClause(
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("_z0"),
                                            GenericName(
                                                Identifier("GetDefaultValue"))
                                            .WithTypeArgumentList(
                                                TypeArgumentList(
                                                    SingletonSeparatedList<TypeSyntax>(
                                                        fieldType)))))))))));
        }

        var whileBody = new List<StatementSyntax>()
        {
            LocalDeclarationStatement(
                        VariableDeclaration(
                            PredefinedType(
                                Token(SyntaxKind.UIntKeyword)))
                        .WithVariables(
                            SingletonSeparatedList<VariableDeclaratorSyntax>(
                                VariableDeclarator(
                                    Identifier("_field"))))),
            TryStatement(
                SingletonList<CatchClauseSyntax>(
                    CatchClause()
                    .WithDeclaration(
                        CatchDeclaration(
                            QualifiedName(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("Kodoshi"),
                                        IdentifierName("Core")),
                                    IdentifierName("Exceptions")),
                                IdentifierName("StreamClosedException"))))
                    .WithBlock(
                        Block(
                            SingletonList<StatementSyntax>(
                                BreakStatement())))))
            .WithBlock(
                Block(
                    SingletonList<StatementSyntax>(
                        ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName("_field"),
                                AwaitExpression(
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("_z1"),
                                                    IdentifierName("DeserializeAsync")))
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SeparatedList<ArgumentSyntax>(
                                                        new SyntaxNodeOrToken[]{
                                                            Argument(
                                                                IdentifierName("_tmpReader")),
                                                            Token(SyntaxKind.CommaToken),
                                                            Argument(
                                                                IdentifierName("_ct"))}))),
                                            IdentifierName("ConfigureAwait")))
                                    .WithArgumentList(
                                        ArgumentList(
                                            SingletonSeparatedList<ArgumentSyntax>(
                                                Argument(
                                                    LiteralExpression(
                                                        SyntaxKind.FalseLiteralExpression))))))))))),
        };

        whileBody.Add(
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
                            Identifier("_fieldTag"))
                        .WithInitializer(
                            EqualsValueClause(
                                BinaryExpression(
                                    SyntaxKind.BitwiseAndExpression,
                                    IdentifierName("_field"),
                                    LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        Literal(
                                            "0b111",
                                            0b111)))))))));
        whileBody.Add(
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
                                Identifier("_fieldId"))
                            .WithInitializer(
                                EqualsValueClause(
                                    BinaryExpression(
                                        SyntaxKind.BitwiseAndExpression,
                                        IdentifierName("_field"),
                                        PrefixUnaryExpression(
                                            SyntaxKind.BitwiseNotExpression,
                                            CastExpression(
                                                PredefinedType(
                                                    Token(SyntaxKind.UIntKeyword)),
                                                LiteralExpression(
                                                    SyntaxKind.NumericLiteralExpression,
                                                    Literal(
                                                        "0b111",
                                                        0b111)))))))))));

        var switches = new List<SwitchSectionSyntax>();
        var parameters = _serializersProperties[model.FullName];
        foreach (var field in fields)
        {
            var fieldType = _helpers.TransformModelReferenceToSyntax(field.Type);
            var serializerType = QualifiedName(
                    QualifiedName(
                        IdentifierName("Kodoshi"),
                        IdentifierName("Core")),
                    GenericName(
                        Identifier("ISerializer"))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(
                                _helpers.TransformModelReferenceToSyntax(field.Type))))).ToFullString();
            var serializerField = $"_z{parameters[serializerType]}";
            var realFieldId = field.Id << 3;

            var caseBody = new List<StatementSyntax>
            {
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
                                Identifier("_tmp"))
                            .WithInitializer(
                                EqualsValueClause(
                                    AwaitExpression(
                                        InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName(serializerField),
                                                        IdentifierName("DeserializeAsync")))
                                                .WithArgumentList(
                                                    ArgumentList(
                                                        SeparatedList<ArgumentSyntax>(
                                                            new SyntaxNodeOrToken[]{
                                                                Argument(
                                                                    IdentifierName("_tmpReader")),
                                                                Token(SyntaxKind.CommaToken),
                                                                Argument(
                                                                    IdentifierName("_ct"))}))),
                                                IdentifierName("ConfigureAwait")))
                                        .WithArgumentList(
                                            ArgumentList(
                                                SingletonSeparatedList<ArgumentSyntax>(
                                                    Argument(
                                                        LiteralExpression(
                                                            SyntaxKind.FalseLiteralExpression))))))))))),
                IfStatement(
                    PrefixUnaryExpression(
                        SyntaxKind.LogicalNotExpression,
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("_tmp"),
                                IdentifierName("Equals")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        IdentifierName($"_arg{field.Id}")))))),
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName($"_arg{field.Id}"),
                            IdentifierName("_tmp")))),
                BreakStatement()
            };
            switches.Add(
                SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            CaseSwitchLabel(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal($"0x{realFieldId:X}", realFieldId)))))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            Block(caseBody))));
        }
        switches.Add(
            SwitchSection()
                .WithLabels(
                    SingletonList<SwitchLabelSyntax>(
                        DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        Block(
                            ExpressionStatement(
                                AwaitExpression(
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("SerializationHelpers"),
                                                    IdentifierName("ForwardReader")))
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SeparatedList<ArgumentSyntax>(
                                                        new SyntaxNodeOrToken[]{
                                                            Argument(
                                                                CastExpression(
                                                                    PredefinedType(
                                                                        Token(SyntaxKind.ByteKeyword)),
                                                                    IdentifierName("_fieldTag"))),
                                                            Token(SyntaxKind.CommaToken),
                                                            Argument(
                                                                IdentifierName("_z1")),
                                                            Token(SyntaxKind.CommaToken),
                                                            Argument(
                                                                IdentifierName("_tmpReader")),
                                                            Token(SyntaxKind.CommaToken),
                                                            Argument(
                                                                IdentifierName("_ct"))}))),
                                            IdentifierName("ConfigureAwait")))
                                    .WithArgumentList(
                                        ArgumentList(
                                            SingletonSeparatedList<ArgumentSyntax>(
                                                Argument(
                                                    LiteralExpression(
                                                        SyntaxKind.FalseLiteralExpression))))))),
                            BreakStatement()))));

        whileBody.Add(
            SwitchStatement(
                IdentifierName("_fieldId"))
            .WithSections(
                List<SwitchSectionSyntax>(switches)));

        deserializerBody.Add(
            WhileStatement(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("_ct"),
                        IdentifierName("IsCancellationRequested"))),
                Block(List(whileBody))));
        
        deserializerBody.Add(
            ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("_pipeReader"),
                        IdentifierName("AdvanceTo")))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList<ArgumentSyntax>(
                            Argument(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("_readResult"),
                                            IdentifierName("Buffer")),
                                        IdentifierName("GetPosition")))
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList<ArgumentSyntax>(
                                            Argument(
                                                IdentifierName("_size")))))))))));

        deserializerBody.Add(
            ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("_ct"),
                            IdentifierName("ThrowIfCancellationRequested")))));

        var args = new List<SyntaxNodeOrToken>(2*fields.Count);
        foreach (var field in fields)
        {
            args.Add(Argument(IdentifierName($"_arg{field.Id}")));
            args.Add(Token(SyntaxKind.CommaToken));
        }
        args.RemoveAt(args.Count - 1);

        deserializerBody.Add(
            ReturnStatement(
                    ObjectCreationExpression(modelName)
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(args)))));

        return MethodDeclaration(
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
                            SingletonSeparatedList<TypeSyntax>(modelName)))),
                Identifier("DeserializeAsync"))
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AsyncKeyword)))
            .WithParameterList(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                        new SyntaxNodeOrToken[]{
                            Parameter(pipeReaderId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        QualifiedName(
                                            IdentifierName("System"),
                                            IdentifierName("IO")),
                                        IdentifierName("Pipelines")),
                                    IdentifierName("PipeReader"))),
                            Token(SyntaxKind.CommaToken),
                            Parameter(ctId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("System"),
                                        IdentifierName("Threading")),
                                    IdentifierName("CancellationToken")))})))
            .WithBody(
                Block(List(deserializerBody)));
    }

    private MemberDeclarationSyntax GenerateSerializerMethod(ModelDefinition model)
    {
        switch (model.Kind)
        {
            case ModelKind.Message:
            {
                var realModel = (MessageDefinition)model;
                return GenerateSerializerMethodForModel(model, realModel.Fields);
            }
            case ModelKind.MessageTemplate:
            {
                var realModel = (MessageTemplateDefinition)model;
                return GenerateSerializerMethodForModel(model, realModel.Fields);
            }
            case ModelKind.Tag:
            {
                var realModel = (TagDefinition)model;
                return GenerateSerializerMethodForTag(model, realModel.Fields);
            }
            case ModelKind.TagTemplate:
            {
                var realModel = (TagTemplateDefinition)model;
                return GenerateSerializerMethodForTag(model, realModel.Fields);
            }
            default: throw new NotImplementedException();
        }
    }

    private MemberDeclarationSyntax GenerateSerializerMethodForTag(ModelDefinition model, IReadOnlyList<TagFieldDefinition> fields)
    {
        string nonGenericName;
        switch (model.Kind)
        {
            case ModelKind.TagTemplate:
            {
                nonGenericName = _helpers.BuildNonGenericVariantNameDefinition((TagTemplateDefinition)model).ToFullString();
                break;
            }
            default:
            {
                nonGenericName = _helpers.TransformModelDefinitionToSyntax(model).ToFullString();
                break;
            }
        }
        var enumName = $"{nonGenericName}.Values";
        var instanceId = IdentifierName("_instance");
        var pipeWriterId = IdentifierName("_pipeWriter");
        var ctId = IdentifierName("_ct");
        var modelName = _helpers.TransformModelDefinitionToSyntax(model);
        var serializerBody = new List<StatementSyntax>(10);

        var textBody = $@"
        {{
            if (_instance.Equals(_z0.GetDefaultValue<{modelName.ToFullString()}>()))
            {{
                await _z1.SerializeAsync(0, _pipeWriter, _ct).ConfigureAwait(false);
                return;
            }}
            
            using var _stream = new System.IO.MemoryStream();
            var _tmpWriter = System.IO.Pipelines.PipeWriter.Create(_stream, SerializationHelpers.WriterOptions);

            await _z1.SerializeAsync((uint)_instance.Tag, _tmpWriter, _ct).ConfigureAwait(false);

            {{ }}

            await _tmpWriter.CompleteAsync().ConfigureAwait(false);
            var _writtenBytes = (int)_stream.Position;
            _stream.Seek(0, System.IO.SeekOrigin.Begin);
            await _z1.SerializeAsync((uint)_writtenBytes, _pipeWriter, _ct).ConfigureAwait(false);
            var _tmpReader = System.IO.Pipelines.PipeReader.Create(_stream);
            await _tmpReader.CopyToAsync(_pipeWriter, _ct).ConfigureAwait(false);
            await _tmpReader.CompleteAsync().ConfigureAwait(false);
        }}";

        var code = ParseCompilationUnit(textBody);
        CompilationUnitSyntax realCode;
        var parameters = _serializersProperties[model.FullName];
        

        {
            var internalBlock = (BlockSyntax)code.ChildNodes().First().ChildNodes().First().ChildNodes().Where(x => x is BlockSyntax).First();
            var switchCases = new List<SwitchSectionSyntax>();
            var props = _serializersProperties[model.FullName];

            foreach (var field in fields)
            {
                if (field.AdditionalDataType is null) continue;
                var _fieldType = _helpers.TransformModelReferenceToSyntax(field.AdditionalDataType);
                var serializerType = QualifiedName(
                    QualifiedName(
                        IdentifierName("Kodoshi"),
                        IdentifierName("Core")),
                    GenericName(
                        Identifier("ISerializer"))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(_fieldType)))).ToFullString();                
                var serializerField = $"_z{parameters[serializerType]}";
                var castExpr = CastExpression(
                        _fieldType,
                        PostfixUnaryExpression(
                            SyntaxKind.SuppressNullableWarningExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("_instance"),
                                IdentifierName("Data"))));
                var switchStatements = new StatementSyntax[]
                {
                    Block(
                        ExpressionStatement(
                            AwaitExpression(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(serializerField),
                                        IdentifierName("SerializeAsync")))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList<ArgumentSyntax>(
                                            new SyntaxNodeOrToken[]{
                                                Argument(castExpr),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    IdentifierName("_tmpWriter")),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    IdentifierName("_ct"))}))))),
                        BreakStatement())
                };

                switchCases.Add(
                     SwitchSection()
                        .WithLabels(
                            SingletonList<SwitchLabelSyntax>(
                                CaseSwitchLabel(MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(enumName),
                                    IdentifierName(field.Name)))))
                        .WithStatements(List(switchStatements)));
            }

            if (switchCases is null)
            {
                realCode = code.RemoveNode(internalBlock, SyntaxRemoveOptions.KeepNoTrivia)!;
            }
            else
            {
                switchCases.Add(
                    SwitchSection()
                        .WithLabels(
                            SingletonList<SwitchLabelSyntax>(
                                DefaultSwitchLabel()))
                        .WithStatements(
                            SingletonList<StatementSyntax>(
                                BreakStatement())));
                var switchStatement = SwitchStatement(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("_instance"),
                        IdentifierName("Tag")))
                .WithSections(List(switchCases));
                realCode = code.ReplaceNode(internalBlock, switchStatement);
            }
        }
        serializerBody.AddRange(realCode.ChildNodes().First().ChildNodes().First().ChildNodes().Select(x => (StatementSyntax)x));

        
        return MethodDeclaration(
                QualifiedName(
                    QualifiedName(
                        QualifiedName(
                            IdentifierName("System"),
                            IdentifierName("Threading")),
                        IdentifierName("Tasks")),
                    IdentifierName("ValueTask")),
                Identifier("SerializeAsync"))
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AsyncKeyword)))
            .WithParameterList(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                        new SyntaxNodeOrToken[]{
                            Parameter(instanceId.Identifier)
                            .WithType(modelName),
                            Token(SyntaxKind.CommaToken),
                            Parameter(pipeWriterId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        QualifiedName(
                                            IdentifierName("System"),
                                            IdentifierName("IO")),
                                        IdentifierName("Pipelines")),
                                    IdentifierName("PipeWriter"))),
                            Token(SyntaxKind.CommaToken),
                            Parameter(ctId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("System"),
                                        IdentifierName("Threading")),
                                    IdentifierName("CancellationToken")))})))
            .WithBody(
                Block(List(serializerBody)));
    }

    private MemberDeclarationSyntax GenerateSerializerMethodForModel(ModelDefinition model, IReadOnlyList<MessageFieldDefinition> fields)
    {
        var instanceId = IdentifierName("_instance");
        var pipeWriterId = IdentifierName("_pipeWriter");
        var ctId = IdentifierName("_ct");
        var modelName = _helpers.TransformModelDefinitionToSyntax(model);
        var serializerBody = new List<StatementSyntax>(10);
        var textBody = $@"
        {{
            if (_instance.Equals(_z0.GetDefaultValue<{modelName.ToFullString()}>()))
            {{
                await _z1.SerializeAsync(0, _pipeWriter, _ct).ConfigureAwait(false);
                return;
            }}
            using var _stream = new System.IO.MemoryStream();
            var _tmpWriter = System.IO.Pipelines.PipeWriter.Create(_stream, SerializationHelpers.WriterOptions);

            {{ }}

            await _tmpWriter.CompleteAsync().ConfigureAwait(false);
            var _writtenBytes = (int)_stream.Position;
            _stream.Seek(0, System.IO.SeekOrigin.Begin);
            await _z1.SerializeAsync((uint)_writtenBytes, _pipeWriter, _ct).ConfigureAwait(false);
            var _tmpReader = System.IO.Pipelines.PipeReader.Create(_stream);
            await _tmpReader.CopyToAsync(_pipeWriter, _ct).ConfigureAwait(false);
            await _tmpReader.CompleteAsync().ConfigureAwait(false);
        }}";
        var code = ParseCompilationUnit(textBody);
        CompilationUnitSyntax realCode;
        var parameters = _serializersProperties[model.FullName];

        {
            var internalBlock = (BlockSyntax)code.ChildNodes().First().ChildNodes().First().ChildNodes().Where(x => x is BlockSyntax).First();
            var localStatements = new List<StatementSyntax>();
            foreach (var field in fields)
            {
                var numericTag = field.Id << 3;
                var fieldType = _helpers.TransformModelReferenceToSyntax(field.Type);
                var serializerType = QualifiedName(
                        QualifiedName(
                            IdentifierName("Kodoshi"),
                            IdentifierName("Core")),
                        GenericName(
                            Identifier("ISerializer"))
                        .WithTypeArgumentList(
                            TypeArgumentList(
                                SingletonSeparatedList<TypeSyntax>(
                                    _helpers.TransformModelReferenceToSyntax(field.Type))))).ToFullString();
                var serializerField = $"_z{parameters[serializerType]}";

                var ifBlock = new List<StatementSyntax>(10);
                ifBlock.Add(LocalDeclarationStatement(
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
                                Identifier("_tag"))
                            .WithInitializer(
                                EqualsValueClause(
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("SerializationHelpers"),
                                            GenericName(
                                                Identifier("GetTagValue"))
                                            .WithTypeArgumentList(
                                                TypeArgumentList(
                                                    SingletonSeparatedList<TypeSyntax>(
                                                        fieldType)))))))))));
                ifBlock.Add(
                    ExpressionStatement(
                    AwaitExpression(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("_z1"),
                                        IdentifierName("SerializeAsync")))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList<ArgumentSyntax>(
                                            new SyntaxNodeOrToken[]{
                                                Argument(
                                                    CastExpression(
                                                        PredefinedType(Token(SyntaxKind.UIntKeyword)),
                                                        BinaryExpression(
                                                            SyntaxKind.BitwiseOrExpression,
                                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal($"0x{numericTag:X}", numericTag)),
                                                            IdentifierName("_tag")))),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    IdentifierName("_tmpWriter")),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    IdentifierName("_ct"))}))),
                                IdentifierName("ConfigureAwait")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        LiteralExpression(
                                            SyntaxKind.FalseLiteralExpression))))))));
                ifBlock.Add(
                    ExpressionStatement(
                    AwaitExpression(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(serializerField),
                                        IdentifierName("SerializeAsync")))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList<ArgumentSyntax>(
                                            new SyntaxNodeOrToken[]{
                                                Argument(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName("_instance"),
                                                        IdentifierName(field.Name))),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    IdentifierName("_tmpWriter")),
                                                Token(SyntaxKind.CommaToken),
                                                Argument(
                                                    IdentifierName("_ct"))}))),
                                IdentifierName("ConfigureAwait")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        LiteralExpression(
                                            SyntaxKind.FalseLiteralExpression))))))));

                localStatements.Add(IfStatement(
                    PrefixUnaryExpression(
                        SyntaxKind.LogicalNotExpression,
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("_instance"),
                                    IdentifierName(field.Name)),
                                IdentifierName("Equals")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("_z0"),
                                                GenericName(
                                                    Identifier("GetDefaultValue"))
                                                .WithTypeArgumentList(
                                                    TypeArgumentList(
                                                        SingletonSeparatedList<TypeSyntax>(
                                                            fieldType)))))))))),
                    Block(List(ifBlock))));
            }
            var newBlock = Block(List(localStatements));
            realCode = code.ReplaceNode(internalBlock, newBlock.ChildNodes());
        }
        serializerBody.AddRange(realCode.ChildNodes().First().ChildNodes().First().ChildNodes().Select(x => (StatementSyntax)x));

        return MethodDeclaration(
                QualifiedName(
                    QualifiedName(
                        QualifiedName(
                            IdentifierName("System"),
                            IdentifierName("Threading")),
                        IdentifierName("Tasks")),
                    IdentifierName("ValueTask")),
                Identifier("SerializeAsync"))
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AsyncKeyword)))
            .WithParameterList(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                        new SyntaxNodeOrToken[]{
                            Parameter(instanceId.Identifier)
                            .WithType(modelName),
                            Token(SyntaxKind.CommaToken),
                            Parameter(pipeWriterId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        QualifiedName(
                                            IdentifierName("System"),
                                            IdentifierName("IO")),
                                        IdentifierName("Pipelines")),
                                    IdentifierName("PipeWriter"))),
                            Token(SyntaxKind.CommaToken),
                            Parameter(ctId.Identifier)
                            .WithType(
                                QualifiedName(
                                    QualifiedName(
                                        IdentifierName("System"),
                                        IdentifierName("Threading")),
                                    IdentifierName("CancellationToken")))})))
            .WithBody(
                Block(List(serializerBody)));
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
