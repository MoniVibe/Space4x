using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Normalized snapshot of individual stats (0..1).
    /// Provides a consistent contract for downstream systems.
    /// </summary>
    public struct Space4XNormalizedIndividualStats : IComponentData
    {
        public float Command;
        public float Tactics;
        public float Logistics;
        public float Diplomacy;
        public float Engineering;
        public float Resolve;
        public float Physique;
        public float Finesse;
        public float Will;
        public float Wisdom;
    }
}
