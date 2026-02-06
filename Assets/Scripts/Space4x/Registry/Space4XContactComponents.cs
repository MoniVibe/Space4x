using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Tuning for contact standing tiers and loyalty points.
    /// </summary>
    public struct Space4XContactTierConfig : IComponentData
    {
        public half Tier1Standing;
        public half Tier2Standing;
        public half Tier3Standing;
        public float StandingGainScale;
        public float LpPerReward;

        public static Space4XContactTierConfig Default => new Space4XContactTierConfig
        {
            Tier1Standing = (half)0.3f,
            Tier2Standing = (half)0.55f,
            Tier3Standing = (half)0.8f,
            StandingGainScale = 1f,
            LpPerReward = 0.05f
        };
    }

    /// <summary>
    /// Standing/LP ledger entry for a contact (usually a faction or guild).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct Space4XContactStanding : IBufferElementData
    {
        public ushort ContactFactionId;
        public half Standing;
        public float LoyaltyPoints;
        public byte Tier;
    }

    public static class Space4XContactTierUtility
    {
        public static byte ResolveTier(float standing, in Space4XContactTierConfig config)
        {
            if (standing >= config.Tier3Standing)
            {
                return 3;
            }
            if (standing >= config.Tier2Standing)
            {
                return 2;
            }
            if (standing >= config.Tier1Standing)
            {
                return 1;
            }
            return 0;
        }
    }
}
