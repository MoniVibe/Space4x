using Unity.Entities;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Game-agnostic render classification used by simulation code.
    /// Maps to game-side render catalog entries.
    /// </summary>
    public struct RenderKey : IComponentData
    {
        /// <summary>
        /// Stable archetype identifier (e.g., villager, carrier, asteroid).
        /// </summary>
        public ushort ArchetypeId;

        /// <summary>
        /// Level of detail to request: 0 = full, 1 = mid, 2 = impostor.
        /// </summary>
        public byte LOD;
    }

    /// <summary>
    /// Toggle flags for visibility and lightweight presentation state.
    /// </summary>
    public struct RenderFlags : IComponentData
    {
        /// <summary>
        /// Non-zero if the entity should be visible.
        /// </summary>
        public byte Visible;

        /// <summary>
        /// Non-zero if the entity should cast shadows.
        /// </summary>
        public byte ShadowCaster;

        /// <summary>
        /// Bitmask for selection/outline/highlight channels.
        /// </summary>
        public byte HighlightMask;
    }

    /// <summary>
    /// Optional owner linkage for proxy/impostor entities.
    /// </summary>
    public struct RenderOwner : IComponentData
    {
        public Entity Owner;
    }
}
