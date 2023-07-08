namespace Kodoshi.CodeGenerator.Entities;

public sealed class ServiceDefinition
{
    public string Name { get; }
    public ModelReference Input { get; }
    public ModelReference Output { get; }
    public int Id { get; }

    public ServiceDefinition(
        string name,
        ModelReference input,
        ModelReference output,
        int id)
    {
        Name = name;
        Input = input;
        Output = output;
        Id = id;
    }
}
