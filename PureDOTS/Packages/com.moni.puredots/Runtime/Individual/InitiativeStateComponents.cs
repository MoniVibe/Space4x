using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Unified initiative state for SimIndividuals.
    /// Uses normalized [0..1] range for consistency.
    /// </summary>
    public struct InitiativeState : IComponentData
    {
        /// <summary>
        /// Current initiative [0..1]. Charges over time, consumed by actions.
        /// </summary>
        public float Current;

        /// <summary>
        /// Initiative gain per fixed tick [0..1]. Calculated from Finesse + Agility + Will, modified by Morale/Boldness.
        /// </summary>
        public float GainPerTick;

        /// <summary>
        /// Action cost [0..1]. How much initiative is consumed per action.
        /// </summary>
        public float ActionCost;

        /// <summary>
        /// Ready flag. Set to true when Current >= ActionCost, allowing entity to act.
        /// </summary>
        public bool Ready;

        /// <summary>
        /// Tick when initiative was last updated (for charge calculations).
        /// </summary>
        public uint LastUpdateTick;

        /// <summary>
        /// Create from gain and cost values.
        /// </summary>
        public static InitiativeState FromValues(float gainPerTick, float actionCost)
        {
            return new InitiativeState
            {
                Current = 0f,
                GainPerTick = math.max(0f, gainPerTick),
                ActionCost = math.max(0f, math.min(1f, actionCost)),
                Ready = false
            };
        }
    }
}

