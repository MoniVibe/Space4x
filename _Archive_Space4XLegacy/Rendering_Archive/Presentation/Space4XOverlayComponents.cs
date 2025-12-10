using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    // ============================================================================
    // Resource Overlay Components
    // ============================================================================

    /// <summary>
    /// Per-asteroid resource overlay data.
    /// </summary>
    public struct ResourceOverlayData : IComponentData
    {
        /// <summary>Richness level (0-1) based on ResourceAmount / MaxResourceAmount</summary>
        public float RichnessLevel;
        /// <summary>True if mined in last N ticks</summary>
        public bool RecentlyMined;
        /// <summary>Number of active miners</summary>
        public int MiningActivity;
        /// <summary>Last harvest tick (from ResourceSourceState)</summary>
        public uint LastHarvestTick;
    }

    // ============================================================================
    // Faction Overlay Components
    // ============================================================================

    /// <summary>
    /// Per-entity faction overlay data.
    /// </summary>
    public struct FactionOverlayData : IComponentData
    {
        /// <summary>Faction ID</summary>
        public int FactionId;
        /// <summary>Control strength (0-1) - how strongly faction controls this entity</summary>
        public float ControlStrength;
        /// <summary>True if player-controlled</summary>
        public bool IsPlayerControlled;
    }

    // ============================================================================
    // Logistics Route Overlay Components
    // ============================================================================

    /// <summary>
    /// Logistics route visualization data.
    /// </summary>
    public struct LogisticsRouteOverlay : IComponentData
    {
        public FixedString64Bytes RouteId;
        public float3 OriginPosition;
        public float3 DestinationPosition;
        public Space4XLogisticsRouteStatus RouteStatus;
        public float Throughput;
        public float LineWidth;
        public float4 LineColor;
    }

    /// <summary>
    /// Marker component for route overlay entities (visual-only, not sim entities).
    /// </summary>
    public struct RouteOverlayTag : IComponentData { }
}

