using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Request to spawn a new situation instance.
    /// Written by game-side or event effects, consumed by SituationSpawnSystem.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SituationSpawnRequest : IBufferElementData
    {
        public NarrativeId SituationId;
        public Entity Location;
        public Entity Faction;
        public NarrativeTagMask Tags;
    }
}

