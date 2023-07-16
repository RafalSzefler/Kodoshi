using System;
using System.Text;
using Kodoshi.CodeGenerator.Entities;

namespace Kodoshi.CodeGenerator.InputLoader;

internal static class Stringifier
{
    public static string ToString(Identifier id)
    {
        var name = id.Name;
        if (!string.IsNullOrEmpty(id.Namespace))
            name = $"{id.Namespace}.{id.Name}";
        return name;
    }

    public static string ToString(ModelReference @ref)
    {
        switch (@ref.Kind)
        {
            case ModelReferenceKind.TemplateArgument:
                return "x-" + @ref.ReferenceId.ToString();
            case ModelReferenceKind.Message:
                return "m-" + ToString(((MessageReference)@ref).Definition.FullName);
            case ModelReferenceKind.MessageTemplate:
            {
                var realRef = (MessageTemplateReference)@ref;
                var contentBuilder = new StringBuilder();
                contentBuilder
                    .Append("mt-")
                    .Append(ToString(realRef.Definition.FullName))
                    .Append("<");
                foreach (var tmplArg in realRef.ModelArguments)
                    contentBuilder.Append(ToString(tmplArg));
                contentBuilder.Append(">");
                return contentBuilder.ToString();
            }
            case ModelReferenceKind.Tag:
                return "t-" + ToString(((TagReference)@ref).Definition.FullName);
            case ModelReferenceKind.TagTemplate:
            {
                var realRef = (TagTemplateReference)@ref;
                var contentBuilder = new StringBuilder();
                contentBuilder
                    .Append("tt-")
                    .Append(ToString(realRef.Definition.FullName))
                    .Append("<");
                foreach (var tmplArg in realRef.ModelArguments)
                    contentBuilder.Append(ToString(tmplArg));
                contentBuilder.Append(">");
                return contentBuilder.ToString();
            }
            default: throw new NotImplementedException();
        }
    }
}
