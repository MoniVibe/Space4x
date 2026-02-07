using Unity.Collections;

namespace Space4x.Orders
{
    public static class EmpireDirectiveKeys
    {
        public static readonly FixedString32Bytes SecureResources = BuildSecureResources();
        public static readonly FixedString32Bytes Expand = BuildExpand();
        public static readonly FixedString32Bytes ResearchFocus = BuildResearchFocus();
        public static readonly FixedString32Bytes MilitaryPosture = BuildMilitaryPosture();
        public static readonly FixedString32Bytes TradeBias = BuildTradeBias();

        private static FixedString32Bytes BuildSecureResources()
        {
            FixedString32Bytes value = default;
            value.Append('s');
            value.Append('e');
            value.Append('c');
            value.Append('u');
            value.Append('r');
            value.Append('e');
            value.Append('_');
            value.Append('r');
            value.Append('e');
            value.Append('s');
            value.Append('o');
            value.Append('u');
            value.Append('r');
            value.Append('c');
            value.Append('e');
            value.Append('s');
            return value;
        }

        private static FixedString32Bytes BuildExpand()
        {
            FixedString32Bytes value = default;
            value.Append('e');
            value.Append('x');
            value.Append('p');
            value.Append('a');
            value.Append('n');
            value.Append('d');
            return value;
        }

        private static FixedString32Bytes BuildResearchFocus()
        {
            FixedString32Bytes value = default;
            value.Append('r');
            value.Append('e');
            value.Append('s');
            value.Append('e');
            value.Append('a');
            value.Append('r');
            value.Append('c');
            value.Append('h');
            value.Append('_');
            value.Append('f');
            value.Append('o');
            value.Append('c');
            value.Append('u');
            value.Append('s');
            return value;
        }

        private static FixedString32Bytes BuildMilitaryPosture()
        {
            FixedString32Bytes value = default;
            value.Append('m');
            value.Append('i');
            value.Append('l');
            value.Append('i');
            value.Append('t');
            value.Append('a');
            value.Append('r');
            value.Append('y');
            value.Append('_');
            value.Append('p');
            value.Append('o');
            value.Append('s');
            value.Append('t');
            value.Append('u');
            value.Append('r');
            value.Append('e');
            return value;
        }

        private static FixedString32Bytes BuildTradeBias()
        {
            FixedString32Bytes value = default;
            value.Append('t');
            value.Append('r');
            value.Append('a');
            value.Append('d');
            value.Append('e');
            value.Append('_');
            value.Append('b');
            value.Append('i');
            value.Append('a');
            value.Append('s');
            return value;
        }
    }
}
