using Unity.Entities;

namespace PureDOTS.Runtime.Core
{
    /// <summary>
    /// Controls update cadence and phase offset for entity systems.
    /// Enables staggered updates to spread work across multiple ticks.
    /// </summary>
    public struct UpdateCadence : IComponentData
    {
        /// <summary>
        /// Update cadence - entity updates every N ticks.
        /// 1 = every tick, 5 = every 5 ticks, etc.
        /// </summary>
        public uint UpdateCadenceValue;

        /// <summary>
        /// Phase offset - random offset (0 to UpdateCadenceValue-1) to stagger updates.
        /// Ensures entities don't all update on the same tick.
        /// </summary>
        public uint PhaseOffset;

        /// <summary>
        /// Last tick when this entity was updated.
        /// </summary>
        public uint LastUpdateTick;

        /// <summary>
        /// Creates update cadence with specified frequency and random phase offset.
        /// </summary>
        public static UpdateCadence Create(uint cadence, uint phaseOffset)
        {
            return new UpdateCadence
            {
                UpdateCadenceValue = cadence,
                PhaseOffset = phaseOffset,
                LastUpdateTick = 0
            };
        }

        /// <summary>
        /// Creates update cadence with specified frequency and random phase offset based on entity hash.
        /// </summary>
        public static UpdateCadence CreateWithRandomPhase(uint cadence, uint entityHash)
        {
            uint phaseOffset = entityHash % cadence;
            return Create(cadence, phaseOffset);
        }
    }

    /// <summary>
    /// Helper methods for update cadence checks.
    /// </summary>
    public static class UpdateCadenceHelpers
    {
        /// <summary>
        /// Checks if entity should update on the current tick based on cadence and phase offset.
        /// </summary>
        /// <param name="currentTick">Current simulation tick.</param>
        /// <param name="cadence">Update cadence component.</param>
        /// <returns>True if entity should update this tick.</returns>
        public static bool ShouldUpdate(uint currentTick, in UpdateCadence cadence)
        {
            if (cadence.UpdateCadenceValue == 0)
            {
                return false; // Disabled
            }

            if (cadence.UpdateCadenceValue == 1)
            {
                return true; // Every tick
            }

            return (currentTick + cadence.PhaseOffset) % cadence.UpdateCadenceValue == 0;
        }

        /// <summary>
        /// Checks if entity should update, considering minimum time since last update.
        /// </summary>
        /// <param name="currentTick">Current simulation tick.</param>
        /// <param name="cadence">Update cadence component.</param>
        /// <param name="minTicksSinceUpdate">Minimum ticks that must have passed since last update.</param>
        /// <returns>True if entity should update this tick.</returns>
        public static bool ShouldUpdate(uint currentTick, in UpdateCadence cadence, uint minTicksSinceUpdate)
        {
            if (!ShouldUpdate(currentTick, cadence))
            {
                return false;
            }

            if (currentTick < cadence.LastUpdateTick + minTicksSinceUpdate)
            {
                return false;
            }

            return true;
        }
    }
}

