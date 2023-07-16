using Kodoshi.CodeGenerator.Entities;

namespace Kodoshi.CodeGenerator.CSharp;

internal static class ServiceHelpers
{
    public static string ServiceIdentifierToTag(Identifier id)
        => $"_{id.Namespace.Replace(".", "_")}_{id.Name}";
}