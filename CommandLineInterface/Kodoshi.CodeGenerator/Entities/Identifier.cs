using System;

namespace Kodoshi.CodeGenerator.Entities;

public readonly struct Identifier : IEquatable<Identifier>
{
    public string Name { get; }
    public string Namespace { get; }

    public Identifier(string? name, string? @namespace)
    {
        Name = name ?? "";
        Namespace = @namespace ?? "";
    }

    public bool Equals(Identifier other)
        => other.Name == Name && other.Namespace == Namespace;

    public override bool Equals(object obj)
        => obj is Identifier id && Equals(id);

    public override int GetHashCode()
        => HashCode.Combine(Name, Namespace);
}
