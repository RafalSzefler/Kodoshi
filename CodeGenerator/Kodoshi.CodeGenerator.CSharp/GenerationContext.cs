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
    public bool CreateClient { get; }
    public bool CreateServer  { get; }
    public AsyncLazy<IFolder> ModelsFolder { get; }
    public AsyncLazy<IFolder> ClientFolder { get; }
    public AsyncLazy<IFolder> ServerFolder { get; }
    public TagDefinition? RequestsTag { get; }
    public IReadOnlyList<ModelDefinition> ServicesTags { get; }

    public GenerationContext(
        string name,
        string version,
        string globalNamespace,
        string modelsNamespace,
        string clientNamespace,
        string serverNamespace,
        string coreNamespace,
        bool createClient,
        bool createServer,
        AsyncLazy<IFolder> modelsFolder,
        AsyncLazy<IFolder> clientFolder,
        AsyncLazy<IFolder> serverFolder,
        TagDefinition? requestTag)
    {
        Name = name;
        Version = version;
        GlobalNamespace = globalNamespace;
        ModelsNamespace = modelsNamespace;
        ClientNamespace = clientNamespace;
        ServerNamespace = serverNamespace;
        CoreNamespace = coreNamespace;
        CreateClient = createClient;
        CreateServer = createServer;
        ModelsFolder = modelsFolder;
        ClientFolder = clientFolder;
        ServerFolder = serverFolder;
        RequestsTag = requestTag;
        if (requestTag is null)
        {
            ServicesTags = Array.Empty<ModelDefinition>();
        }
        else
        {
            ServicesTags = new ModelDefinition[] { requestTag };
        }
    }
}
