using System.Collections.Generic;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator.CLI;

internal sealed class InputContext : IInputContext
{
    public IFile ProjectFile { get; }

    public IReadOnlyDictionary<string, string> Settings { get; }

    public InputContext(IFile projectFile, IReadOnlyDictionary<string, string> settings)
    {
        ProjectFile = projectFile;
        Settings = settings;
    }
}
