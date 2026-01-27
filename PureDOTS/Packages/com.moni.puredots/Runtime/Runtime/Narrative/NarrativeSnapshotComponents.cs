using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Narrative snapshot for hot path consumption.
    /// Consuming already-fired "beats" (ritual, raided village, anomaly).
    /// Light updates to state.
    /// </summary>
    public struct NarrativeSnapshot : IComponentData
    {
        /// <summary>
        /// Active narrative beats affecting this entity.
        /// </summary>
        public NarrativeBeatFlags ActiveBeats;

        /// <summary>
        /// Awareness level of recent events (0..1).
        /// </summary>
        public float EventAwareness;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Narrative beat flags indicating what events the entity is aware of.
    /// </summary>
    [System.Flags]
    public enum NarrativeBeatFlags : ushort
    {
        None = 0,
        RitualHappened = 1 << 0,
        VillageRaided = 1 << 1,
        AnomalyDetected = 1 << 2,
        WarDeclared = 1 << 3,
        AllianceFormed = 1 << 4,
        DisasterOccurred = 1 << 5
    }
}

