using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Player choice input for a situation.
    /// Written by game-side UI, consumed by SituationUpdateSystem.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SituationChoice : IBufferElementData
    {
        public Entity SituationEntity;
        public int OptionIndex;       // from game's UI mapping
        public NarrativeId ChoiceId;  // optional, for analytics
    }
}

