namespace Kodoshi.CodeGenerator.InputLoader;

public static class ProjectLoaderBuilder
{
    public static IProjectLoader Build() => new ProjectLoaderImpl();
}