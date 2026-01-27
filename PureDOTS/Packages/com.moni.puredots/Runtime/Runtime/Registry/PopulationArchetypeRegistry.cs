using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Definition of a population archetype (roles, needs, loadouts, capabilities, default directives).
    /// Used for registry-driven spawning in scenarios.
    /// </summary>
    public struct PopulationArchetypeDef
    {
        public FixedString64Bytes ArchetypeId;
        public FixedString64Bytes Name;
        public FixedString128Bytes Description;
        
        // Role information
        public FixedString32Bytes DefaultRole;
        
        // Needs configuration
        public float HungerDecayRate;
        public float EnergyDecayRate;
        public float MoraleDecayRate;
        
        // Capabilities
        public byte CanClimb;
        public byte CanSwim;
        public byte CanFly;
        
        // Default directives
        public FixedString32Bytes DefaultDirective;
    }

    /// <summary>
    /// Registry of population archetypes.
    /// Maps archetype IDs to definitions for scenario spawning.
    /// </summary>
    public struct PopulationArchetypeRegistry : IComponentData
    {
        public BlobAssetReference<PopulationArchetypeRegistryBlob> Registry;
    }

    /// <summary>
    /// Blob asset containing archetype definitions.
    /// </summary>
    public struct PopulationArchetypeRegistryBlob
    {
        public BlobArray<PopulationArchetypeDef> Archetypes;
    }
}



