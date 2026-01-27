using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Logistics
{
    /// <summary>
    /// Static helpers for caravan and logistics calculations.
    /// </summary>
    [BurstCompile]
    public static class CaravanHelpers
    {
        /// <summary>
        /// Default logistics configuration.
        /// </summary>
        public static LogisticsConfig DefaultConfig => new LogisticsConfig
        {
            RouteQualityThreshold1 = 5,
            RouteQualityThreshold2 = 20,
            RouteQualityThreshold3 = 50,
            BaseAmbushChance = 0.1f,
            GuardEffectiveness = 0.05f,
            InfrastructureDecayRate = 0.001f
        };

        /// <summary>
        /// Calculates route quality from trips.
        /// </summary>
        public static byte CalculateRouteQuality(uint totalTrips, in LogisticsConfig config)
        {
            if (totalTrips >= config.RouteQualityThreshold3) return 3;
            if (totalTrips >= config.RouteQualityThreshold2) return 2;
            if (totalTrips >= config.RouteQualityThreshold1) return 1;
            return 0;
        }

        /// <summary>
        /// Calculates travel time with modifiers.
        /// </summary>
        public static float CalculateTravelTime(
            float baseTime,
            float caravanSpeed,
            byte routeQuality,
            float infrastructureBonus)
        {
            float qualityMod = 1f - routeQuality * 0.1f; // Better routes = faster
            float infraMod = 1f - infrastructureBonus * 0.2f;
            
            return baseTime * qualityMod * infraMod / math.max(0.1f, caravanSpeed);
        }

        /// <summary>
        /// Calculates ambush chance.
        /// </summary>
        public static float CalculateAmbushChance(
            float baseChance,
            float securityRating,
            byte guardCount,
            float guardEffectiveness)
        {
            float chance = baseChance * (1f - securityRating);
            chance -= guardCount * guardEffectiveness;
            return math.clamp(chance, 0f, 1f);
        }

        /// <summary>
        /// Resolves an ambush.
        /// </summary>
        public static AmbushOutcome ResolveAmbush(
            float ambushStrength,
            float defenseStrength,
            uint seed)
        {
            float ratio = defenseStrength / math.max(0.1f, ambushStrength);
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            
            if (ratio > 2f)
                return AmbushOutcome.CaravanDefended;
            if (ratio > 1.2f)
                return roll < 0.8f ? AmbushOutcome.CaravanDefended : AmbushOutcome.CargoStolen;
            if (ratio > 0.8f)
                return roll < 0.5f ? AmbushOutcome.CaravanEscaped : AmbushOutcome.CargoStolen;
            if (ratio > 0.4f)
                return roll < 0.3f ? AmbushOutcome.CaravanEscaped : AmbushOutcome.CargoStolen;
            
            return roll < 0.5f ? AmbushOutcome.CargoStolen : AmbushOutcome.CaravanDestroyed;
        }

        /// <summary>
        /// Calculates cargo value.
        /// </summary>
        public static float CalculateCargoValue(in DynamicBuffer<CargoManifest> cargo)
        {
            float total = 0;
            for (int i = 0; i < cargo.Length; i++)
            {
                total += cargo[i].Quantity * cargo[i].PurchasePrice;
            }
            return total;
        }

        /// <summary>
        /// Calculates trip profit.
        /// </summary>
        public static float CalculateTripProfit(
            float cargoValue,
            float sellPrice,
            float transportCost,
            float lossRatio)
        {
            float revenue = cargoValue * sellPrice * (1f - lossRatio);
            return revenue - transportCost;
        }

        /// <summary>
        /// Updates caravan position along route.
        /// </summary>
        public static float UpdateProgress(float currentProgress, float travelTime, float deltaTime)
        {
            if (travelTime <= 0) return 1f;
            return math.saturate(currentProgress + deltaTime / travelTime);
        }

        /// <summary>
        /// Calculates position along route.
        /// </summary>
        public static float3 CalculateRoutePosition(
            float3 start,
            float3 end,
            float progress)
        {
            return math.lerp(start, end, progress);
        }

        /// <summary>
        /// Gets infrastructure bonus at position.
        /// </summary>
        public static float GetInfrastructureBonus(
            float3 position,
            in DynamicBuffer<RouteInfrastructure> infrastructure)
        {
            float totalBonus = 0;
            
            for (int i = 0; i < infrastructure.Length; i++)
            {
                var infra = infrastructure[i];
                float dist = math.distance(position, infra.Position);
                
                if (dist <= infra.EffectRadius)
                {
                    float falloff = 1f - (dist / infra.EffectRadius);
                    totalBonus += infra.BenefitModifier * infra.Condition * falloff;
                }
            }
            
            return math.saturate(totalBonus);
        }

        /// <summary>
        /// Applies infrastructure decay.
        /// </summary>
        public static void DecayInfrastructure(
            ref DynamicBuffer<RouteInfrastructure> infrastructure,
            float decayRate)
        {
            for (int i = 0; i < infrastructure.Length; i++)
            {
                var infra = infrastructure[i];
                infra.Condition = math.max(0f, infra.Condition - decayRate);
                infrastructure[i] = infra;
            }
        }

        /// <summary>
        /// Calculates defense strength.
        /// </summary>
        public static float CalculateDefenseStrength(byte guardCount, float cargoWeight)
        {
            // More cargo = harder to defend
            float weightPenalty = cargoWeight / 1000f;
            return guardCount * 10f - weightPenalty;
        }

        /// <summary>
        /// Checks if caravan can carry cargo.
        /// </summary>
        public static bool CanAddCargo(in Caravan caravan, float weight)
        {
            return caravan.CurrentCargoWeight + weight <= caravan.CargoCapacity;
        }

        /// <summary>
        /// Adds cargo to manifest.
        /// </summary>
        public static bool AddCargo(
            ref DynamicBuffer<CargoManifest> manifest,
            ref Caravan caravan,
            FixedString32Bytes resourceType,
            float quantity,
            float price,
            Entity destination)
        {
            if (!CanAddCargo(caravan, quantity))
                return false;

            manifest.Add(new CargoManifest
            {
                ResourceType = resourceType,
                Quantity = quantity,
                PurchasePrice = price,
                DestinationStorage = destination
            });
            
            caravan.CurrentCargoWeight += quantity;
            return true;
        }

        /// <summary>
        /// Simple deterministic random.
        /// </summary>
        private static uint DeterministicRandom(uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }
    }
}

