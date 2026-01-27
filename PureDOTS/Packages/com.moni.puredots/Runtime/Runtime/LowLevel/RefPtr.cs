using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace PureDOTS.Runtime.LowLevel
{
    public unsafe struct RefPtr<T> where T : unmanaged
    {
        public T* Ptr;

        public bool IsNull => Ptr == null;

        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.AsRef<T>(Ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RefPtr<T> Null() => new RefPtr<T> { Ptr = null };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RefPtr<T> FromRef(ref T value)
            => new RefPtr<T> { Ptr = (T*)UnsafeUtility.AddressOf(ref value) };
    }
}
