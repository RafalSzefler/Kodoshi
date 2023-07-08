using System;
using System.Collections.Generic;
using Kodoshi.CodeGenerator.Entities;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator.CSharp;

internal sealed class GenerationContext
{
    public string Name { get; }
    public string Version { get; }
    public string GlobalNamespace { get; }
    public string ModelsNamespace { get; }
    public string ClientNamespace { get; }
    public string ServerNamespace { get; }
    public string CoreNamespace { get; }
    public AsyncLazy<IFolder> ModelsFolder { get; }
    public AsyncLazy<IFolder> ClientFolder { get; }
    public AsyncLazy<IFolder> ServerFolder { get; }
    public TagDefinition? RequestsTag { get; }
    public TagDefinition? ResponseTag { get; }
    public IReadOnlyList<ModelDefinition> ServicesTags { get; }

    public GenerationContext(
        string name,
        string version,
        string globalNamespace,
        string modelsNamespace,
        string clientNamespace,
        string serverNamespace,
        string coreNamespace,
        AsyncLazy<IFolder> modelsFolder,
        AsyncLazy<IFolder> clientFolder,
        AsyncLazy<IFolder> serverFolder,
        TagDefinition? requestTag,
        TagDefinition? responseTag)
    {
        Name = name;
        Version = version;
        GlobalNamespace = globalNamespace;
        ModelsNamespace = modelsNamespace;
        ClientNamespace = clientNamespace;
        ServerNamespace = serverNamespace;
        CoreNamespace = coreNamespace;
        ModelsFolder = modelsFolder;
        ClientFolder = clientFolder;
        ServerFolder = serverFolder;
        RequestsTag = requestTag;
        ResponseTag = responseTag;
        if (requestTag is null && responseTag is null)
        {
            ServicesTags = Array.Empty<ModelDefinition>();
        }
        else
        {
            var services = new List<ModelDefinition>();
            if (requestTag is not null) services.Add(requestTag);
            if (responseTag is not null) services.Add(responseTag);
            ServicesTags = services;
        }
    }
}
