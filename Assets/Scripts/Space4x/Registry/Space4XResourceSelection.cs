using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Lightweight resource selection utilities for logistics and mission generation.
    /// Keeps critical resources weighted higher without excluding any type.
    /// </summary>
    public static class Space4XResourceSelection
    {
        public static ResourceType SelectLogisticsResource(ref Random rng)
        {
            var roll = rng.NextInt(0, 100);
            return SelectWeightedResource(roll);
        }

        public static ResourceType SelectLogisticsResource(uint hash)
        {
            var roll = (int)(hash % 100u);
            return SelectWeightedResource(roll);
        }

        public static ResourceType SelectMissionCargoResource(ref Random rng)
        {
            var roll = rng.NextInt(0, 100);
            return SelectMissionWeightedResource(roll);
        }

        public static ResourceType SelectMissionCargoResource(uint hash)
        {
            var roll = (int)(hash % 100u);
            return SelectMissionWeightedResource(roll);
        }

        private static ResourceType SelectWeightedResource(int roll)
        {
            // Weighted to keep critical resources abundant while allowing all types.
            if (roll < 10) return ResourceType.Food;
            if (roll < 19) return ResourceType.Water;
            if (roll < 28) return ResourceType.Supplies;
            if (roll < 37) return ResourceType.Fuel;
            if (roll < 46) return ResourceType.Minerals;
            if (roll < 54) return ResourceType.OrganicMatter;
            if (roll < 60) return ResourceType.Volatiles;
            if (roll < 65) return ResourceType.HeavyWater;
            if (roll < 70) return ResourceType.Isotopes;
            if (roll < 75) return ResourceType.EnergyCrystals;
            if (roll < 80) return ResourceType.Ore;
            if (roll < 84) return ResourceType.RareMetals;
            if (roll < 87) return ResourceType.IndustrialCrystals;
            if (roll < 90) return ResourceType.TransplutonicOre;
            if (roll < 93) return ResourceType.ExoticGases;
            if (roll < 95) return ResourceType.LiquidOzone;
            if (roll < 96) return ResourceType.StrontiumClathrates;
            if (roll < 97) return ResourceType.SalvageComponents;
            if (roll < 98) return ResourceType.VolatileMotes;
            if (roll < 99) return ResourceType.BoosterGas;
            return ResourceType.RelicData;
        }

        private static ResourceType SelectMissionWeightedResource(int roll)
        {
            // Mission cargo skews even more toward essentials.
            if (roll < 14) return ResourceType.Food;
            if (roll < 26) return ResourceType.Water;
            if (roll < 36) return ResourceType.Supplies;
            if (roll < 46) return ResourceType.Fuel;
            if (roll < 55) return ResourceType.Minerals;
            if (roll < 62) return ResourceType.OrganicMatter;
            if (roll < 68) return ResourceType.Volatiles;
            if (roll < 73) return ResourceType.HeavyWater;
            if (roll < 78) return ResourceType.Isotopes;
            if (roll < 82) return ResourceType.EnergyCrystals;
            if (roll < 86) return ResourceType.Ore;
            if (roll < 89) return ResourceType.RareMetals;
            if (roll < 92) return ResourceType.IndustrialCrystals;
            if (roll < 94) return ResourceType.TransplutonicOre;
            if (roll < 96) return ResourceType.ExoticGases;
            if (roll < 97) return ResourceType.LiquidOzone;
            if (roll < 98) return ResourceType.StrontiumClathrates;
            if (roll < 99) return ResourceType.SalvageComponents;
            return ResourceType.RelicData;
        }
    }
}
