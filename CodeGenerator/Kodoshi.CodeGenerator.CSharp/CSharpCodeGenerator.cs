using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.CSharp.Client;
using Kodoshi.CodeGenerator.CSharp.Models;
using Kodoshi.CodeGenerator.CSharp.Server;
using Kodoshi.CodeGenerator.Entities;
using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator.CSharp;

internal sealed class CSharpCodeGenerator : ICodeGenerator
{
    private static readonly string _assemblyName;
    private static readonly string _assemblyVersion;

    static CSharpCodeGenerator()
    {
        var asmName = typeof(CSharpCodeGenerator).Assembly.GetName();
        CSharpCodeGenerator._assemblyName = asmName.Name;
        CSharpCodeGenerator._assemblyVersion = asmName.Version.ToString();
    }

    public string Name => _assemblyName;

    public string Version => _assemblyVersion;

    public async ValueTask GenerateFromContext(
        ProjectContext inputContext,
        CancellationToken ct)
    {
        if (inputContext.Project.Models.Count == 0)
        {
            return;
        }

        foreach (var model in inputContext.Project.Models)
        {
            if (model.FullName.Namespace == "System" || model.FullName.Namespace.StartsWith("System."))
            {
                throw new ArgumentException($"[{_assemblyName}] does not allow models with namespaces starting with System.");
            }
        }
        await Task.Yield();

        var context = BuildGenerationContext(inputContext, ct);
        var helpers = new Helpers(context);
        var modelsGenerator = new ModelsGenerator(inputContext, context, helpers);
        var clientGenerator = new ClientGenerator(inputContext, context, helpers);
        var serverGenerator = new ServerGenerator(inputContext, context, helpers);

        var tasks = new Task[]
        {
            modelsGenerator.Generate(ct),
            clientGenerator.Generate(ct),
            serverGenerator.Generate(ct),
        };
        await Task.WhenAll(tasks);
    }

    private static async Task<IFolder> GetFolder(ProjectContext inputContext, string folderName, CancellationToken ct)
    {
        var root = inputContext.OutputFolder;
        if (await root.Exists(folderName, ct))
        {
            await root.Delete(folderName, ct);
        }
        return await root.CreateFolder(folderName, ct);
    }

    private static GenerationContext BuildGenerationContext(ProjectContext inputContext, CancellationToken ct)
    {
        string nmspc;
        if (!inputContext.AdditionalSettings.TryGetValue("GlobalNamespace", out nmspc))
        {
            nmspc = "KodoshiGenerated";
        }
        if (nmspc == "System" || nmspc.StartsWith("System."))
        {
            throw new ArgumentException($"Project name starting with System is not allowed.");
        }
        var coreNmspc = nmspc + ".Core";
        var modelsNmspc = nmspc + ".Models";
        var clientNmspc = nmspc + ".Client";
        var serverNmspc = nmspc + ".Server";
        var modelsFolder = new AsyncLazy<IFolder>(() => GetFolder(inputContext, modelsNmspc, ct));
        var clientFolder = new AsyncLazy<IFolder>(() => GetFolder(inputContext, clientNmspc, ct));
        var serverFolder = new AsyncLazy<IFolder>(() => GetFolder(inputContext, serverNmspc, ct));

        var unknownTagField = new TagFieldDefinition(null, "UNKNOWN", 0);
        var requestsTagDefinitions = new List<TagFieldDefinition>(inputContext.Project.Services.Count + 1)
        {
            unknownTagField,
        };

        var responseTagDefinitions = new List<TagFieldDefinition>(inputContext.Project.Services.Count + 1)
        {
            unknownTagField,
        };

        foreach (var service in inputContext.Project.Services)
        {
            requestsTagDefinitions.Add(new TagFieldDefinition(service.Input, service.Name, service.Id));
            responseTagDefinitions.Add(new TagFieldDefinition(service.Output, service.Name, service.Id));
        }

        var requestTag = new TagDefinition(
            new Identifier("Request", "_Services"),
            requestsTagDefinitions
        );

        var responseTag = new TagDefinition(
            new Identifier("Response", "_Services"),
            responseTagDefinitions
        );

        return new GenerationContext(
            _assemblyName,
            _assemblyVersion,
            nmspc,
            modelsNmspc,
            clientNmspc,
            serverNmspc,
            coreNmspc,
            modelsFolder,
            clientFolder,
            serverFolder,
            requestTag,
            responseTag);
    }
}
