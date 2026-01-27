using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Unified alignment triplet used across Godgame and Space4X.
    /// Replaces Space4X's Law/Good/Integrity with Moral/Order/Purity.
    /// Values are normalized [-1..+1].
    /// </summary>
    public struct AlignmentTriplet : IComponentData
    {
        /// <summary>
        /// Moral axis: Good (+1) vs Evil (-1).
        /// </summary>
        public float Moral;

        /// <summary>
        /// Order axis: Order (+1) vs Chaos (-1).
        /// </summary>
        public float Order;

        /// <summary>
        /// Purity axis: Pure (+1) vs Corrupt (-1).
        /// </summary>
        public float Purity;

        /// <summary>
        /// Create from float values with clamping.
        /// </summary>
        public static AlignmentTriplet FromFloats(float moral, float order, float purity)
        {
            return new AlignmentTriplet
            {
                Moral = math.clamp(moral, -1f, 1f),
                Order = math.clamp(order, -1f, 1f),
                Purity = math.clamp(purity, -1f, 1f)
            };
        }

        /// <summary>
        /// Convert to float3 for vector operations.
        /// </summary>
        public readonly float3 AsFloat3() => new float3(Moral, Order, Purity);

        /// <summary>
        /// Compute distance between two alignments (for compatibility checks).
        /// </summary>
        public static float Distance(AlignmentTriplet a, AlignmentTriplet b)
        {
            var diff = a.AsFloat3() - b.AsFloat3();
            return math.length(diff);
        }
    }

    /// <summary>
    /// Might vs Magic alignment axis for fantasy settings.
    /// Axis: -1 = pure might, +1 = pure magic.
    /// Strength: how strongly aligned (0..1).
    /// </summary>
    public struct MightMagicAlignment : IComponentData
    {
        /// <summary>
        /// Alignment axis: -1 (pure might) to +1 (pure magic).
        /// </summary>
        public float Axis;

        /// <summary>
        /// Alignment strength [0..1]. How strongly aligned to might or magic.
        /// </summary>
        public float Strength;

        /// <summary>
        /// Precomputed bonus to might-based actions (combat, physical).
        /// Calculated by AlignmentBonusCalculationSystem.
        /// </summary>
        public float MightBonus;

        /// <summary>
        /// Precomputed bonus to magic-based actions (spells, miracles).
        /// Calculated by AlignmentBonusCalculationSystem.
        /// </summary>
        public float MagicBonus;

        /// <summary>
        /// Precomputed penalty to opposite actions (might-aligned gets magic penalty, etc.).
        /// Calculated by AlignmentBonusCalculationSystem.
        /// </summary>
        public float OppositePenalty;

        /// <summary>
        /// Create from axis and strength with clamping.
        /// </summary>
        public static MightMagicAlignment FromAxis(float axis, float strength)
        {
            return new MightMagicAlignment
            {
                Axis = math.clamp(axis, -1f, 1f),
                Strength = math.clamp(strength, 0f, 1f)
            };
        }
    }
}

