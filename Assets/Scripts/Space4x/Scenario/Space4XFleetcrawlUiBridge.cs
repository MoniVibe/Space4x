using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Scenario
{
    public struct Space4XRunPendingGatePick : IComponentData
    {
        public int RoomIndex;
        public int GateOrdinal;
    }

    public struct Space4XRunPendingBoonPick : IComponentData
    {
        public int RoomIndex;
        public int OfferIndex;
    }

    internal static class Space4XFleetcrawlUiBridge
    {
        public static bool IsFleetcrawlScenario(in FixedString64Bytes scenarioId)
        {
            return scenarioId.Length > 0 && scenarioId.ToString().StartsWith("space4x_fleetcrawl", System.StringComparison.OrdinalIgnoreCase);
        }

        public static int ResolveGateCount(Space4XFleetcrawlRoomKind roomKind)
        {
            return roomKind == Space4XFleetcrawlRoomKind.Relief ? 2 : 3;
        }

        public static Space4XRunGateKind ResolveGateKind(Space4XFleetcrawlRoomKind roomKind, int gateOrdinal)
        {
            if (roomKind == Space4XFleetcrawlRoomKind.Relief)
            {
                return gateOrdinal == 0 ? Space4XRunGateKind.Boon : Space4XRunGateKind.Blueprint;
            }

            return gateOrdinal switch
            {
                0 => Space4XRunGateKind.Boon,
                1 => Space4XRunGateKind.Blueprint,
                _ => Space4XRunGateKind.Relief
            };
        }

        public static int ResolveAutoGateOrdinal(uint seed, int roomIndex, int gateCount)
        {
            if (gateCount <= 1)
            {
                return 0;
            }

            var hash = DeterministicMix(seed, (uint)(roomIndex + 1), 0x4F1BBCDDu, 0x7F4A7C15u);
            return (int)(hash % (uint)gateCount);
        }

        public static int ResolveAutoOfferIndex(uint seed, int roomIndex, Space4XRunGateKind gateKind, int offerCount)
        {
            if (offerCount <= 1)
            {
                return 0;
            }

            var hash = DeterministicMix(seed, (uint)(roomIndex + 1), (uint)gateKind + 101u, 0xC3A5C85Cu);
            return (int)(hash % (uint)offerCount);
        }

        public static FixedString64Bytes ResolveBoonOfferIdAt(uint seed, int roomIndex, int offerIndex)
        {
            var start = (int)(DeterministicMix(seed, (uint)(roomIndex + 1), (uint)Space4XRunGateKind.Boon + 17u, 0xA5A5A5A5u) % 4u);
            var index = (start + math.clamp(offerIndex, 0, 2)) % 4;
            return ResolveBoonId(index);
        }

        private static FixedString64Bytes ResolveBoonId(int index)
        {
            return index switch
            {
                0 => new FixedString64Bytes("perk_convert_kinetic_to_beam_100"),
                1 => new FixedString64Bytes("perk_drones_use_beam"),
                2 => new FixedString64Bytes("perk_beam_chain_small"),
                _ => new FixedString64Bytes("perk_beam_damage_mult_small")
            };
        }

        private static uint DeterministicMix(uint a, uint b, uint c, uint d)
        {
            var hash = 2166136261u;
            hash ^= a;
            hash *= 16777619u;
            hash ^= b;
            hash *= 16777619u;
            hash ^= c;
            hash *= 16777619u;
            hash ^= d;
            hash *= 16777619u;
            return hash;
        }
    }
}
