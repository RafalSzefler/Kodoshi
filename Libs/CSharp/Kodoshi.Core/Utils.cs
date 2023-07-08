using System.Runtime.CompilerServices;

namespace Kodoshi.Core
{
    public static class Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CalculateHashCode<T>(T value) => (value is null ? 0 : (uint)value.GetHashCode());
    }
}
