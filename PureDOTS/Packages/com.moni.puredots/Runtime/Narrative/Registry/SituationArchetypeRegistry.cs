#nullable disable
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Registry blob containing all situation archetype definitions.
    /// </summary>
    public struct SituationArchetypeRegistry
    {
        public BlobArray<SituationArchetype> Archetypes;
    }
}

