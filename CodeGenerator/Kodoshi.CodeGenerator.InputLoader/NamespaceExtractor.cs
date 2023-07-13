using Kodoshi.CodeGenerator.FileSystem;
using Kodoshi.CodeGenerator.InputLoader.AST;

namespace Kodoshi.CodeGenerator.InputLoader;

internal static class NamespaceExtractor
{
    public static ASTNamespaceStatement? ExtractTopNamespace(IFile file, ASTBlock block)
    {
        if (block.Statements.Count == 0)
        {
            return null;
        }
        var firstStmt = block.Statements[0];

        ASTNamespaceStatement? topNamespace = null;
        foreach (var stmt in block.Statements)
        {
            if (!(stmt is ASTNamespaceStatement nmspc)) continue;
            if (nmspc.AttachedBlock is null)
            {
                if (topNamespace is not null)
                {
                    throw new ParsingException($"File {file.FullName} contains multiple global namespaces.");
                }
                topNamespace = nmspc;
            }
            else
            {
                ValidateNamespace(file, nmspc.AttachedBlock);
            }
        }

        if (!object.ReferenceEquals(firstStmt, topNamespace))
        {
            throw new ParsingException($"Top namespace has to be the first statement in file {file.FullName}.");
        }
        return topNamespace;
    }

    private static void ValidateNamespace(IFile file, ASTBlock block)
    {
        foreach (var stmt in block.Statements)
        {
            if (!(stmt is ASTNamespaceStatement nmspc)) continue;
            if (nmspc.AttachedBlock is null)
            {
                throw new ParsingException($"File {file.FullName} contains nested global namespace. That is not allowed.");
            }
            else
            {
                ValidateNamespace(file, nmspc.AttachedBlock);
            }
        }
    }
}
