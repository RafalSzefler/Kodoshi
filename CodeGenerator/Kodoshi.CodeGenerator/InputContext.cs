using System.Collections.Generic;
using Kodoshi.CodeGenerator.Entities;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator;

public sealed class InputContext
{
    public Project Project { get; }
    public IFolder OutputFolder { get; }
    public IReadOnlyDictionary<string, string> AdditionalSettings { get; }

    public InputContext(
        Project project,
        IFolder outputFolder,
        IReadOnlyDictionary<string, string> additionalSettings)
    {
        Project = project;
        OutputFolder = outputFolder;
        AdditionalSettings = additionalSettings;
    }
}
