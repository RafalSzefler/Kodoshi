using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.Entities;
using Kodoshi.CodeGenerator.FileSystem;
using Kodoshi.CodeGenerator.InputLoader.AST;
using sly.parser.generator;
using YamlDotNet.Serialization;

namespace Kodoshi.CodeGenerator.InputLoader;

internal sealed class ProjectLoaderImpl : IProjectLoader
{
    private const string OutputFolderKey = "OutputFolder";
    private const string CodeGeneratorKey = "CodeGenerator";

    private readonly Dictionary<string, string> _empty = new Dictionary<string, string>();
    private readonly Dictionary<string, Dictionary<string, string>> _emptyNested = new Dictionary<string, Dictionary<string, string>>();
    public async ValueTask<ProjectContext> Parse(IInputContext context, CancellationToken ct)
    {
        var tmpSettings = new Dictionary<string, string>(context.Settings);
        var projectFile = context.ProjectFile;
        tmpSettings.Remove(CodeGeneratorKey, out var codeGenerator);
        var content = await projectFile.Read(ct);
        var text = Encoding.UTF8.GetString(content.Span);
        var deserializer = new DeserializerBuilder().Build();
        var settings = deserializer.Deserialize<ProjectSettings>(text);
        ValidateSettings(settings);
        var globalSettings = settings.GlobalSettings ?? _empty;
        var compilerSettings = settings.CompilerSettings ?? _emptyNested;
        var codeGeneratorSettings = compilerSettings[codeGenerator];
        var inputFolder = projectFile.ParentFolder;

        if (!codeGeneratorSettings.TryGetValue(OutputFolderKey, out var outputFolderPath))
        {
            if (!globalSettings.TryGetValue(OutputFolderKey, out outputFolderPath))
            {
                outputFolderPath = "out";
            }
        }

        IFolder outputFolder;
        if (!(await inputFolder.Exists(outputFolderPath, ct)))
        {
            outputFolder = await inputFolder.CreateFolder(outputFolderPath, ct);
        }
        else
        {
            outputFolder = await inputFolder.OpenFolder(outputFolderPath, ct);
        }

        var additionalSettings = new Dictionary<string, string>();
        additionalSettings[nameof(settings.ProjectName)] = settings.ProjectName!;
        additionalSettings[nameof(settings.Version)] = settings.Version!;
        UpdateAdditionalSettings(codeGenerator, compilerSettings, additionalSettings);
        foreach (var kvp in tmpSettings)
        {
            additionalSettings[kvp.Key] = kvp.Value;
        }

        var project = await ReadProject(inputFolder, settings, ct);

        return new ProjectContext(project, outputFolder, additionalSettings);
    }

    private void UpdateAdditionalSettings(string codeGeneratorName, Dictionary<string, Dictionary<string, string>> additionalProjectSettings, Dictionary<string, string> inputAdditionalSettings)
    {
        if (!additionalProjectSettings.TryGetValue(codeGeneratorName, out var compilerSettings))
            return;

        if (compilerSettings is null)
            return;

        foreach (var kvp in compilerSettings)
        {
            inputAdditionalSettings[kvp.Key] = kvp.Value;
        }
    }

    private static void ValidateSettings(ProjectSettings? settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }
        if (string.IsNullOrEmpty(settings.ProjectName))
        {
            throw new ArgumentException($"{nameof(settings.ProjectName)} cannot be empty.");
        }
        if (string.IsNullOrEmpty(settings.Version))
        {
            throw new ArgumentException($"{nameof(settings.Version)} cannot be empty.");
        }
    }

    private async ValueTask<Project> ReadProject(IFolder inputFolder, ProjectSettings settings, CancellationToken ct)
    {
        var allFiles = new List<IFile>();
        await ReadAllFiles(allFiles, inputFolder, ct);
        return await ParseFiles(allFiles, settings, ct);
    }

    private async ValueTask ReadAllFiles(List<IFile> contents, IFolder inputFolder, CancellationToken ct)
    {
        var files = await inputFolder.ListFiles(ct);
        contents.AddRange(files.Where(f => Path.GetExtension(f.Name) == ".ks"));
        var folders = await inputFolder.ListFolders(ct);
        foreach (var folder in folders)
        {
            await ReadAllFiles(contents, folder, ct);
        }
    }

    private async Task<Project> ParseFiles(List<IFile> allFiles, ProjectSettings settings, CancellationToken ct)
    {
        var parserInstance = new ExpressionParser();
        var builder = new ParserBuilder<ExpressionToken, AST.ASTNode>();
        var parserResult = builder.BuildParser(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");
        if (parserResult.IsError)
        {
            var message = string.Join("; ", parserResult.Errors.Select(x => x.Message));
            throw new MiscException(message);
        }

        var parser = parserResult.Result;

        var tasks = new Task<ASTBlock>[allFiles.Count];
        for (var i = 0; i < allFiles.Count; i++)
        {
            tasks[i] = ParseInputFile(parser, allFiles[i], settings, ct);
        }
        
        var astNodes = new (IFile, AST.ASTBlock)[allFiles.Count];
        for (var i = 0; i < allFiles.Count; i++)
        {
            var block = await tasks[i];
            astNodes[i] = (allFiles[i], block);
        }

        var converter = new ASTToProjectConverter(astNodes, settings);
        return converter.Convert();
    }

    private async Task<ASTBlock> ParseInputFile(sly.parser.Parser<ExpressionToken, AST.ASTNode> parser, IFile file, ProjectSettings settings, CancellationToken ct)
    {
        var content = await file.Read(ct);
        var textContent = Encoding.UTF8.GetString(content.Span);
        var result = parser.Parse(textContent);
        if (result.IsOk)
        {
            if (result.Result is ASTBlock block) return block;
            throw new ParsingException($"Expected block, received {result.Result.GetType()} instead.");
        }

        var errorMessage = new StringBuilder();
        errorMessage.Append("Parsing errors:\n");
        foreach (var error in result.Errors)
        {
            errorMessage.Append("- ").Append(error.ErrorMessage).Append("\n");
        }
        throw new ParsingException(errorMessage.ToString());
    }
}
