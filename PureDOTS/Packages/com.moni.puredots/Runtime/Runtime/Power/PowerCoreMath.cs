using Unity.Burst;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Power
{
    public enum PowerQuality : byte
    {
        Crude = 0,
        Standard = 1,
        Masterwork = 2,
        Legendary = 3,
        Artifact = 4
    }

    public struct PowerDistributionResult
    {
        public float OutputPower;
        public float TransmissionLoss;
        public float ActualEfficiency;
    }

    public struct BatteryChargeResult
    {
        public float NewStored;
        public float PowerConsumed; // MW
        public float ChargeLoss;
        public float ChargedAmount;
    }

    public struct BatteryDischargeResult
    {
        public float NewStored;
        public float PowerDelivered; // MW
        public float DischargeLoss;
        public float DischargedAmount;
    }

    public struct BatteryTechStats
    {
        public float CapacityMultiplier;
        public float SelfDischargeRate;
        public float ChargeEfficiency;
        public float DischargeEfficiency;
        public int MaxCycles;
    }

    public static class PowerCoreMath
    {
        [BurstCompile]
        public static float CalculateActualOutput(
            float theoreticalMax,
            float currentOutputPercent,
            float baseEfficiency,
            float degradationLevel,
            out float wasteHeat)
        {
            var operatingOutput = math.max(0f, theoreticalMax) * math.saturate(currentOutputPercent);
            var degradationPenalty = math.saturate(degradationLevel) * 0.25f;
            var actualEfficiency = math.saturate(baseEfficiency) * (1f - degradationPenalty);
            var actualOutput = operatingOutput * actualEfficiency;
            wasteHeat = operatingOutput * (1f - actualEfficiency);
            return actualOutput;
        }

        [BurstCompile]
        public static float GetReactorEfficiency(byte techLevel)
        {
            return techLevel switch
            {
                1 => 0.70f,
                2 => 0.74f,
                3 => 0.78f,
                4 => 0.82f,
                5 => 0.85f,
                6 => 0.88f,
                7 => 0.90f,
                8 => 0.92f,
                9 => 0.94f,
                10 => 0.95f,
                11 => 0.96f,
                12 => 0.97f,
                13 => 0.98f,
                14 => 0.985f,
                15 => 0.99f,
                _ => 0.70f
            };
        }

        [BurstCompile]
        public static PowerDistributionResult CalculateDistribution(
            float inputPower,
            float baseEfficiency,
            float conduitDamage)
        {
            var damagePenalty = math.saturate(conduitDamage) * 0.20f;
            var actualEfficiency = math.saturate(baseEfficiency) * (1f - damagePenalty);
            var outputPower = math.max(0f, inputPower) * actualEfficiency;
            var transmissionLoss = math.max(0f, inputPower) - outputPower;
            return new PowerDistributionResult
            {
                OutputPower = outputPower,
                TransmissionLoss = transmissionLoss,
                ActualEfficiency = actualEfficiency
            };
        }

        [BurstCompile]
        public static float GetDistributionEfficiency(byte techLevel)
        {
            return techLevel switch
            {
                1 => 0.85f,
                2 => 0.87f,
                3 => 0.89f,
                4 => 0.90f,
                5 => 0.92f,
                6 => 0.93f,
                7 => 0.94f,
                8 => 0.95f,
                9 => 0.96f,
                10 => 0.97f,
                11 => 0.975f,
                12 => 0.98f,
                13 => 0.985f,
                14 => 0.99f,
                15 => 0.995f,
                _ => 0.85f
            };
        }

        [BurstCompile]
        public static float CalculateSelfDischarge(float currentStored, float selfDischargeRate, float deltaTime)
        {
            return math.max(0f, currentStored) * math.max(0f, selfDischargeRate) * math.max(0f, deltaTime);
        }

        [BurstCompile]
        public static BatteryChargeResult ChargeBattery(
            float currentStored,
            float maxCapacity,
            float maxChargeRate,
            float chargeEfficiency,
            float availablePower,
            float deltaTime)
        {
            var roomAvailable = math.max(0f, maxCapacity - currentStored);
            var maxChargeThisTick = math.max(0f, maxChargeRate) * math.max(0f, deltaTime);
            var chargeAmount = math.min(maxChargeThisTick, math.max(0f, availablePower) * math.max(0f, deltaTime));
            chargeAmount = math.min(chargeAmount, roomAvailable);

            var efficiency = math.saturate(chargeEfficiency);
            var actualStored = chargeAmount * efficiency;
            var chargeLoss = chargeAmount - actualStored;
            var newStored = currentStored + actualStored;

            return new BatteryChargeResult
            {
                NewStored = newStored,
                PowerConsumed = deltaTime > 0f ? chargeAmount / deltaTime : 0f,
                ChargeLoss = chargeLoss,
                ChargedAmount = actualStored
            };
        }

        [BurstCompile]
        public static BatteryDischargeResult DischargeBattery(
            float currentStored,
            float maxDischargeRate,
            float dischargeEfficiency,
            float requestedPower,
            float deltaTime)
        {
            var maxDischargeThisTick = math.max(0f, maxDischargeRate) * math.max(0f, deltaTime);
            var dischargeAmount = math.min(maxDischargeThisTick, math.max(0f, requestedPower) * math.max(0f, deltaTime));
            dischargeAmount = math.min(dischargeAmount, math.max(0f, currentStored));

            var efficiency = math.saturate(dischargeEfficiency);
            var actualOutput = dischargeAmount * efficiency;
            var dischargeLoss = dischargeAmount - actualOutput;
            var newStored = currentStored - dischargeAmount;

            return new BatteryDischargeResult
            {
                NewStored = newStored,
                PowerDelivered = deltaTime > 0f ? actualOutput / deltaTime : 0f,
                DischargeLoss = dischargeLoss,
                DischargedAmount = dischargeAmount
            };
        }

        [BurstCompile]
        public static float CalculateBatteryHealth(int currentCycles, int maxCycles)
        {
            if (currentCycles < maxCycles)
            {
                return 1.0f;
            }

            var excessCycles = currentCycles - maxCycles;
            var degradation = maxCycles > 0 ? excessCycles / (maxCycles * 2f) : 1f;
            var health = 1.0f - degradation;
            return math.clamp(health, 0.3f, 1.0f);
        }

        [BurstCompile]
        public static BatteryTechStats GetBatteryTechStats(byte techLevel)
        {
            return techLevel switch
            {
                1 => new BatteryTechStats
                {
                    CapacityMultiplier = 1.0f,
                    SelfDischargeRate = 0.001f,
                    ChargeEfficiency = 0.85f,
                    DischargeEfficiency = 0.83f,
                    MaxCycles = 2000
                },
                5 => new BatteryTechStats
                {
                    CapacityMultiplier = 1.5f,
                    SelfDischargeRate = 0.0005f,
                    ChargeEfficiency = 0.92f,
                    DischargeEfficiency = 0.90f,
                    MaxCycles = 5000
                },
                10 => new BatteryTechStats
                {
                    CapacityMultiplier = 2.5f,
                    SelfDischargeRate = 0.0002f,
                    ChargeEfficiency = 0.96f,
                    DischargeEfficiency = 0.95f,
                    MaxCycles = 10000
                },
                15 => new BatteryTechStats
                {
                    CapacityMultiplier = 4.0f,
                    SelfDischargeRate = 0.00005f,
                    ChargeEfficiency = 0.99f,
                    DischargeEfficiency = 0.98f,
                    MaxCycles = 50000
                },
                _ => GetBatteryTechStats(1)
            };
        }

        [BurstCompile]
        public static float CalculateModuleEffectiveness(float powerAllocationPercent)
        {
            if (powerAllocationPercent <= 0f)
            {
                return 0f;
            }

            var p = powerAllocationPercent;
            var effectiveness = 0.4f + (p * 0.008f) - (p * p * 0.00001f);
            return math.clamp(effectiveness, 0f, 1.8f);
        }

        [BurstCompile]
        public static float CalculateHeatGeneration(float baseHeatGeneration, float powerAllocationPercent)
        {
            var powerRatio = powerAllocationPercent / 100f;
            var heatMultiplier = powerRatio * powerRatio;
            return math.max(0f, baseHeatGeneration) * heatMultiplier;
        }

        [BurstCompile]
        public static float CalculatePowerConsumption(float baselinePowerDraw, float powerAllocationPercent)
        {
            return math.max(0f, baselinePowerDraw) * (powerAllocationPercent / 100f);
        }

        [BurstCompile]
        public static float CalculateBurnoutRisk(float powerAllocationPercent, PowerQuality quality, float deltaTime)
        {
            if (powerAllocationPercent <= 100f)
            {
                return 0f;
            }

            var overchargeAmount = powerAllocationPercent - 100f;
            var baseRiskPerMinute = overchargeAmount * 0.04f;
            var baseRiskPerSecond = baseRiskPerMinute / 60f;

            var qualityModifier = quality switch
            {
                PowerQuality.Crude => 2.0f,
                PowerQuality.Standard => 1.0f,
                PowerQuality.Masterwork => 0.6f,
                PowerQuality.Legendary => 0.3f,
                PowerQuality.Artifact => 0.05f,
                _ => 1.0f
            };

            var adjustedRisk = baseRiskPerSecond * qualityModifier * math.max(0f, deltaTime);
            return math.clamp(adjustedRisk, 0f, 1f);
        }
    }
}
