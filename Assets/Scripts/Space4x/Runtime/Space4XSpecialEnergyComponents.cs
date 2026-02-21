using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.Resources;

namespace Space4X.Runtime
{
    public struct ShipSpecialEnergyConfig : IComponentData
    {
        public ResourcePoolConfig Pool;
        public float ReactorOutputToMax;
        public float ReactorOutputToRegen;
        public float ReactorEfficiencyRegenMultiplier;
        public float RestartRegenPenaltyMultiplier;

        public float BaseMax
        {
            get => Pool.BaseMax;
            set => Pool.BaseMax = value;
        }

        public float BaseRegenPerSecond
        {
            get => Pool.BaseRegenPerSecond;
            set => Pool.BaseRegenPerSecond = value;
        }

        public float ActivationCostMultiplier
        {
            get => Pool.SpendCostMultiplier;
            set => Pool.SpendCostMultiplier = value;
        }

        public static ShipSpecialEnergyConfig Default => new ShipSpecialEnergyConfig
        {
            Pool = new ResourcePoolConfig
            {
                BaseMax = 40f,
                BaseRegenPerSecond = 3f,
                SpendCostMultiplier = 1f
            },
            ReactorOutputToMax = 0.02f,
            ReactorOutputToRegen = 0.0015f,
            ReactorEfficiencyRegenMultiplier = 1f,
            RestartRegenPenaltyMultiplier = 0.2f
        };
    }

    public struct ShipSpecialEnergyState : IComponentData
    {
        public ResourcePoolState Pool;

        public float Current
        {
            get => Pool.Current;
            set => Pool.Current = value;
        }

        public float EffectiveMax
        {
            get => Pool.EffectiveMax;
            set => Pool.EffectiveMax = value;
        }

        public float EffectiveRegenPerSecond
        {
            get => Pool.EffectiveRegenPerSecond;
            set => Pool.EffectiveRegenPerSecond = value;
        }

        public float LastSpent
        {
            get => Pool.LastSpent;
            set => Pool.LastSpent = value;
        }

        public uint LastSpendTick
        {
            get => Pool.LastSpendTick;
            set => Pool.LastSpendTick = value;
        }

        public ushort FailedSpendAttempts
        {
            get => Pool.FailedSpendAttempts;
            set => Pool.FailedSpendAttempts = value;
        }

        public uint LastUpdatedTick
        {
            get => Pool.LastUpdatedTick;
            set => Pool.LastUpdatedTick = value;
        }

        public float Ratio => Pool.Ratio;
    }

    [InternalBufferCapacity(4)]
    public struct ShipSpecialEnergyPassiveModifier : IBufferElementData
    {
        public FixedString64Bytes SourceId;
        public ResourcePoolModifier PoolModifier;

        public float AdditiveMax
        {
            get => PoolModifier.AdditiveMax;
            set => PoolModifier.AdditiveMax = value;
        }

        public float MultiplicativeMax
        {
            get => PoolModifier.MultiplicativeMax;
            set => PoolModifier.MultiplicativeMax = value;
        }

        public float AdditiveRegenPerSecond
        {
            get => PoolModifier.AdditiveRegenPerSecond;
            set => PoolModifier.AdditiveRegenPerSecond = value;
        }

        public float MultiplicativeRegen
        {
            get => PoolModifier.MultiplicativeRegen;
            set => PoolModifier.MultiplicativeRegen = value;
        }
    }

    [InternalBufferCapacity(4)]
    public struct ShipSpecialEnergySpendRequest : IBufferElementData
    {
        public FixedString64Bytes Reason;
        public ResourcePoolSpendRequest Request;

        public float Amount
        {
            get => Request.Amount;
            set => Request.Amount = value;
        }
    }
}
