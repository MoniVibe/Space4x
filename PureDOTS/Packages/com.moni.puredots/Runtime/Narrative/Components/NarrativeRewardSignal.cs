using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Resolved reward/penalty signal for game-side systems.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct NarrativeRewardSignal : IBufferElementData
    {
        public int RewardType;    // ResourceDelta, OpinionDelta, TraitChange, UnitSpawn, Injury, etc.
        public Entity Target;
        public int Amount;
        public NarrativeId SourceId;
    }
}

