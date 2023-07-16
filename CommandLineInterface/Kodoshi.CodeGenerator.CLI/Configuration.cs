using System;

namespace Kodoshi.CodeGenerator.CLI;

internal sealed class Configuration
{
    public string ProjectFilePath { get; }
    public string CodeGeneratorName { get; }
    public Func<IStartup> StartupBuilder { get; }

    public Configuration(
        string projectFilePath,
        string codeGeneratorName,
        Func<IStartup> startupBuilder)
    {
        ProjectFilePath = projectFilePath;
        CodeGeneratorName = codeGeneratorName;
        StartupBuilder = startupBuilder;
    }
}
