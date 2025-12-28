using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    // ============================================================================
    // Space4X-Specific Presentation Tags
    // ============================================================================
    // These are game-specific tags that don't exist in PureDOTS.
    // PureDOTS provides: RenderLODData, RenderCullable, RenderSampleIndex
    // Space4X adds: entity type tags, visual state, faction colors, etc.

    /// <summary>
    /// Marker component for carrier entities in the presentation layer.
    /// </summary>
    public struct CarrierPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for craft/mining vessel entities in the presentation layer.
    /// </summary>
    public struct CraftPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for strike craft entities in the presentation layer.
    /// </summary>
    public struct StrikeCraftPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for asteroid entities in the presentation layer.
    /// </summary>
    public struct AsteroidPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for individual entities in the presentation layer.
    /// </summary>
    public struct IndividualPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for resource pickup entities in the presentation layer.
    /// </summary>
    public struct ResourcePickupPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for carrier storage markers.
    /// </summary>
    public struct StorageMarkerPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for asteroid resource markers.
    /// </summary>
    public struct ResourceMarkerPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for cargo visuals attached to vessels.
    /// </summary>
    public struct CargoPresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for projectile entities in the presentation layer.
    /// </summary>
    public struct ProjectilePresentationTag : IComponentData { }

    /// <summary>
    /// Marker component for fleet impostor entities (aggregate visualization).
    /// </summary>
    public struct FleetImpostorTag : IComponentData { }

    /// <summary>
    /// Marker component for selected entities.
    /// </summary>
    public struct SelectedTag : IComponentData { }

    /// <summary>
    /// Coarse presentation layers for distance-based culling and icon switching.
    /// </summary>
    public enum PresentationLayerId : byte
    {
        Colony = 0,
        Island = 1,
        Continent = 2,
        Planet = 3,
        Orbital = 4,
        System = 5,
        Galactic = 6
    }

    /// <summary>
    /// Per-entity presentation layer classification.
    /// </summary>
    public struct PresentationLayer : IComponentData
    {
        public PresentationLayerId Value;
    }

    // ============================================================================
    // Faction & Color Components
    // ============================================================================

    /// <summary>
    /// Shared palette for faction tinting without binding gameplay assemblies to presentation-only components.
    /// </summary>
    public static class Space4XFactionColors
    {
        public static readonly float4 Red = new float4(1f, 0.2f, 0.2f, 1f);
        public static readonly float4 Blue = new float4(0.2f, 0.4f, 1f, 1f);
        public static readonly float4 Green = new float4(0.2f, 1f, 0.2f, 1f);
        public static readonly float4 Yellow = new float4(1f, 1f, 0.2f, 1f);
        public static readonly float4 Purple = new float4(0.8f, 0.2f, 1f, 1f);
        public static readonly float4 Orange = new float4(1f, 0.6f, 0.2f, 1f);
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
        public static ResourceTypeColor Ore => new ResourceTypeColor { Value = new float4(0.5f, 0.3f, 0.2f, 1f) };
    }

    /// <summary>
    /// Reference to parent carrier entity for crafts.
    /// </summary>
    public struct ParentCarrier : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// Links a vessel to its cargo visual entity.
    /// </summary>
    public struct CargoVisualLink : IComponentData
    {
        public Entity CargoEntity;
    }

    /// <summary>
    /// Links a cargo visual entity back to its parent vessel.
    /// </summary>
    public struct CargoVisualParent : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// Links an asteroid to its resource marker entity.
    /// </summary>
    public struct ResourceMarkerLink : IComponentData
    {
        public Entity MarkerEntity;
    }

    /// <summary>
    /// Links a resource marker entity back to its parent asteroid.
    /// </summary>
    public struct ResourceMarkerParent : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// Links a carrier to its storage marker entity.
    /// </summary>
    public struct CarrierStorageMarkerLink : IComponentData
    {
        public Entity MarkerEntity;
    }

    /// <summary>
    /// Links a storage marker entity back to its parent carrier.
    /// </summary>
    public struct CarrierStorageMarkerParent : IComponentData
    {
        public Entity Value;
    }

    /// <summary>
    /// Links a resource pickup to its presenter entity.
    /// </summary>
    public struct ResourcePickupPresenterLink : IComponentData
    {
        public Entity PresenterEntity;
    }

    /// <summary>
    /// Tracks intake pulse timing for carrier storage markers.
    /// </summary>
    public struct CarrierIntakePulseState : IComponentData
    {
        public float Timer;
        public float LastTotal;
        public ResourceType LastType;
    }

    /// <summary>
    /// Presentation-only attachment to a presenter entity.
    /// </summary>
    public struct PresentationAttachTo : IComponentData
    {
        public Entity ParentPresenter;
        public float3 LocalOffset;
    }

    /// <summary>
    /// Marks LocalToWorld as authored by presentation systems.
    /// </summary>
    [WriteGroup(typeof(LocalToWorld))]
    public struct PresentationLocalToWorldOverride : IComponentData
    {
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
    /// Visual state for strike craft entities.
    /// </summary>
    public enum StrikeCraftVisualStateType : byte
    {
        Docked = 0,
        FormingUp = 1,
        Approaching = 2,
        Engaging = 3,
        Disengaging = 4,
        Returning = 5
    }

    /// <summary>
    /// Per-strike-craft visual state component.
    /// </summary>
    public struct StrikeCraftVisualState : IComponentData
    {
        public StrikeCraftVisualStateType State;
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

    // ============================================================================
    // Fleet & Aggregate Components (Space4X-specific)
    // ============================================================================

    /// <summary>
    /// Space4X-specific fleet state component.
    /// </summary>
    public struct FleetState : IComponentData
    {
        public int MemberCount;
        public float3 AveragePosition;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public float TotalStrength;
        public float TotalHealth;
        public float TotalCargoCapacity;
    }

    /// <summary>
    /// Space4X-specific fleet render summary component.
    /// </summary>
    public struct FleetRenderSummary : IComponentData
    {
        public int MemberCount;
        public float3 AveragePosition;
        public float3 BoundsCenter;
        public float BoundsRadius;
        public float TotalStrength;
        public float TotalHealth;
        /// <summary>Dominant ship type as numeric index (byte) - presentation layer maps to names/icons</summary>
        public byte DominantShipType;
        public int FactionIndex;
    }

    /// <summary>
    /// Space4X-specific aggregate state component.
    /// </summary>
    public struct AggregateState : IComponentData
    {
        public int MemberCount;
        public float3 AveragePosition;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public float TotalHealth;
        public float AverageMorale;
        public float TotalStrength;
    }

    /// <summary>
    /// Space4X-specific aggregate render summary component.
    /// </summary>
    public struct AggregateRenderSummary : IComponentData
    {
        public int MemberCount;
        public float3 AveragePosition;
        public float3 BoundsCenter;
        public float BoundsRadius;
        public float TotalStrength;
        public float AverageMorale;
    }

    /// <summary>
    /// Space4X-specific aggregate member element buffer.
    /// </summary>
    public struct AggregateMemberElement : IBufferElementData
    {
        public Entity MemberEntity;
        public float StrengthContribution;
        public float Health;
    }

    /// <summary>
    /// Space4X-specific fleet member reference component.
    /// </summary>
    public struct FleetMemberRef : IComponentData
    {
        public Entity FleetEntity;
        public int MemberIndex;
    }

    /// <summary>
    /// Space4X-specific aggregate membership component.
    /// </summary>
    public struct AggregateMembership : IComponentData
    {
        public Entity AggregateEntity;
        public int MemberIndex;
    }

    /// <summary>
    /// Tag component for fleet aggregate entities.
    /// </summary>
    public struct FleetTag : IComponentData { }
}
