using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    // ============================================================================
    // LOD Components
    // ============================================================================

    /// <summary>
    /// LOD level for presentation rendering.
    /// </summary>
    public enum PresentationLODLevel : byte
    {
        FullDetail = 0,    // 0-100 units: Full mesh, all crafts visible
        ReducedDetail = 1, // 100-500 units: Simplified mesh, fewer crafts
        Impostor = 2,      // 500-2000 units: Fleet icon at centroid
        Hidden = 3         // >2000 units: Not rendered
    }

    /// <summary>
    /// Per-entity LOD level assigned by the LOD system based on camera distance.
    /// </summary>
    public struct PresentationLOD : IComponentData
    {
        public PresentationLODLevel Level;
        public float DistanceToCamera;
    }

    /// <summary>
    /// Configuration for LOD distance thresholds. Singleton component.
    /// </summary>
    public struct PresentationLODConfig : IComponentData
    {
        /// <summary>Distance threshold for FullDetail → ReducedDetail (default: 100)</summary>
        public float FullDetailMaxDistance;
        /// <summary>Distance threshold for ReducedDetail → Impostor (default: 500)</summary>
        public float ReducedDetailMaxDistance;
        /// <summary>Distance threshold for Impostor → Hidden (default: 2000)</summary>
        public float ImpostorMaxDistance;

        public static PresentationLODConfig Default => new PresentationLODConfig
        {
            FullDetailMaxDistance = 100f,
            ReducedDetailMaxDistance = 500f,
            ImpostorMaxDistance = 2000f
        };
    }

    // ============================================================================
    // Presentation Tag Components
    // ============================================================================

    /// <summary>
    /// Marker component for carrier entities in the presentation layer.
    /// </summary>
    public struct CarrierPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for craft/mining vessel entities in the presentation layer.
    /// </summary>
    public struct CraftPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for asteroid entities in the presentation layer.
    /// </summary>
    public struct AsteroidPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for resource pickup entities in the presentation layer.
    /// </summary>
    public struct ResourcePickupPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for fleet impostor entities (aggregate visualization).
    /// </summary>
    public struct FleetImpostorTag : IComponentData { }

    /// <summary>
    /// Marker component for entities that should be rendered (used with render density).
    /// </summary>
    public struct ShouldRenderTag : IComponentData { }

    /// <summary>
    /// Marker component for selected entities.
    /// </summary>
    public struct SelectedTag : IComponentData { }

    // ============================================================================
    // Faction & Color Components
    // ============================================================================

    /// <summary>
    /// Per-entity faction color for visual distinction.
    /// </summary>
    public struct FactionColor : IComponentData
    {
        public float4 Value; // RGBA

        public static FactionColor Red => new FactionColor { Value = new float4(1f, 0.2f, 0.2f, 1f) };
        public static FactionColor Blue => new FactionColor { Value = new float4(0.2f, 0.4f, 1f, 1f) };
        public static FactionColor Green => new FactionColor { Value = new float4(0.2f, 1f, 0.2f, 1f) };
        public static FactionColor Yellow => new FactionColor { Value = new float4(1f, 1f, 0.2f, 1f) };
        public static FactionColor Purple => new FactionColor { Value = new float4(0.8f, 0.2f, 1f, 1f) };
        public static FactionColor Orange => new FactionColor { Value = new float4(1f, 0.6f, 0.2f, 1f) };
    }

    /// <summary>
    /// Per-entity resource type color for asteroids and pickups.
    /// </summary>
    public struct ResourceTypeColor : IComponentData
    {
        public float4 Value; // RGBA

        public static ResourceTypeColor Minerals => new ResourceTypeColor { Value = new float4(0.6f, 0.6f, 0.6f, 1f) };
        public static ResourceTypeColor RareMetals => new ResourceTypeColor { Value = new float4(0.8f, 0.7f, 0.2f, 1f) };
        public static ResourceTypeColor EnergyCrystals => new ResourceTypeColor { Value = new float4(0.2f, 0.8f, 1f, 1f) };
        public static ResourceTypeColor OrganicMatter => new ResourceTypeColor { Value = new float4(0.2f, 0.8f, 0.3f, 1f) };
    }

    /// <summary>
    /// Reference to parent carrier entity for crafts.
    /// </summary>
    public struct ParentCarrier : IComponentData
    {
        public Entity Value;
    }

    // ============================================================================
    // Visual State Components
    // ============================================================================

    /// <summary>
    /// Visual state for carrier entities.
    /// </summary>
    public enum CarrierVisualStateType : byte
    {
        Idle = 0,
        Patrolling = 1,
        Mining = 2,
        Combat = 3,
        Retreating = 4
    }

    /// <summary>
    /// Per-carrier visual state component.
    /// </summary>
    public struct CarrierVisualState : IComponentData
    {
        public CarrierVisualStateType State;
        public float StateTimer; // For animations/pulsing
    }

    /// <summary>
    /// Visual state for craft/mining vessel entities.
    /// </summary>
    public enum CraftVisualStateType : byte
    {
        Idle = 0,    // Docked/waiting
        Mining = 1,  // At asteroid, extracting
        Returning = 2, // Moving back to carrier
        Moving = 3   // En route to target
    }

    /// <summary>
    /// Per-craft visual state component.
    /// </summary>
    public struct CraftVisualState : IComponentData
    {
        public CraftVisualStateType State;
        public float StateTimer;
    }

    /// <summary>
    /// Visual state for asteroid entities.
    /// </summary>
    public enum AsteroidVisualStateType : byte
    {
        Full = 0,        // Bright resource color
        MiningActive = 1, // Being mined, pulsing
        Depleted = 2     // Gray, low alpha
    }

    /// <summary>
    /// Per-asteroid visual state component.
    /// </summary>
    public struct AsteroidVisualState : IComponentData
    {
        public AsteroidVisualStateType State;
        public float DepletionRatio; // 0 = full, 1 = empty
        public float StateTimer;
    }

    // ============================================================================
    // Material Property Override Components
    // ============================================================================

    /// <summary>
    /// Material property overrides for Entities Graphics rendering.
    /// </summary>
    public struct MaterialPropertyOverride : IComponentData
    {
        public float4 BaseColor;
        public float4 EmissiveColor;
        public float Alpha;
        public float PulsePhase; // For animated effects

        public static MaterialPropertyOverride Default => new MaterialPropertyOverride
        {
            BaseColor = new float4(1f, 1f, 1f, 1f),
            EmissiveColor = float4.zero,
            Alpha = 1f,
            PulsePhase = 0f
        };
    }

    // ============================================================================
    // Render Density Components
    // ============================================================================

    /// <summary>
    /// Configuration for render density control. Singleton component.
    /// </summary>
    public struct RenderDensityConfig : IComponentData
    {
        /// <summary>Density value from 0.0 (none) to 1.0 (all)</summary>
        public float Density;
        /// <summary>Enable automatic density adjustment based on performance</summary>
        public bool AutoAdjust;

        public static RenderDensityConfig Default => new RenderDensityConfig
        {
            Density = 1f,
            AutoAdjust = false
        };
    }

    /// <summary>
    /// Per-entity sample index for stable render density sampling.
    /// </summary>
    public struct RenderSampleIndex : IComponentData
    {
        public uint Index;
    }

    // ============================================================================
    // Fleet Impostor Components
    // ============================================================================

    /// <summary>
    /// Aggregate data for fleet impostor rendering.
    /// </summary>
    public struct FleetAggregateData : IComponentData
    {
        public float3 Centroid;
        public float Strength;
        public int ShipCount;
        public int FactionId;
    }

    /// <summary>
    /// Fleet icon mesh reference for impostor rendering.
    /// </summary>
    public struct FleetIconMesh : IComponentData
    {
        public int MeshIndex;
        public float Size;
    }

    /// <summary>
    /// Optional fleet volume bubble visualization.
    /// </summary>
    public struct FleetVolumeBubble : IComponentData
    {
        public float Radius;
        public float4 Color;
    }

    /// <summary>
    /// Fleet strength indicator visualization.
    /// </summary>
    public struct FleetStrengthIndicator : IComponentData
    {
        public float NormalizedStrength; // 0-1
        public int IndicatorLevel; // 1-5 bars/pips
    }
}

