using PureDOTS.Runtime.Combat;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime.Breakables
{
    [System.Flags]
    public enum ShipCapabilityFlags : uint
    {
        None = 0u,
        Command = 1u << 0,
        Power = 1u << 1,
        Propulsion = 1u << 2,
        Steering = 1u << 3,
        Sensors = 1u << 4,
        Weapons = 1u << 5,
        Hangar = 1u << 6,
        Cargo = 1u << 7,
        LifeSupport = 1u << 8,
        Ftl = 1u << 9,
        Towable = 1u << 10
    }

    public enum Space4XBreakMode : byte
    {
        Threshold = 0,
        Implosion = 1,
        Explosion = 2
    }

    public struct ShipBreakPieceDef
    {
        public float3 LocalOffset;
        public float MassFraction;
        public int AttachmentGroup;
        public byte ColliderPreset;
        public byte VisualPreset;
        public byte IsCore;
        public ushort PieceId;
        public ShipCapabilityFlags ProvidesFlags;
        public float ThrustContribution;
        public float PowerGeneration;
        public float SensorRangeMultiplier;
        public byte WeaponHardpointCount;
    }

    public struct ShipBreakEdgeDef
    {
        public ushort PieceA;
        public ushort PieceB;
        public float BreakDamageThreshold;
        public float BreakInstabilityThreshold;
        public Space4XBreakMode BreakMode;
        public byte IsCriticalPath;
    }

    public struct ShipBreakProfileBlob
    {
        public float BreakDelaySeconds;
        public byte MaxFragments;
        public float MinFragmentMass;
        public ShipCapabilityFlags AliveRequired;
        public ShipCapabilityFlags MobileRequiredAny;
        public ShipCapabilityFlags CombatRequiredAny;
        public ShipCapabilityFlags FtlRequiredAll;
        public BlobArray<ShipBreakPieceDef> Pieces;
        public BlobArray<ShipBreakEdgeDef> Edges;
    }

    public struct Space4XBreakableRoot : IComponentData
    {
        public BlobAssetReference<ShipBreakProfileBlob> Profile;
        public uint BreakTick;
        public byte IsBroken;
        public float Damage;
        public float Instability;
        public byte Reserved0;
        public ushort Reserved1;
    }

    public struct Space4XBreakablePiece : IComponentData
    {
        public Entity Root;
        public ushort PieceIndex;
        public float3 LocalOffset;
        public int AttachmentGroup;
    }

    public struct Space4XBreakablePieceState : IComponentData
    {
        public float Damage01;
        public float Instability01;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XBreakableEdgeState : IBufferElementData
    {
        public int EdgeIndex;
        public uint BrokenTick;
        public byte IsBroken;
        public byte Reserved0;
        public ushort Reserved1;
    }

    public struct Space4XBreakableDamagePulse : IComponentData
    {
        public float DamageAmount;
        public float DelaySeconds;
        public uint TriggerTick;
        public byte Fired;
        public DamageType DamageType;
        public DamageFlags DamageFlags;
    }

    public struct Space4XBreakableFragmentRoot : IComponentData
    {
        public Entity SourceRoot;
        public int AttachmentGroup;
    }

    public struct Space4XShipCapabilityState : IComponentData
    {
        public ShipCapabilityFlags ProvidesFlags;
        public float MaxThrust;
        public float PowerGeneration;
        public float SensorRangeMultiplier;
        public byte WeaponHardpointCount;
        public byte IsAlive;
        public byte IsMobile;
        public byte IsCombatCapable;
        public byte IsFtlCapable;
    }
}
