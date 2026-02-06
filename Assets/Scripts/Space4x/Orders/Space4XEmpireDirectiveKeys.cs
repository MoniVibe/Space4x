using Unity.Collections;

namespace Space4x.Orders
{
    public static class EmpireDirectiveKeys
    {
        public static readonly FixedString32Bytes SecureResources = new FixedString32Bytes("secure_resources");
        public static readonly FixedString32Bytes Expand = new FixedString32Bytes("expand");
        public static readonly FixedString32Bytes ResearchFocus = new FixedString32Bytes("research_focus");
        public static readonly FixedString32Bytes MilitaryPosture = new FixedString32Bytes("military_posture");
        public static readonly FixedString32Bytes TradeBias = new FixedString32Bytes("trade_bias");
    }
}
