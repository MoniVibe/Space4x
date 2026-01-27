using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace PureDOTS.Runtime.LowLevel
{
    /// <summary>
    /// Burst-safe null-ref helpers. Use ONLY internally.
    /// Do not expose "null ref" semantics in public APIs.
    /// </summary>
    public static unsafe class UnsafeRef
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNull<T>(ref T value) where T : unmanaged
            => UnsafeUtility.AddressOf(ref value) == null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Null<T>() where T : unmanaged
            => ref UnsafeUtility.AsRef<T>(null);
    }
}
