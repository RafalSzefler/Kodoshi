using Microsoft.Extensions.DependencyInjection;

namespace Kodoshi.CodeGenerator.CSharp;

public sealed class Startup : IStartup
{
    public void Register(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<ICodeGenerator, CSharpCodeGenerator>();
    }
}
