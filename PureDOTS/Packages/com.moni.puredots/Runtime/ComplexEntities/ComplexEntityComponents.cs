using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.ComplexEntities
{
    /// <summary>
    /// Entity type classification for complex entities.
    /// </summary>
    public enum ComplexEntityType : byte
    {
        Carrier = 0,
        Guild = 1,
        Colony = 2,
        Station = 3,
        Fleet = 4,
        Organization = 5,
        Other = 255
    }

    /// <summary>
    /// Core identity for complex entities. Every complex entity has this.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexEntityIdentity : IComponentData
    {
        /// <summary>
        /// Stable identifier for lookups and persistence.
        /// Keep this purely numeric for 10M-scale memory.
        /// </summary>
        public ulong StableId;

        /// <summary>Creation tick (for age calculations).</summary>
        public uint CreationTick;

        /// <summary>Entity type classification.</summary>
        public ComplexEntityType EntityType;

        /// <summary>Reserved for future use.</summary>
        public byte Reserved0;

        /// <summary>Reserved for future use.</summary>
        public ushort Reserved1;
    }

    /// <summary>
    /// Packed core axes for hot-path reads (the 10M entity-of-record representation).
    ///
    /// This is intentionally quantized / fixed-size:
    /// - Avoids float drift for aggregates (use fixed-point ints)
    /// - Keeps core chunks dense and cache-friendly
    ///
    /// Operational / narrative expansions should add additional components rather than bloating this.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexEntityCoreAxes : IComponentData
    {
        // Spatial (16 bytes)
        /// <summary>Coarse spatial cell index in world space (game-defined cell size).</summary>
        public int3 Cell;
        /// <summary>Quantized local position within cell (0..65535 maps to cell size).</summary>
        public ushort LocalX;
        public ushort LocalY;

        // Motion (8 bytes)
        /// <summary>Quantized planar velocity (game-defined scale, e.g. 0.1 m/s per unit).</summary>
        public short VelX;
        public short VelY;
        /// <summary>Quantized heading (0..65535 maps to 0..2π).</summary>
        public ushort HeadingQ;
        /// <summary>Quantized health (0..65535 maps to 0..1).</summary>
        public ushort HealthQ;

        // Aggregates / capacities (12 bytes, fixed-point)
        /// <summary>Total mass (fixed-point, game-defined scale; includes crew mass when collapsed).</summary>
        public uint MassQ;
        /// <summary>Total capacity (fixed-point, game-defined scale; includes crew capacity when collapsed).</summary>
        public uint CapacityQ;
        /// <summary>Current load (fixed-point, game-defined scale).</summary>
        public uint LoadQ;

        // Flags & small aggregates (8 bytes)
        /// <summary>Bitfield flags: operational active, narrative active, etc.</summary>
        public uint Flags;
        /// <summary>Crew count aggregate for cold state (optional; narrative roster is externalized).</summary>
        public ushort CrewCount;
        public ushort Reserved0;
    }

    /// <summary>
    /// Flags bitfield constants for ComplexEntityCoreAxes.Flags.
    /// </summary>
    public static class ComplexEntityFlags
    {
        public const uint OperationalActive = 1 << 0;
        public const uint NarrativeActive = 1 << 1;
        public const uint CrewLoaded = 1 << 2;
        public const uint SparseAxesDirty = 1 << 3;
    }

    /// <summary>
    /// Sparse axes buffer for rare/optional axes that don't fit in core.
    /// Only populated for operational entities.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct ComplexEntitySparseAxesBuffer : IBufferElementData
    {
        /// <summary>Axis identifier (hash or enum).</summary>
        public uint AxisId;
        
        /// <summary>Axis value (interpreted based on AxisId).</summary>
        public float Value;
        
        /// <summary>Optional second value for 2D axes.</summary>
        public float Value2;
    }

    /// <summary>
    /// Operational state component (enableable). Activated when entity enters active bubble,
    /// player focus, combat, or docking operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexEntityOperationalState : IComponentData, IEnableableComponent
    {
        /// <summary>Current operational mode (patrol, combat, docked, etc.).</summary>
        public byte OperationalMode;
        
        /// <summary>Target entity (if applicable).</summary>
        public Entity TargetEntity;
        
        /// <summary>Operational state flags.</summary>
        public ushort StateFlags;
        
        /// <summary>Last update tick for operational state.</summary>
        public uint LastUpdateTick;
        
        /// <summary>Reserved for future expansion.</summary>
        public uint Reserved0;
    }

    /// <summary>
    /// Crew roster blob asset structure.
    /// </summary>
    public struct CrewRosterBlob
    {
        public BlobArray<CrewMemberBlob> Members;
        public BlobArray<CrewRoleBlob> Roles;
        public BlobArray<CrewRelationshipBlob> Relationships;
    }

    /// <summary>
    /// Individual crew member data in blob.
    /// </summary>
    public struct CrewMemberBlob
    {
        public FixedString64Bytes MemberId;
        public FixedString64Bytes Name;
        public byte RoleIndex;
        public byte TraitFlags;
        public float InfluenceWeight;
        public float Loyalty;
    }

    /// <summary>
    /// Crew role definition in blob.
    /// </summary>
    public struct CrewRoleBlob
    {
        public FixedString64Bytes RoleId;
        public FixedString64Bytes DisplayName;
        public byte Priority;
        public float BaseInfluence;
    }

    /// <summary>
    /// Crew relationship data in blob.
    /// </summary>
    public struct CrewRelationshipBlob
    {
        public ushort MemberIndexA;
        public ushort MemberIndexB;
        public float RelationshipValue; // -1 to +1
        public byte RelationshipType;
    }

    /// <summary>
    /// Handle to crew roster in pooled blob store.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexEntityCrewHandle : IComponentData
    {
        /// <summary>Stable ID of the owning entity (used as pool key).</summary>
        public ulong OwnerStableId;

        /// <summary>Reference to crew roster blob asset.</summary>
        public BlobAssetReference<CrewRosterBlob> Roster;
        
        /// <summary>Last update tick for crew roster.</summary>
        public uint LastUpdateTick;
        
        /// <summary>Crew count (cached aggregate).</summary>
        public ushort CrewCount;
        
        /// <summary>Reserved for future use.</summary>
        public ushort Reserved0;
    }

    /// <summary>
    /// Narrative detail component (enableable). Activated when entity is inspected
    /// or participates in narrative events.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexEntityNarrativeDetail : IComponentData, IEnableableComponent
    {
        /// <summary>Reference to narrative blob asset (if applicable).</summary>
        public BlobAssetReference<ComplexEntityNarrativeBlob> NarrativeBlob;
        
        /// <summary>Narrative state flags.</summary>
        public uint NarrativeFlags;
        
        /// <summary>Last narrative event tick.</summary>
        public uint LastNarrativeTick;
    }

    /// <summary>
    /// Narrative blob asset structure.
    /// </summary>
    public struct ComplexEntityNarrativeBlob
    {
        public BlobArray<FixedString128Bytes> HistoryEntries;
        public BlobArray<FixedString64Bytes> KnownFacts;
        public BlobArray<FixedString64Bytes> RelationshipKeys;
    }

    // ========== Activation Tags ==========

    /// <summary>
    /// Tag component indicating entity is within active bubble (viewport/frustum).
    /// </summary>
    public struct ActiveBubbleTag : IComponentData { }

    /// <summary>
    /// Tag component indicating entity has player focus (selected/inspected).
    /// </summary>
    public struct FocusTargetTag : IComponentData { }

    /// <summary>
    /// Tag component indicating entity is combat-ready or engaged in combat.
    /// </summary>
    public struct CombatReadyTag : IComponentData { }

    /// <summary>
    /// Tag component indicating entity is performing docking operations.
    /// </summary>
    public struct DockingActiveTag : IComponentData { }

    /// <summary>
    /// Tag component indicating entity inspection is requested (UI detail panel).
    /// </summary>
    public struct InspectionRequest : IComponentData { }
}
