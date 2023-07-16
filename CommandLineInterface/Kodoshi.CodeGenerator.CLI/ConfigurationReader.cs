using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;

namespace Kodoshi.CodeGenerator.CLI;

internal static class ConfigurationReader
{
    private sealed class CommandLineOptions
    {
        [Option('p', "project", Required = false, HelpText = "Path to project file. [Default = path to project.yaml in current working directory]")]
        public string? ProjectPath { get; set; }

        [Option('c', "code-generator", Required = true, HelpText = "Name of code generator to use.")]
        public string? CodeGeneratorName { get; set; }
    }

    public static Configuration? Read(string[] args)
    {
        Configuration? config = null;

        var result = Parser.Default.ParseArguments<CommandLineOptions>(args)
            .WithParsed<CommandLineOptions>(o =>
            {
                var codeGeneratorName = o.CodeGeneratorName;
                if (string.IsNullOrWhiteSpace(codeGeneratorName))
                {
                    throw new ArgumentNullException(nameof(codeGeneratorName));
                }

                var resolutionPaths = new List<string>(4);
                var currentDirectory = Directory.GetCurrentDirectory();
                resolutionPaths.Add(currentDirectory);
                string projectFilePath;
                if (string.IsNullOrEmpty(o.ProjectPath))
                {
                    projectFilePath = Path.Join(currentDirectory, "project.yaml");
                }
                else
                {
                    projectFilePath = Path.GetFullPath(o.ProjectPath);
                    var dir = Path.GetDirectoryName(projectFilePath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        resolutionPaths.Add(dir);
                    }
                }

                if (!string.IsNullOrEmpty(currentDirectory))
                {
                    resolutionPaths.Add(currentDirectory);
                }
                var currentAssemblyDir = GetCurrentAssemblyDirectory();
                if (!string.IsNullOrEmpty(currentAssemblyDir))
                {
                    resolutionPaths.Add(Path.Join(currentAssemblyDir, "CodeGenerators", codeGeneratorName));
                }

                if (resolutionPaths.Count == 0)
                {
                    throw new ArgumentException($"{nameof(resolutionPaths)} empty");
                }

                var startupBuilder = CreateStartupBuilder(resolutionPaths, codeGeneratorName);
                config = new Configuration(projectFilePath, codeGeneratorName, startupBuilder);
            });
        
        return config;
    }

    private static Func<IStartup> CreateStartupBuilder(IReadOnlyList<string> resolutionDirectories, string name)
    {
        if (!name.EndsWith(".dll"))
        {
            name = name + ".dll";
        }

        var currentWorkingDirectory = Directory.GetCurrentDirectory();

        foreach (var directory in resolutionDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            Assembly asm;
            Directory.SetCurrentDirectory(directory);
            try
            {
                if (!Path.Exists(name))
                {
                    continue;
                }

                asm = Assembly.LoadFrom(Path.Join(directory, name));
            }
            finally
            {
                Directory.SetCurrentDirectory(currentWorkingDirectory);
            }

            var startupTypes = asm.GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IStartup)))
                .ToList();
            
            if (startupTypes.Count == 0)
            {
                throw new InvalidDataException($"Assembly [{name}] does not implement {nameof(IStartup)} interface.");
            }

            if (startupTypes.Count > 1)
            {
                throw new InvalidDataException($"Assembly [{name}] implements multiple {nameof(IStartup)} interfaces. Expected one.");
            }
            var type = startupTypes[0];

            var ctr = type.GetConstructor(Array.Empty<Type>());
            if (ctr == null)
            {
                throw new InvalidDataException($"Assembly [{name}] implements {nameof(IStartup)} interface. However it doesn't have default constructor, which is expected.");
            }

            return () => {
                var instance = Activator.CreateInstance(type);
                if (instance == null)
                {
                    throw new ArgumentNullException("Activator.CreateInstance fail");
                }
                return (IStartup)instance;
            };
        }

        throw new DllNotFoundException(name);
    }

    private static string? GetCurrentAssemblyDirectory()
    {
        var asm = typeof(ConfigurationReader).Assembly;
        var path =  Path.GetFullPath(asm.Location);
        return Path.GetDirectoryName(path);
    }
}
