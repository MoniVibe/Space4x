using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    // NOTE: SimIndividualTag and IndividualId are defined in Runtime/Individual/IndividualComponents.cs
    // NOTE: AlignmentTriplet is defined in Runtime/Individual/AlignmentComponents.cs
    // NOTE: IndividualStats and ResourcePools are defined in Runtime/Individual/StatsComponents.cs
    // NOTE: PersonalityAxes is defined in Runtime/Individual/PersonalityComponents.cs
    // Duplicate definitions removed to avoid conflicts

    /// <summary>
    /// Affinity for might vs magic.
    /// Value ranges from -1 (pure magic) to 1 (pure might).
    /// </summary>
    public struct MightMagicAffinity : IComponentData
    {
        public float Value; // -1 = Magic, 0 = Balanced, 1 = Might
    }

    /// <summary>
    /// LOD level for an individual (determines which components are active).
    /// </summary>
    public struct IndividualLOD : IComponentData
    {
        public byte Level; // 0 = Full detail, higher = less detail
        public float ImportanceScore; // 0-1, determines LOD level
    }
}

