using System.Collections.Generic;

namespace Kodoshi.CodeGenerator.Entities;

public sealed class Project
{
    public string Name { get; }
    public string Version { get; }
    public string Build { get; }
    public IReadOnlyList<ModelDefinition> Models { get; }
    public IReadOnlyList<ServiceDefinition> Services { get; }
    public IReadOnlyList<ModelReference> MaterializedModels { get; }

    public Project(
        string name,
        string version,
        string build,
        IReadOnlyList<ModelDefinition> models,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<ModelReference> materializedModels)
    {
        Name = name;
        Version = version;
        Build = build;
        Models = models;
        Services = services;
        MaterializedModels = materializedModels;
    }
}
