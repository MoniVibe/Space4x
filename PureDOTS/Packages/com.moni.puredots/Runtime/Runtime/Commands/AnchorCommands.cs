using Unity.Entities;
using PureDOTS.Runtime.Rendering;

namespace PureDOTS.Runtime.Commands
{
    /// <summary>
    /// Command to anchor a character entity.
    /// Processed by AnchorCharacterCommandSystem.
    /// Create an entity with this component to request anchoring.
    /// </summary>
    public struct AnchorCharacterCommand : IComponentData
    {
        /// <summary>
        /// The entity to anchor.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Which player is requesting the anchor.
        /// Entity.Null = shared anchor (all players).
        /// </summary>
        public Entity PlayerEntity;

        /// <summary>
        /// Priority level (0-10, higher = more important).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Reason for anchoring.
        /// </summary>
        public AnchorReason Reason;

        // Render configuration
        /// <summary>
        /// Minimum LOD level (0=full, 1=medium, 2=low).
        /// </summary>
        public byte MinLODLevel;

        /// <summary>
        /// Should shadows always be cast?
        /// </summary>
        public bool AlwaysCastShadows;

        /// <summary>
        /// Should VFX always render?
        /// </summary>
        public bool AlwaysRenderVFX;

        /// <summary>
        /// Maximum render distance (0 = infinite).
        /// </summary>
        public float MaxRenderDistance;

        // Simulation configuration
        /// <summary>
        /// Tick rate divisor when distant (1=full, 2=half, 4=quarter).
        /// </summary>
        public byte TickRateDivisor;

        /// <summary>
        /// Distance before reduced tick rate kicks in.
        /// </summary>
        public float DistanceForReduced;

        /// <summary>
        /// Always run full simulation regardless of distance?
        /// </summary>
        public bool AlwaysFullSimulation;

        /// <summary>
        /// Creates a default anchor command with standard settings.
        /// </summary>
        public static AnchorCharacterCommand Create(
            Entity target,
            Entity player,
            byte priority = 0,
            AnchorReason reason = AnchorReason.PlayerFavorite)
        {
            return new AnchorCharacterCommand
            {
                TargetEntity = target,
                PlayerEntity = player,
                Priority = priority,
                Reason = reason,
                MinLODLevel = 1, // Medium detail minimum
                AlwaysCastShadows = false,
                AlwaysRenderVFX = false,
                MaxRenderDistance = 0f, // Infinite
                TickRateDivisor = 1, // Full rate
                DistanceForReduced = 500f,
                AlwaysFullSimulation = false
            };
        }

        /// <summary>
        /// Creates a high-priority anchor command for important characters.
        /// </summary>
        public static AnchorCharacterCommand CreateHighPriority(
            Entity target,
            Entity player,
            AnchorReason reason = AnchorReason.Leader)
        {
            return new AnchorCharacterCommand
            {
                TargetEntity = target,
                PlayerEntity = player,
                Priority = 5,
                Reason = reason,
                MinLODLevel = 0, // Full detail
                AlwaysCastShadows = true,
                AlwaysRenderVFX = true,
                MaxRenderDistance = 0f, // Infinite
                TickRateDivisor = 1, // Full rate
                DistanceForReduced = 0f,
                AlwaysFullSimulation = true
            };
        }
    }

    /// <summary>
    /// Command to remove anchor from a character entity.
    /// Processed by AnchorCharacterCommandSystem.
    /// Create an entity with this component to request unanchoring.
    /// </summary>
    public struct UnanchorCharacterCommand : IComponentData
    {
        /// <summary>
        /// The entity to unanchor.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Which player is requesting the unanchor.
        /// Must match the original AnchoredBy or be a shared anchor.
        /// </summary>
        public Entity PlayerEntity;

        /// <summary>
        /// Creates an unanchor command.
        /// </summary>
        public static UnanchorCharacterCommand Create(Entity target, Entity player)
        {
            return new UnanchorCharacterCommand
            {
                TargetEntity = target,
                PlayerEntity = player
            };
        }
    }
}

