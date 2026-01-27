namespace PureDOTS.Runtime.WorldGen
{
    public static class SurfaceFieldsHash
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037ul;
        private const ulong FnvPrime64 = 1099511628211ul;

        public static ulong Begin()
        {
            return FnvOffsetBasis64;
        }

        public static ulong HashByte(ulong hash, byte value)
        {
            return (hash ^ value) * FnvPrime64;
        }

        public static ulong HashU16(ulong hash, ushort value)
        {
            hash = HashByte(hash, (byte)(value & 0xFF));
            hash = HashByte(hash, (byte)((value >> 8) & 0xFF));
            return hash;
        }
    }
}

