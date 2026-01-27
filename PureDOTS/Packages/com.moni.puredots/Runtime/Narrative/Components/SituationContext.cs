using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Context information for a situation instance.
    /// </summary>
    public struct SituationContext : IComponentData
    {
        public Entity Location;           // colony, village, star system, etc.
        public Entity OwningFaction;      // optional
        public NarrativeTagMask Tags;     // copy from archetype + dynamic
    }
}

