using System.Collections.Generic;

namespace Kodoshi.CodeGenerator.Entities;

public sealed class Project
{
    public string Name { get; }
    public string Version { get; }
    public IReadOnlyList<ModelDefinition> Models { get; }
    public IReadOnlyList<ServiceDefinition> Services { get; }

    public Project(
        string name,
        string version,
        IReadOnlyList<ModelDefinition> models,
        IReadOnlyList<ServiceDefinition> services)
    {
        Name = name;
        Version = version;
        Models = models;
        Services = services;
    }
}
