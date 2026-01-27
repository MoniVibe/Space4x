using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Minimal rewind state snapshot for hot path reads.
    /// Systems respect RewindState.Mode and early-out on Playback.
    /// Small ring buffers for critical fast histories only when necessary.
    /// </summary>
    public struct RewindStateSnapshot : IComponentData
    {
        /// <summary>
        /// Current rewind mode (Record, Playback, CatchUp).
        /// </summary>
        public RewindMode Mode;

        /// <summary>
        /// Current simulation tick.
        /// </summary>
        public uint CurrentTick;

        /// <summary>
        /// Whether simulation is paused.
        /// </summary>
        public byte IsPaused;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }
}

