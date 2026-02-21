using Unity.Collections;
using Unity.Entities;

namespace Space4X.Runtime
{
    public struct ShipSpecialEnergyConfig : IComponentData
    {
        public float BaseMax;
        public float BaseRegenPerSecond;
        public float ReactorOutputToMax;
        public float ReactorOutputToRegen;
        public float ReactorEfficiencyRegenMultiplier;
        public float RestartRegenPenaltyMultiplier;
        public float ActivationCostMultiplier;

        public static ShipSpecialEnergyConfig Default => new ShipSpecialEnergyConfig
        {
            BaseMax = 40f,
            BaseRegenPerSecond = 3f,
            ReactorOutputToMax = 0.02f,
            ReactorOutputToRegen = 0.0015f,
            ReactorEfficiencyRegenMultiplier = 1f,
            RestartRegenPenaltyMultiplier = 0.2f,
            ActivationCostMultiplier = 1f
        };
    }

    public struct ShipSpecialEnergyState : IComponentData
    {
        public float Current;
        public float EffectiveMax;
        public float EffectiveRegenPerSecond;
        public float LastSpent;
        public uint LastSpendTick;
        public ushort FailedSpendAttempts;
        public uint LastUpdatedTick;

        public float Ratio => EffectiveMax > 0f ? Current / EffectiveMax : 0f;
    }

    [InternalBufferCapacity(4)]
    public struct ShipSpecialEnergyPassiveModifier : IBufferElementData
    {
        public FixedString64Bytes SourceId;
        public float AdditiveMax;
        public float MultiplicativeMax;
        public float AdditiveRegenPerSecond;
        public float MultiplicativeRegen;
    }

    [InternalBufferCapacity(4)]
    public struct ShipSpecialEnergySpendRequest : IBufferElementData
    {
        public FixedString64Bytes Reason;
        public float Amount;
    }
}
