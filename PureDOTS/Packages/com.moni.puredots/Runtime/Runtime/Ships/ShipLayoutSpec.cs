using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Ships
{
    /// <summary>
    /// 8-way facing enumeration for directional damage.
    /// </summary>
    public enum Facing8 : byte
    {
        Fore = 0,
        ForeStar = 1,
        Starboard = 2,
        AftStar = 3,
        Aft = 4,
        AftPort = 5,
        Port = 6,
        ForePort = 7
    }

    /// <summary>
    /// Armor arc specification - defines armor protection for a facing direction.
    /// </summary>
    public struct ArmorArc
    {
        public Facing8 Facing; // Which arc this is
        public float Thickness; // Abstract cm-RHA equivalent
        public float KineticResist; // 0..1 multipliers
        public float EnergyResist;
        public float ExplosiveResist;
    }

    /// <summary>
    /// Shield arc specification - defines shield protection for a facing direction.
    /// </summary>
    public struct ShieldArc
    {
        public Facing8 Facing;
        public float MaxHP; // Per-arc capacity
        public float RegenPerSec; // Regeneration rate
        public float CoverageCos; // Half-angle via dot threshold (cosine of half-angle)
    }

    /// <summary>
    /// Oriented bounding box for module hit detection in local space.
    /// </summary>
    public struct ModuleHitOBB
    {
        public float3 Center; // Local space center
        public float3 Extents; // Half-extents
        public quaternion Rot; // Local rotation
        public ushort ModuleIndex; // Index into ModuleTable
    }

    /// <summary>
    /// Module slot definition - defines a module location and properties.
    /// </summary>
    public struct ModuleSlot
    {
        public FixedString32Bytes Id; // "engine_main", "reactor", "bridge"
        public byte TechTier; // Repair gate (tech level required)
        public byte Criticality; // 0..10 (crew loss / kill switches)
        public BlobArray<ModuleHitOBB> Volumes; // Can be >1 for complex shapes
    }

    /// <summary>
    /// Ship layout blob - defines physical layout for directional damage & module hits.
    /// Baked from authoring, immutable at runtime.
    /// </summary>
    public struct ShipLayoutBlob
    {
        public BlobArray<ArmorArc> Armor; // 6-8 arcs
        public BlobArray<ShieldArc> Shields; // Optional; empty if none
        public BlobArray<ModuleSlot> Modules; // All module slots
    }

    /// <summary>
    /// Reference to a ship layout specification blob.
    /// Entities use this to access their layout data.
    /// </summary>
    public struct ShipLayoutRef : IComponentData
    {
        public BlobAssetReference<ShipLayoutBlob> Blob;
    }
}

