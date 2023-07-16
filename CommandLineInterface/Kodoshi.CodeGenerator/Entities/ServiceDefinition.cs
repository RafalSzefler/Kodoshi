namespace Kodoshi.CodeGenerator.Entities;

public sealed class ServiceDefinition
{
    public Identifier FullName { get; }
    public ModelReference Input { get; }
    public ModelReference Output { get; }
    public int Id { get; }

    public ServiceDefinition(
        Identifier fullName,
        ModelReference input,
        ModelReference output,
        int id)
    {
        FullName = fullName;
        Input = input;
        Output = output;
        Id = id;
    }
}
