using System.Collections.Generic;

namespace Kodoshi.CodeGenerator.InputLoader;

internal class ProjectSettings
{
    public string? ProjectName { get; set; }
    public string? Version { get; set; }
    public string? OutputFolder { get; set; }
    public Dictionary<string, Dictionary<string, string>>? CompilerSettings { get; set; }
}
