namespace Kodoshi.CodeGenerator.Entities;

public sealed class TagFieldDefinition
{
    public ModelReference? AdditionalDataType { get; }
    public string Name { get; }
    public int Value { get; }

    public TagFieldDefinition(
        ModelReference? additionalDataType,
        string name,
        int value)
    {
        AdditionalDataType = additionalDataType;
        Name = name;
        Value = value;
    }
}
