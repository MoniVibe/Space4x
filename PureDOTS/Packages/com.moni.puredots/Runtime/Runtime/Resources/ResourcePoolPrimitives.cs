using Unity.Mathematics;

namespace PureDOTS.Runtime.Resources
{
    /// <summary>
    /// Game-agnostic configuration for a rechargeable resource pool.
    /// </summary>
    public struct ResourcePoolConfig
    {
        public float BaseMax;
        public float BaseRegenPerSecond;
        public float SpendCostMultiplier;

        public static ResourcePoolConfig Default => new ResourcePoolConfig
        {
            BaseMax = 100f,
            BaseRegenPerSecond = 0f,
            SpendCostMultiplier = 1f
        };
    }

    /// <summary>
    /// Runtime state for a rechargeable resource pool.
    /// </summary>
    public struct ResourcePoolState
    {
        public float Current;
        public float EffectiveMax;
        public float EffectiveRegenPerSecond;
        public float LastSpent;
        public uint LastSpendTick;
        public ushort FailedSpendAttempts;
        public uint LastUpdatedTick;

        public float Ratio => EffectiveMax > 0f ? math.saturate(Current / EffectiveMax) : 0f;
    }

    /// <summary>
    /// Generic additive/multiplicative modifiers for pool cap and regen.
    /// </summary>
    public struct ResourcePoolModifier
    {
        public float AdditiveMax;
        public float MultiplicativeMax;
        public float AdditiveRegenPerSecond;
        public float MultiplicativeRegen;

        public static ResourcePoolModifier Identity => new ResourcePoolModifier
        {
            AdditiveMax = 0f,
            MultiplicativeMax = 1f,
            AdditiveRegenPerSecond = 0f,
            MultiplicativeRegen = 1f
        };
    }

    /// <summary>
    /// Generic spend request payload for resource pools.
    /// </summary>
    public struct ResourcePoolSpendRequest
    {
        public float Amount;
    }
}
