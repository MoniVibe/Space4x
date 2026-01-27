using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public static class SurfaceFieldsQuantization
    {
        public static byte QuantizeU8(float value01)
        {
            var clamped = math.saturate(value01);
            return (byte)math.clamp((int)math.round(clamped * 255f), 0, 255);
        }

        public static ushort QuantizeU16(float value01)
        {
            var clamped = math.saturate(value01);
            return (ushort)math.clamp((int)math.round(clamped * 65535f), 0, 65535);
        }

        public static float DequantizeU8(byte q)
        {
            return q * (1f / 255f);
        }

        public static float DequantizeU16(ushort q)
        {
            return q * (1f / 65535f);
        }
    }
}

