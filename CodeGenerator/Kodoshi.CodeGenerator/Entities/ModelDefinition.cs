using System.Collections.Generic;

namespace Kodoshi.CodeGenerator.Entities;

public abstract class ModelDefinition
{
    public ModelKind Kind { get; }
    public Identifier FullName { get; }

    protected ModelDefinition(ModelKind kind, Identifier fullName)
    {
        Kind = kind;
        FullName = fullName;
    }
}

public sealed class MessageDefinition : ModelDefinition
{
    public IReadOnlyList<MessageFieldDefinition> Fields { get; }
    public MessageDefinition(
        Identifier fullName,
        IReadOnlyList<MessageFieldDefinition> fields)
        : base(ModelKind.Message, fullName)
    {
        Fields = fields;
    }
}

public sealed class MessageTemplateDefinition : ModelDefinition
{
    public IReadOnlyList<TemplateArgumentReference> TemplateArguments { get; }
    public IReadOnlyList<MessageFieldDefinition> Fields { get; }
    public MessageTemplateDefinition(
        Identifier fullName,
        IReadOnlyList<TemplateArgumentReference> templateArguments,
        IReadOnlyList<MessageFieldDefinition> fields)
        : base(ModelKind.MessageTemplate, fullName)
    {
        TemplateArguments = templateArguments;
        Fields = fields;
    }
}

public sealed class TagDefinition : ModelDefinition
{
    public IReadOnlyList<TagFieldDefinition> Fields { get; }

    public TagDefinition(
        Identifier fullName,
        IReadOnlyList<TagFieldDefinition> fields)
        : base(ModelKind.Tag, fullName)
    {
        Fields = fields;
    }
}

public sealed class TagTemplateDefinition : ModelDefinition
{
    public IReadOnlyList<TemplateArgumentReference> TemplateArguments { get; }
    public IReadOnlyList<TagFieldDefinition> Fields { get; }

    public TagTemplateDefinition(
        Identifier fullName,
        IReadOnlyList<TemplateArgumentReference> templateArguments,
        IReadOnlyList<TagFieldDefinition> fields)
        : base(ModelKind.TagTemplate, fullName)
    {
        Fields = fields;
        TemplateArguments = templateArguments;
    }
}
