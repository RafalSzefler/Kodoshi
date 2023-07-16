using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

    private static readonly HashSet<string> _trueValues
        = new HashSet<string> { "1", "true", "t", "yes", "y" };
    private static readonly HashSet<string> _falseValues
        = new HashSet<string> { "0", "false", "f", "no", "n" };

    private static bool ParseBool(string text)
    {
        text = text.Trim().ToLower();
        if (_trueValues.Contains(text))
            return true;
        if (_falseValues.Contains(text))
            return false;
        throw new ArgumentException($"Invalid value [{text}]. Expected boolean.");
    }

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
        var buildInfoGenerator = new BuildInfoGenerator(inputContext, context, helpers);

        var tasks = new Task[]
        {
            modelsGenerator.Generate(ct),
            clientGenerator.Generate(ct),
            serverGenerator.Generate(ct),
            buildInfoGenerator.Generate(ct),
        };
        foreach (var task in tasks)
        {
            await task;
        }
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
        const string GlobalNamespaceKey = "GlobalNamespace";
        string nmspc;
        if (!inputContext.AdditionalSettings.TryGetValue(GlobalNamespaceKey, out nmspc))
        {
            if (string.IsNullOrWhiteSpace(nmspc))
                nmspc = "KodoshiGenerated";
        }

        const string regexPattern = "[a-zA-Z][a-zA-Z0-9_]*";
        var nmspcRegex = new Regex(regexPattern);
        foreach (var piece in nmspc.Split('.'))
        {
            if (string.IsNullOrWhiteSpace(piece))
            {
                throw new ArgumentException($"{GlobalNamespaceKey} cannot contain whitespace between dots.");
            }

            if (!nmspcRegex.IsMatch(piece))
            {
                throw new ArgumentException($"Each piece in {GlobalNamespaceKey} has to follow {regexPattern} pattern.");
            }
        }

        var _restrictedTopNamespaces = new string[]
        {
            "System", "Microsoft", "Kodoshi",
        };
        foreach (var restrictedNmspc in _restrictedTopNamespaces)
        {
            if (nmspc == restrictedNmspc || nmspc.StartsWith($"{restrictedNmspc}."))
            {
                throw new ArgumentException($"Top namespace {restrictedNmspc} is restricted and cannot be used in the project.");
            }
        }
        var coreNmspc = nmspc + ".Core";
        var modelsNmspc = nmspc + ".Models";
        var clientNmspc = nmspc + ".Client";
        var serverNmspc = nmspc + ".Server";
        var modelsFolder = new AsyncLazy<IFolder>(() => GetFolder(inputContext, modelsNmspc, ct));
        var clientFolder = new AsyncLazy<IFolder>(() => GetFolder(inputContext, clientNmspc, ct));
        var serverFolder = new AsyncLazy<IFolder>(() => GetFolder(inputContext, serverNmspc, ct));

        TagDefinition? requestTag = null;

        var createClient = false;
        var createServer = false;

        if (inputContext.Project.Services.Count > 0)
        {
            createClient = true;
            createServer = true;
            if (inputContext.AdditionalSettings.TryGetValue("WithClient", out var withClientValue))
            {
                if (!string.IsNullOrWhiteSpace(withClientValue))
                    createClient = ParseBool(withClientValue);
            }
            if (inputContext.AdditionalSettings.TryGetValue("WithServer", out var withServerValue))
            {
                if (!string.IsNullOrWhiteSpace(withServerValue))
                    createServer = ParseBool(withServerValue);
            }

            if (createClient || createServer)
            {
                var unknownTagField = new TagFieldDefinition(null, "UNKNOWN", 0);
                var requestsTagDefinitions = new List<TagFieldDefinition>(inputContext.Project.Services.Count + 1)
                {
                    unknownTagField,
                };

                foreach (var service in inputContext.Project.Services)
                {
                    var tag = ServiceHelpers.ServiceIdentifierToTag(service.FullName);
                    requestsTagDefinitions.Add(new TagFieldDefinition(service.Input, tag, service.Id));
                }

                requestTag = new TagDefinition(
                    new Identifier("Request", "_Services"),
                    requestsTagDefinitions
                );
            }
        }

        return new GenerationContext(
            _assemblyName,
            _assemblyVersion,
            nmspc,
            modelsNmspc,
            clientNmspc,
            serverNmspc,
            coreNmspc,
            createClient,
            createServer,
            modelsFolder,
            clientFolder,
            serverFolder,
            requestTag);
    }
}
