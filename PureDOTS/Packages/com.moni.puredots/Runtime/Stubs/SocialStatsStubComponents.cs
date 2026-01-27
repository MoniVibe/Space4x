// [TRI-STUB] Stub components for social stats system
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Social stats for individuals - fame, wealth, reputation, glory, renown.
    /// </summary>
    public struct SocialStats : IComponentData
    {
        /// <summary>
        /// Public recognition (0-1000). Legendary status threshold at 500+.
        /// </summary>
        public float Fame;

        /// <summary>
        /// Liquid wealth + asset value (currency).
        /// </summary>
        public float Wealth;

        /// <summary>
        /// Standing in community (-100 to +100).
        /// </summary>
        public float Reputation;

        /// <summary>
        /// Combat achievements, heroic deeds (0-1000).
        /// </summary>
        public float Glory;

        /// <summary>
        /// Overall legendary status (combines fame + glory) (0-1000).
        /// </summary>
        public float Renown;

        /// <summary>
        /// Check if entity has legendary status (Renown >= 500).
        /// </summary>
        public readonly bool IsLegendary => Renown >= 500f;

        /// <summary>
        /// Calculate renown from fame and glory.
        /// </summary>
        public static float CalculateRenown(float fame, float glory)
        {
            return (fame + glory) / 2f;
        }
    }
}

