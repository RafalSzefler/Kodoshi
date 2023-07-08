using System;
using System.Collections.Generic;
using System.Threading;

namespace Kodoshi.CodeGenerator.Entities;

public abstract class ModelReference : IEquatable<ModelReference>
{
    public ModelReferenceKind Kind { get; }
    private int ReferenceId { get; }
    private static int _globalCounter = 0;

    protected ModelReference(ModelReferenceKind kind)
    {
        Kind = kind;
        ReferenceId = Interlocked.Increment(ref _globalCounter);
    }

    public bool Equals(ModelReference other)
    {
        #nullable disable
        if (this is null && other is null)
        {
            return true;
        }
        if (this is null || other is null)
        {
            return false;
        }
        return this.ReferenceId == other.ReferenceId;
        #nullable restore
    }

    public override bool Equals(object obj)
        => obj is ModelReference model && Equals(model);

    public override int GetHashCode() => ReferenceId.GetHashCode();

    public static bool operator==(ModelReference left, ModelReference right)
        => left.Equals(right);

    public static bool operator!=(ModelReference left, ModelReference right)
        => !left.Equals(right);
}

public sealed class MessageReference : ModelReference
{
    public MessageDefinition Definition { get; }

    public MessageReference(MessageDefinition definition)
        : base(ModelReferenceKind.Message)
    {
        Definition = definition;
    }
}

public sealed class MessageTemplateReference : ModelReference
{
    public MessageTemplateDefinition Definition { get; }
    public IReadOnlyList<ModelReference> ModelArguments { get; }

    public MessageTemplateReference(
        MessageTemplateDefinition definition,
        IReadOnlyList<ModelReference> modelArguments
    )
        : base(ModelReferenceKind.MessageTemplate)
    {
        Definition = definition;
        ModelArguments = modelArguments;
    }
}

public sealed class TagReference : ModelReference
{
    public TagDefinition Definition { get; }
    public TagReference(TagDefinition definition)
        : base(ModelReferenceKind.Tag)
    {
        Definition = definition;
    }
}

public sealed class TagTemplateReference : ModelReference
{
    public TagTemplateDefinition Definition { get; }
    public IReadOnlyList<ModelReference> ModelArguments { get; }
    public TagTemplateReference(
        TagTemplateDefinition definition,
        IReadOnlyList<ModelReference> modelArguments)
        : base(ModelReferenceKind.TagTemplate)
    {
        Definition = definition;
        ModelArguments = modelArguments;
    }
}

public sealed class TemplateArgumentReference : ModelReference
{
    public TemplateArgumentReference()
        : base(ModelReferenceKind.TemplateArgument)
    { }
}
