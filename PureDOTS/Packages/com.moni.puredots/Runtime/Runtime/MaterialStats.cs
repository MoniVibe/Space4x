using Unity.Entities;

namespace PureDOTS.Runtime
{
    /// <summary>
    /// Material properties for collision damage calculation.
    /// Attached to entities (ships, rocks, buildings, projectiles) to define their material characteristics.
    /// </summary>
    public struct MaterialStats : IComponentData
    {
        /// <summary>
        /// Resistance to deformation. Higher values mean the material is harder and deals/takes more damage.
        /// Typical values:
        /// - Rock: 2.0
        /// - Ship hull: 1.5
        /// - Villager/soft stuff: 0.5
        /// </summary>
        public float Hardness;

        /// <summary>
        /// How easily the material shatters (for rocks, glass, etc.).
        /// Higher values mean the material breaks more easily when damaged.
        /// Typical values:
        /// - Brittle rock: 1.5
        /// - Durable rock: 0.5
        /// - Ship hull: 0.1 (doesn't shatter)
        /// </summary>
        public float Fragility;

        /// <summary>
        /// Material density. Used for computing effective mass in collision calculations.
        /// Can be used to make heavier objects deal more damage.
        /// Typical values:
        /// - Rock: 3.0
        /// - Ship hull: 2.0
        /// - Villager/soft stuff: 0.8
        /// </summary>
        public float Density;
    }
}

























