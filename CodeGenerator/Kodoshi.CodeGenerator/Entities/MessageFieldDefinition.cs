namespace Kodoshi.CodeGenerator.Entities;

public sealed class MessageFieldDefinition
{
    public ModelReference Type { get; }
    public string Name { get; }
    public int Id { get; }

    public MessageFieldDefinition(ModelReference type, string name, int id)
    {
        Type = type;
        Name = name;
        Id = id;
    }
}
