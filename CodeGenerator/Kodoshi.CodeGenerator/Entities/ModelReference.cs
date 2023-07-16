using System.Collections.Generic;
using System.Threading;

namespace Kodoshi.CodeGenerator.Entities;

public abstract class ModelReference
{
    public ModelReferenceKind Kind { get; }
    public int ReferenceId { get; }
    private static int _globalCounter = 0;

    protected ModelReference(ModelReferenceKind kind)
    {
        Kind = kind;
        ReferenceId = Interlocked.Increment(ref _globalCounter);
    }
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
