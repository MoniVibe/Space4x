using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Type of world affordance (interactive elements in the world).
    /// </summary>
    public enum AffordanceType : byte
    {
        Storehouse = 0,
        BuildSite = 1,
        Climbable = 2,
        SignalEmitter = 3,
        RitualSite = 4,
        Cover = 5,
        Ladder = 6,
        Ledge = 7,
        Custom = 255
    }

    /// <summary>
    /// Definition of a world affordance.
    /// </summary>
    public struct AffordanceDef
    {
        public FixedString64Bytes AffordanceId;
        public AffordanceType Type;
        public float3 Position;
        public float Radius;
        public FixedString128Bytes Parameters;  // JSON or structured data
    }

    /// <summary>
    /// Registry of world affordances.
    /// Maps affordance IDs to definitions for scenario spawning.
    /// </summary>
    public struct AffordanceRegistry : IComponentData
    {
        public BlobAssetReference<AffordanceRegistryBlob> Registry;
    }

    /// <summary>
    /// Blob asset containing affordance definitions.
    /// </summary>
    public struct AffordanceRegistryBlob
    {
        public BlobArray<AffordanceDef> Affordances;
    }
}



