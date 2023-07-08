using Microsoft.Extensions.DependencyInjection;

namespace Kodoshi.CodeGenerator;

public interface IStartup
{
    void Register(IServiceCollection serviceCollection);
}
