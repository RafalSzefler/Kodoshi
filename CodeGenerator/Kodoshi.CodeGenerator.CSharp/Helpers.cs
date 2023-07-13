using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.Entities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kodoshi.CodeGenerator.CSharp;

internal sealed class Helpers
{
    private readonly GenerationContext _context;

    public Helpers(GenerationContext context)
    {
        _context = context;
    }

    private static readonly Dictionary<ModelDefinition, TypeSyntax> _builtInMappings
        = new Dictionary<ModelDefinition, TypeSyntax>
        {
            { BuiltIns.BoolModel, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)) },
            { BuiltIns.Float32Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword)) },
            { BuiltIns.Float64Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DoubleKeyword)) },
            { BuiltIns.Int8Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.SByteKeyword)) },
            { BuiltIns.Int16Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ShortKeyword)) },
            { BuiltIns.Int32Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)) },
            { BuiltIns.Int64Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)) },
            { BuiltIns.UInt8Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ByteKeyword)) },
            { BuiltIns.UInt16Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UShortKeyword)) },
            { BuiltIns.UInt32Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UIntKeyword)) },
            { BuiltIns.UInt64Model, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ULongKeyword)) },
            { BuiltIns.StringModel, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)) },
            { BuiltIns.UuidModel, SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Guid")) },
            { BuiltIns.VoidModel, SyntaxFactory.ParseTypeName("Kodoshi.Core.VoidType") },
        };

    private readonly ConcurrentDictionary<TemplateArgumentReference, int> _templateIds
        = new ConcurrentDictionary<TemplateArgumentReference, int>();
    private int _currentTemplateId = 0;
    public TypeSyntax TransformModelReferenceToSyntax(ModelReference reference)
    {
        switch (reference.Kind)
        {
            case ModelReferenceKind.Message:
                return GetSyntaxFromMessageReference((MessageReference)reference);
            case ModelReferenceKind.MessageTemplate:
                return GetSyntaxFromMessageTemplateDefinition((MessageTemplateReference)reference);
            case ModelReferenceKind.TemplateArgument:
            {
                var @ref = (TemplateArgumentReference)reference;
                var templateId = _templateIds.GetOrAdd(@ref, (key) => Interlocked.Increment(ref _currentTemplateId));
                return SyntaxFactory.IdentifierName($"T{templateId}");
            }
            case ModelReferenceKind.Tag:
                return GetSyntaxFromTagDefinition((TagReference)reference);
            case ModelReferenceKind.TagTemplate:
                return GetSyntaxFromTagTemplateDefinition((TagTemplateReference)reference);
            default:
                throw new NotImplementedException();
        }
    }

    private TypeSyntax GetSyntaxFromTagDefinition(TagReference reference)
    {
        var definition = reference.Definition;
        if (_builtInMappings.TryGetValue(definition, out var result))
        {
            return result;
        }
        var fullName = definition.FullName;
        NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
        if (!string.IsNullOrEmpty(fullName.Namespace))
        {
            nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
        }
        return SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Name));
    }

    private TypeSyntax GetSyntaxFromTagTemplateDefinition(TagTemplateReference reference)
    {
        var nmspc = GetBaseNameSyntaxFromTemplateMessageDefinition(reference);
        var arguments = reference.ModelArguments;
        var c = arguments.Count;
        var res = new List<SyntaxNodeOrToken>(c);
        if (c > 0)
        {
            res.Add(TransformModelReferenceToSyntax(arguments[0]));
        }
        for (var i = 1; i < c; i++)
        {
            res.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            res.Add(TransformModelReferenceToSyntax(arguments[i]));
        }
        var nmspcC = nmspc.Count;
        NameSyntax result = SyntaxFactory.IdentifierName(nmspc[0]);
        for (var i = 1; i < nmspcC - 1; i++)
        {
            result = SyntaxFactory.QualifiedName(result, SyntaxFactory.IdentifierName(nmspc[i]));
        }
        SimpleNameSyntax last = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier(nmspc[nmspcC - 1]),
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(res)));
        
        return SyntaxFactory.QualifiedName(result, last);
    }

    private List<string> GetBaseNameSyntaxFromTemplateMessageDefinition(MessageTemplateReference @ref)
    {
        if (@ref.Definition == BuiltIns.ArrayModel)
        {
            return new List<string> { "Kodoshi.Core", "ReadOnlyArray" };
        }
        else if (@ref.Definition == BuiltIns.MapModel)
        {
            return new List<string> { "Kodoshi.Core", "ReadOnlyMap" };
        }
        else
        {
            var lst = new List<string>(3);
            lst.Add(_context.ModelsNamespace);
            var fullName = @ref.Definition.FullName;
            if (!string.IsNullOrEmpty(fullName.Namespace))
            {
                lst.Add(fullName.Namespace);
            }
            lst.Add(fullName.Name);
            return lst;
        }
    }

    private List<string> GetBaseNameSyntaxFromTemplateMessageDefinition(TagTemplateReference @ref)
    {
        var lst = new List<string>(3);
        lst.Add(_context.ModelsNamespace);
        var fullName = @ref.Definition.FullName;
        if (!string.IsNullOrEmpty(fullName.Namespace))
        {
            lst.Add(fullName.Namespace);
        }
        lst.Add(fullName.Name);
        return lst;
    }

    private TypeSyntax GetSyntaxFromMessageTemplateDefinition(MessageTemplateReference @ref)
    {
        var nmspc = GetBaseNameSyntaxFromTemplateMessageDefinition(@ref);
        var arguments = @ref.ModelArguments;
        var c = arguments.Count;
        var res = new List<SyntaxNodeOrToken>(c);
        if (c > 0)
        {
            res.Add(TransformModelReferenceToSyntax(arguments[0]));
        }
        for (var i = 1; i < c; i++)
        {
            res.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            res.Add(TransformModelReferenceToSyntax(arguments[i]));
        }
        var nmspcC = nmspc.Count;
        NameSyntax result = SyntaxFactory.IdentifierName(nmspc[0]);
        for (var i = 1; i < nmspcC - 1; i++)
        {
            result = SyntaxFactory.QualifiedName(result, SyntaxFactory.IdentifierName(nmspc[i]));
        }
        SimpleNameSyntax last = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier(nmspc[nmspcC - 1]),
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(res)));
        
        return SyntaxFactory.QualifiedName(result, last);
    }

    private TypeSyntax GetSyntaxFromMessageReference(MessageReference reference)
    {
        var definition = reference.Definition;
        if (_builtInMappings.TryGetValue(definition, out var result))
        {
            return result;
        }
        var fullName = definition.FullName;
        NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
        if (!string.IsNullOrEmpty(fullName.Namespace))
        {
            nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
        }
        return SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Name));
    }

    public static async Task<byte[]> SerializeNode(SyntaxNode finalNode)
    {
        using (var stream = new MemoryStream())
        using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            finalNode.WriteTo(writer);
            await writer.FlushAsync();
            await stream.FlushAsync();
            stream.Seek(0, SeekOrigin.Begin);
            return stream.ToArray();
        }
    }

    private static readonly string _now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public static readonly SyntaxToken TopComment =
        SyntaxFactory.Token(
            SyntaxFactory.TriviaList(
                new[] {
                    SyntaxFactory.Comment("// <auto-generated>"),
                    SyntaxFactory.Comment("//   Kodoshi generated source code."),
                    SyntaxFactory.Comment($"//   Generated at {_now}."),
                    SyntaxFactory.Comment("// </auto-generated>"),
                    SyntaxFactory.Trivia(
                        SyntaxFactory.NullableDirectiveTrivia(
                            SyntaxFactory.Token(SyntaxKind.EnableKeyword),
                            true)),
                }),
            SyntaxKind.NamespaceKeyword,
            SyntaxFactory.TriviaList());
    
    public TypeSyntax BuildOmittedGenericNameDefinition(TagTemplateDefinition definition)
    {
        var fullName = definition.FullName;
        NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
        if (!string.IsNullOrEmpty(fullName.Namespace))
        {
            nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
        }
        var arguments = definition.TemplateArguments;
        var c = arguments.Count;
        var res = new List<SyntaxNodeOrToken>(c);
        if (c > 0)
        {
            res.Add(SyntaxFactory.OmittedTypeArgument());
        }
        for (var i = 1; i < c; i++)
        {
            res.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            res.Add(SyntaxFactory.OmittedTypeArgument());
        }
        
        return SyntaxFactory.QualifiedName(
            nmspc,
            SyntaxFactory.GenericName(
                fullName.Name)
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        res))));
    }

    public TypeSyntax BuildNonGenericVariantNameDefinition(TagTemplateDefinition definition)
    {
        var fullName = definition.FullName;
        NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
        if (!string.IsNullOrEmpty(fullName.Namespace))
        {
            nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
        }
        
        return SyntaxFactory.QualifiedName(
            nmspc,
            SyntaxFactory.IdentifierName(fullName.Name));
    }

    public TypeSyntax BuildOmittedGenericNameDefinition(MessageTemplateDefinition definition)
    {
        if (definition == BuiltIns.ArrayModel)
        {
            return SyntaxFactory.QualifiedName(
                SyntaxFactory.IdentifierName("Kodoshi.Core"),
                SyntaxFactory.GenericName(
                    "ReadOnlyArray")
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList<TypeSyntax>(
                            new[] { SyntaxFactory.OmittedTypeArgument() }))));
        }
        else if (definition == BuiltIns.MapModel)
        {
            return SyntaxFactory.QualifiedName(
                SyntaxFactory.IdentifierName("Kodoshi.Core"),
                SyntaxFactory.GenericName(
                    "ReadOnlyMap")
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList<TypeSyntax>(
                            new[] { SyntaxFactory.OmittedTypeArgument(), SyntaxFactory.OmittedTypeArgument() }))));
        }
        var fullName = definition.FullName;
        NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
        if (!string.IsNullOrEmpty(fullName.Namespace))
        {
            nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
        }
        var arguments = definition.TemplateArguments;
        var c = arguments.Count;
        var res = new List<SyntaxNodeOrToken>(c);
        if (c > 0)
        {
            res.Add(SyntaxFactory.OmittedTypeArgument());
        }
        for (var i = 1; i < c; i++)
        {
            res.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            res.Add(SyntaxFactory.OmittedTypeArgument());
        }
        
        return SyntaxFactory.QualifiedName(
            nmspc,
            SyntaxFactory.GenericName(
                fullName.Name)
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        res))));
    }

    public TypeSyntax TransformModelDefinitionToSyntax(ModelDefinition definition)
    {
        switch (definition.Kind)
        {
            case ModelKind.Message:
            {
                if (_builtInMappings.TryGetValue(definition, out var result))
                {
                    return result;
                }
                var fullName = definition.FullName;
                NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
                if (!string.IsNullOrEmpty(fullName.Namespace))
                {
                    nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
                }
                return SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Name));
            }
            case ModelKind.MessageTemplate:
            {
                var realDefinition = (MessageTemplateDefinition)definition;
                var fullName = realDefinition.FullName;
                NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
                if (!string.IsNullOrEmpty(fullName.Namespace))
                {
                    nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
                }
                var arguments = realDefinition.TemplateArguments;
                var c = arguments.Count;
                var res = new List<SyntaxNodeOrToken>(c);
                if (c > 0)
                {
                    res.Add(TransformModelReferenceToSyntax(arguments[0]));
                }
                for (var i = 1; i < c; i++)
                {
                    res.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                    res.Add(TransformModelReferenceToSyntax(arguments[i]));
                }
                return SyntaxFactory.QualifiedName(
                    nmspc,
                    SyntaxFactory.GenericName(
                        fullName.Name)
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList<TypeSyntax>(
                                res))));
            }
            case ModelKind.Tag:
            {
                var fullName = definition.FullName;
                NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
                if (!string.IsNullOrEmpty(fullName.Namespace))
                {
                    nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
                }
                return SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Name));
            }
            case ModelKind.TagTemplate:
            {
                var realDefinition = (TagTemplateDefinition)definition;
                var fullName = realDefinition.FullName;
                NameSyntax nmspc = SyntaxFactory.IdentifierName(_context.ModelsNamespace);
                if (!string.IsNullOrEmpty(fullName.Namespace))
                {
                    nmspc = SyntaxFactory.QualifiedName(nmspc, SyntaxFactory.IdentifierName(fullName.Namespace));
                }
                var arguments = realDefinition.TemplateArguments;
                var c = arguments.Count;
                var res = new List<SyntaxNodeOrToken>(c);
                if (c > 0)
                {
                    res.Add(TransformModelReferenceToSyntax(arguments[0]));
                }
                for (var i = 1; i < c; i++)
                {
                    res.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
                    res.Add(TransformModelReferenceToSyntax(arguments[i]));
                }
                return SyntaxFactory.QualifiedName(
                    nmspc,
                    SyntaxFactory.GenericName(
                        fullName.Name)
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList<TypeSyntax>(
                                res))));
            }
            default:
                throw new NotImplementedException();
        }
    }

    public static IEnumerable<T> Chain<T>(IEnumerable<T>? first, IEnumerable<T>? second)
    {
        if (first is not null)
            foreach (var item in first)
            {
                yield return item;
            }
        if (second is not null)
            foreach (var item in second)
            {
                yield return item;
            }
    }
}
