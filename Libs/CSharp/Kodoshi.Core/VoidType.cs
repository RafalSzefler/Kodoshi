using System;

namespace Kodoshi.Core
{
    public readonly struct VoidType : IEquatable<VoidType>
    {
        public static VoidType Instance { get; } = new VoidType();

        public bool Equals(VoidType other) => true;

        public override bool Equals(object obj) => obj is VoidType;

        public override int GetHashCode() => 0;
    }
}
