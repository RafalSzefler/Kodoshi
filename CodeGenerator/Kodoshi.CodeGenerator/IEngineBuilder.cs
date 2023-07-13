using Kodoshi.CodeGenerator.FileSystem;

namespace Kodoshi.CodeGenerator;

public interface IEngineBuilder
{
    IEngineBuilder SetCodeGenerator(ICodeGenerator generator);
    IEngineBuilder SetInputFolder(IFolder inputFolder);
    IEngineBuilder SetInputLoader(IProjectLoader inputLoader);
    IEngine Build();
}
