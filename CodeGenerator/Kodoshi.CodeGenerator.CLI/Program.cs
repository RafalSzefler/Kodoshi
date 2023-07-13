using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kodoshi.CodeGenerator.Core.FileSystem;
using Kodoshi.CodeGenerator.InputLoader;
using Microsoft.Extensions.DependencyInjection;

namespace Kodoshi.CodeGenerator.CLI;

public static class Program
{
    public static async Task Main(string[] args)
    {
        await Go(args, default);
    }

    private static async Task Go(string[] args, CancellationToken ct)
    {
        var config = ConfigurationReader.Read(args);
        if (config == null)
        {
            return;
        }
        var startup = config.StartupBuilder();
        var inputContext = await ReadInputContext(config, ct);
        var projectLoader = ProjectLoaderBuilder.Build();
        var projectContext = await projectLoader.Parse(inputContext, ct);

        var serviceCollection = new ServiceCollection();
        startup.Register(serviceCollection);
        await using (var services = serviceCollection.BuildServiceProvider())
        await using (var scope = services.CreateAsyncScope())
        {
            var codeGenerator = scope.ServiceProvider.GetRequiredService<ICodeGenerator>();
            await codeGenerator.GenerateFromContext(projectContext, default);
        }
    }

    private static async Task<InputContext> ReadInputContext(Configuration config, CancellationToken ct)
    {
        var projectFile = await FileImpl.OpenFile(config.ProjectFilePath, ct);
        var settings = new Dictionary<string, string>()
        {
            {"CodeGenerator", config.CodeGeneratorName},
        };
        return new InputContext(projectFile, settings);
    }

    private static Task Generate(Configuration config)
    {
        return Task.CompletedTask;
    }
}
