using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Environment state for vegetation entities.
    /// Updated by environment sampling systems.
    /// </summary>
    public struct VegetationEnvironmentState : IComponentData
    {
        public float Water;
        public float Light;
        public float Soil;
        public float Pollution;
        public float Wind;
        public uint LastSampleTick;
    }

    /// <summary>
    /// Tag indicating vegetation is under environmental stress.
    /// </summary>
    public struct VegetationStressedTag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Singleton configuration for vegetation environment effects.
    /// Baked from VegetationEnvironmentProfile ScriptableObject.
    /// </summary>
    public struct VegetationEnvironmentConfig : IComponentData
    {
        public float SoilRegenerationPerSecond;
        public float DroughtPenaltyScale;
        public float FrostPenaltyScale;
        public uint EnvironmentSeed;
    }
}

