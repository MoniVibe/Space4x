using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Signal emitted by narrative systems for game-side consumption.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct NarrativeSignalBufferElement : IBufferElementData
    {
        public int SignalType;      // StartSituation, StepEntered, StepExiting, EventFired, RewardGranted, etc.
        public NarrativeId Id;      // event/situation/arc id
        public Entity Target;       // optional (village, colony, fleet, band, hero)
        public int PayloadA;        // game-defined
        public int PayloadB;
    }
}

