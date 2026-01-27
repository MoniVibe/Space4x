using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Resources
{
    /// <summary>
    /// Static helpers for supply chain calculations.
    /// </summary>
    [BurstCompile]
    public static class SupplyChainHelpers
    {
        /// <summary>
        /// Default supply chain configuration.
        /// </summary>
        public static SupplyChainConfig DefaultConfig => new SupplyChainConfig
        {
            EmergencyThreshold = 3f,       // 3 days
            WarningThreshold = 7f,         // 7 days
            ReserveRatio = 0.2f,           // 20%
            MaxRouteDistance = 1000f,
            EfficiencyMinimum = 0.1f
        };

        /// <summary>
        /// Calculates total consumption rate from multiple consumers.
        /// </summary>
        public static float CalculateTotalConsumption(in DynamicBuffer<ConsumptionRateEntry> rates)
        {
            float total = 0;
            for (int i = 0; i < rates.Length; i++)
            {
                total += rates[i].Rate.CurrentRate;
            }
            return total;
        }

        /// <summary>
        /// Calculates burn rate with efficiency modifier.
        /// </summary>
        public static float CalculateBurnRate(float baseRate, int consumerCount, float efficiency)
        {
            float rawRate = baseRate * consumerCount;
            return rawRate * (2f - math.clamp(efficiency, 0f, 1f)); // Low efficiency = higher burn
        }

        /// <summary>
        /// Estimates days until supply exhaustion.
        /// </summary>
        public static float EstimateSupplyDuration(float currentSupply, float consumptionRate, float incomeRate)
        {
            float netBurn = consumptionRate - incomeRate;
            if (netBurn <= 0) return float.MaxValue; // Not depleting
            return currentSupply / netBurn;
        }

        /// <summary>
        /// Calculates route efficiency score.
        /// </summary>
        public static float CalculateRouteEfficiency(float distance, float capacity, float travelTime, float riskFactor)
        {
            if (travelTime <= 0) return 0;
            
            // Throughput: how much delivered per unit time
            float throughput = capacity / travelTime;
            
            // Risk adjustment
            float safeDelivery = 1f - math.clamp(riskFactor, 0f, 1f);
            
            // Distance penalty (longer = less efficient)
            float distanceFactor = 1f / (1f + distance * 0.01f);
            
            return throughput * safeDelivery * distanceFactor;
        }

        /// <summary>
        /// Checks if supply situation is emergency.
        /// </summary>
        public static bool IsEmergencyThreshold(float currentSupply, float consumptionRate, float emergencyDays)
        {
            float daysRemaining = EstimateSupplyDuration(currentSupply, consumptionRate, 0);
            return daysRemaining < emergencyDays;
        }

        /// <summary>
        /// Calculates supply ratio (supply / demand).
        /// </summary>
        public static float CalculateSupplyRatio(float currentSupply, float consumptionRate)
        {
            if (consumptionRate <= 0) return 1f;
            
            // How many days of consumption we have
            float daysOfSupply = currentSupply / consumptionRate;
            
            // Normalize to 0-1 range (7 days = 1.0, more is capped)
            return math.saturate(daysOfSupply / 7f);
        }

        /// <summary>
        /// Finds the best supply source for a consumer.
        /// </summary>
        public static Entity FindBestSupplySource(
            NativeArray<Entity> sources,
            NativeArray<SupplySource> sourceData,
            float3 consumerPosition,
            NativeArray<float3> sourcePositions,
            float maxDistance,
            out float efficiency)
        {
            Entity best = Entity.Null;
            float bestScore = 0;
            efficiency = 0;

            for (int i = 0; i < sources.Length; i++)
            {
                if (sourceData[i].IsAvailable == 0) continue;
                
                float distance = math.distance(consumerPosition, sourcePositions[i]);
                if (distance > maxDistance) continue;
                
                // Score: available stock / distance
                float availableStock = sourceData[i].CurrentStock * (1f - sourceData[i].ReserveRatio);
                if (availableStock <= 0) continue;
                
                float score = availableStock / (1f + distance * 0.1f);
                
                if (score > bestScore)
                {
                    bestScore = score;
                    best = sources[i];
                    efficiency = CalculateRouteEfficiency(distance, availableStock, distance * 2f, 0);
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates supply deficit.
        /// </summary>
        public static float CalculateDeficit(float consumption, float income, float targetReserve, float currentSupply)
        {
            float netLoss = consumption - income;
            if (netLoss <= 0) return 0;
            
            // How much extra income needed to maintain reserve
            float targetStock = consumption * targetReserve;
            float deficit = netLoss + math.max(0, targetStock - currentSupply);
            
            return deficit;
        }

        /// <summary>
        /// Updates supply status from current data.
        /// </summary>
        public static SupplyStatus UpdateSupplyStatus(
            float currentSupply,
            float maxCapacity,
            in DynamicBuffer<ConsumptionRateEntry> rates,
            float incomeRate,
            in SupplyChainConfig config,
            uint currentTick)
        {
            float consumption = CalculateTotalConsumption(rates);
            float netFlow = incomeRate - consumption;
            float daysRemaining = EstimateSupplyDuration(currentSupply, consumption, incomeRate);
            
            return new SupplyStatus
            {
                TotalSupply = currentSupply,
                MaxCapacity = maxCapacity,
                TotalConsumption = consumption,
                NetFlow = netFlow,
                DaysRemaining = daysRemaining,
                IsInDeficit = (byte)(netFlow < 0 ? 1 : 0),
                IsEmergency = (byte)(daysRemaining < config.EmergencyThreshold ? 1 : 0),
                LastUpdateTick = currentTick
            };
        }

        /// <summary>
        /// Checks if emergency foraging should start.
        /// </summary>
        public static bool ShouldStartForaging(in SupplyStatus status, in SupplyChainConfig config)
        {
            return status.IsEmergency != 0 && status.DaysRemaining < config.EmergencyThreshold;
        }

        /// <summary>
        /// Calculates optimal delivery quantity.
        /// </summary>
        public static float CalculateDeliveryQuantity(float deficit, float transportCapacity, float sourceAvailable)
        {
            // Don't deliver more than needed or available
            return math.min(deficit, math.min(transportCapacity, sourceAvailable));
        }

        /// <summary>
        /// Gets consumption rate for a specific resource.
        /// </summary>
        public static float GetConsumptionRate(in DynamicBuffer<ConsumptionRateEntry> rates, ushort resourceTypeId)
        {
            for (int i = 0; i < rates.Length; i++)
            {
                if (rates[i].Rate.ResourceTypeId == resourceTypeId)
                {
                    return rates[i].Rate.CurrentRate;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Sets consumption rate for a specific resource.
        /// </summary>
        public static void SetConsumptionRate(ref DynamicBuffer<ConsumptionRateEntry> rates, ushort resourceTypeId, float rate, float efficiency)
        {
            for (int i = 0; i < rates.Length; i++)
            {
                if (rates[i].Rate.ResourceTypeId == resourceTypeId)
                {
                    var entry = rates[i];
                    entry.Rate.CurrentRate = rate;
                    entry.Rate.Efficiency = efficiency;
                    rates[i] = entry;
                    return;
                }
            }

            // Add new entry
            rates.Add(new ConsumptionRateEntry
            {
                Rate = new ConsumptionRate
                {
                    ResourceTypeId = resourceTypeId,
                    BaseRate = rate,
                    CurrentRate = rate,
                    Efficiency = efficiency
                }
            });
        }
    }
}

