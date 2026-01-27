using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Buffer tracking all characters a specific player has anchored.
    /// Attached to player entity for per-player anchor management.
    /// </summary>
    [InternalBufferCapacity(8)] // Most players anchor 5-10 characters
    public struct PlayerAnchoredCharacter : IBufferElementData
    {
        /// <summary>
        /// The entity that is anchored.
        /// </summary>
        public Entity AnchoredEntity;

        /// <summary>
        /// When was this entity anchored?
        /// </summary>
        public uint AnchoredAtTick;

        /// <summary>
        /// Priority level for this anchor (for budget enforcement).
        /// </summary>
        public byte Priority;
    }

    /// <summary>
    /// Singleton component tracking global anchored character budget and telemetry.
    /// Prevents performance issues from unlimited anchoring.
    /// </summary>
    public struct AnchoredCharacterBudget : IComponentData
    {
        /// <summary>
        /// Maximum anchored characters per player.
        /// Default: 10
        /// </summary>
        public byte MaxPerPlayer;

        /// <summary>
        /// Current total anchored count across all players.
        /// </summary>
        public int TotalCount;

        /// <summary>
        /// Performance telemetry: total render cost of anchored characters (ms).
        /// Updated by budget system.
        /// </summary>
        public float RenderCostMs;

        /// <summary>
        /// Performance telemetry: total simulation cost of anchored characters (ms).
        /// Updated by budget system.
        /// </summary>
        public float SimCostMs;

        /// <summary>
        /// Last tick when budget was recalculated.
        /// </summary>
        public uint LastUpdateTick;

        /// <summary>
        /// Creates default budget with standard limits.
        /// </summary>
        public static AnchoredCharacterBudget Default => new AnchoredCharacterBudget
        {
            MaxPerPlayer = 10,
            TotalCount = 0,
            RenderCostMs = 0f,
            SimCostMs = 0f,
            LastUpdateTick = 0
        };
    }

    /// <summary>
    /// Tag component for the singleton entity holding the budget.
    /// </summary>
    public struct AnchoredCharacterBudgetTag : IComponentData { }
}

