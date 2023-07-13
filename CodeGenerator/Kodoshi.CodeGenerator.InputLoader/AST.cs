using System.Collections.Generic;

namespace Kodoshi.CodeGenerator.InputLoader.AST
{
    internal enum ASTKind
    {
        BLOCK = 0,
        NAMESPACE = 1,
        REFERENCE = 2,
        MESSAGE = 3,
        MESSAGE_FIELD = 4,
        TAG = 5,
        TAG_FIELD = 6,
        NAME = 7,
    }

    internal abstract class ASTNode
    {
        public ASTKind Kind { get; }

        protected ASTNode(ASTKind kind)
        {
            Kind = kind;
        }
    }

    internal sealed class ASTName : ASTNode
    {
        public string Value { get; }

        public ASTName(string value) : base(ASTKind.NAME)
        {
            Value = value;
        }
    }

    internal abstract class ASTStatement : ASTNode
    {
        protected ASTStatement(ASTKind kind) : base(kind)
        { }
    }

    internal sealed class ASTBlock : ASTNode
    {
        public IReadOnlyList<ASTStatement> Statements { get; }
        public ASTBlock(IReadOnlyList<ASTStatement> statements)
            : base(ASTKind.BLOCK)
        {
            Statements = statements;
        }
    }

    internal sealed class ASTNamespaceStatement : ASTStatement
    {
        public string Identifier { get; }
        public ASTBlock? AttachedBlock { get; }
        public ASTNamespaceStatement(
                string identifier,
                ASTBlock? attachedBlock)
            : base(ASTKind.NAMESPACE)
        {
            Identifier = identifier;
            AttachedBlock = attachedBlock;
        }
    }

    internal sealed class ASTReference : ASTNode
    {
        public ASTReference(
                string identifier,
                IReadOnlyList<ASTReference> genericArguments)
            : base(ASTKind.REFERENCE)
        {
            Identifier = identifier;
            GenericArguments = genericArguments;
        }

        public string Identifier { get;}
        public IReadOnlyList<ASTReference> GenericArguments { get; }
    }

    internal sealed class ASTMessageFieldDefinition : ASTNode
    {
        public string Name { get; }
        public ASTReference Type { get; }
        public int Id { get; }
        public ASTMessageFieldDefinition(string name, ASTReference type, int id)
            : base(ASTKind.MESSAGE_FIELD)
        {
            Name = name;
            Type = type;
            Id = id;
        }
    }

    internal sealed class ASTMessageDefinition : ASTStatement
    {
        public string Name { get; }
        public IReadOnlyList<ASTReference> GenericArguments { get; }
        public IReadOnlyList<ASTMessageFieldDefinition> Fields { get; }
        public ASTMessageDefinition(
                string name,
                IReadOnlyList<ASTReference> genericArguments,
                IReadOnlyList<ASTMessageFieldDefinition> fields)
            : base(ASTKind.MESSAGE)
        {
            Name = name;
            GenericArguments = genericArguments;
            Fields = fields;
        }
    }

    internal sealed class ASTTagFieldDefinition : ASTNode
    {
        public ASTTagFieldDefinition(
                string name,
                int value,
                ASTReference? attachedType)
            : base(ASTKind.TAG_FIELD)
        {
            Name = name;
            Value = value;
            AttachedType = attachedType;
        }

        public string Name { get; }
        public int Value { get; }
        public ASTReference? AttachedType { get; }
    }

    internal sealed class ASTTagDefinition : ASTStatement
    {
        public string Name { get; }
        public IReadOnlyList<ASTReference> GenericArguments { get; }
        public IReadOnlyList<ASTTagFieldDefinition> Fields { get; }
        public ASTTagDefinition(
                string name,
                IReadOnlyList<ASTReference> genericArguments,
                IReadOnlyList<ASTTagFieldDefinition> fields)
            : base(ASTKind.TAG)
        {
            Name = name;
            GenericArguments = genericArguments;
            Fields = fields;
        }
    }
}
