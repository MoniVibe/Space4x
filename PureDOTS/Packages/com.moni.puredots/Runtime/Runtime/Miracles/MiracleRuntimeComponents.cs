using PureDOTS.Runtime.Miracles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Types of miracles available in the game.
    /// </summary>
    public enum MiracleType : byte
    {
        None = 0,
        BlessRegion = 1,
        CurseRegion = 2,
        RestoreBiome = 3,
        Fireball = 4,
        Heal = 5,
        Shield = 6,
        Lightning = 7,
        Earthquake = 8,
        Forest = 9,
        Freeze = 10,
        Food = 11,
        Meteor = 12,
        Rain = 13, // Legacy/placeholder
    }

    /// <summary>
    /// How a miracle is cast (instant, sustained, thrown, etc.).
    /// </summary>
    public enum MiracleCastingMode : byte
    {
        Instant = 0,
        Sustained = 1,
        Thrown = 2,
        Area = 3,
    }

    /// <summary>
    /// Current lifecycle state of a miracle instance.
    /// </summary>
    public enum MiracleLifecycleState : byte
    {
        Charging = 0,
        Active = 1,
        Sustaining = 2,
        Cooldown = 3,
        Expired = 4,
    }

    /// <summary>
    /// Definition of a miracle type, containing base properties.
    /// </summary>
    public struct MiracleDefinition : IComponentData
    {
        public MiracleType Type;
        public MiracleCastingMode CastingMode;
        public float BaseRadius;
        public float BaseIntensity;
        public float BaseCost;
        public float SustainedCostPerSecond;
    }

    /// <summary>
    /// Runtime state of an active miracle instance.
    /// Legacy structure - kept for backward compatibility.
    /// </summary>
    public struct MiracleRuntimeState : IComponentData
    {
        public MiracleLifecycleState Lifecycle;
        public float ChargePercent;
        public float CurrentRadius;
        public float CurrentIntensity;
        public float CooldownSecondsRemaining;
        public uint LastCastTick;
        public byte AlignmentDelta;
    }
    
    /// <summary>
    /// New runtime state for per-player/hand miracle tracking.
    /// Replaces/extends MiracleRuntimeState with catalog-based system.
    /// </summary>
    public struct MiracleRuntimeStateNew : IComponentData
    {
        /// <summary>Currently selected miracle ID.</summary>
        public MiracleId SelectedId;
        
        /// <summary>Whether a miracle is currently being activated (0/1).</summary>
        public byte IsActivating;
        
        /// <summary>Whether current activation is sustained mode (0/1).</summary>
        public byte IsSustained;
    }

    /// <summary>
    /// Token representing an active miracle instance.
    /// Used to track and identify miracles in the system.
    /// Legacy structure - kept for backward compatibility.
    /// </summary>
    public struct MiracleTokenLegacy : IComponentData
    {
        public MiracleId Id;
        public MiracleType Type;
        public Entity CasterEntity;
        public Entity ConfigEntity;
    }

    /// <summary>
    /// Identifies the entity that cast a miracle and the hand that performed the cast.
    /// </summary>
    public struct MiracleCaster : IComponentData
    {
        public Entity CasterEntity;
        public Entity HandEntity;
    }

    /// <summary>
    /// Effect component applied by a miracle to a target entity.
    /// Legacy structure - kept for backward compatibility.
    /// </summary>
    public struct MiracleEffect : IComponentData
    {
        public MiracleTokenLegacy Token;
        public float Magnitude;
        public float Duration;
        public float RemainingDuration;
    }
    
    /// <summary>
    /// New generic miracle effect component for catalog-based system.
    /// Applied to effect entities spawned by miracles.
    /// </summary>
    public struct MiracleEffectNew : IComponentData
    {
        /// <summary>Miracle ID that created this effect.</summary>
        public MiracleId Id;
        
        /// <summary>Remaining lifetime in seconds.</summary>
        public float RemainingSeconds;
        
        /// <summary>Intensity multiplier (0-1 or more).</summary>
        public float Intensity;
        
        /// <summary>Origin position of the effect.</summary>
        public float3 Origin;
        
        /// <summary>Radius of the effect.</summary>
        public float Radius;
    }

    /// <summary>
    /// Component for region-scale miracle effects.
    /// Applies to biome/ground tile regions rather than individual entities.
    /// </summary>
    public struct RegionMiracleEffect : IComponentData
    {
        /// <summary>Region entity (biome/ground tile region)</summary>
        public Entity RegionEntity;
        /// <summary>Center position of the effect</summary>
        public float3 CenterPosition;
        /// <summary>Radius of the effect</summary>
        public float Radius;
        /// <summary>Miracle type</summary>
        public MiracleType Type;
        /// <summary>Intensity (0-1)</summary>
        public float Intensity;
        /// <summary>Total duration</summary>
        public float Duration;
        /// <summary>Remaining duration</summary>
        public float RemainingDuration;
    }

    /// <summary>
    /// Defines an available miracle slot on a caster, including prefab/config binding.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MiracleSlotDefinition : IBufferElementData
    {
        public byte SlotIndex;
        public MiracleType Type;
        public Entity MiraclePrefab;
        public Entity ConfigEntity;
    }

    /// <summary>
    /// Event raised by input/presentation to trigger a miracle release.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MiracleReleaseEvent : IBufferElementData
    {
        public MiracleType Type;
        public float3 Position;
        public float3 Normal;
        public Entity TargetEntity;
        public float3 Direction;
        public float Impulse;
        public Entity ConfigEntity;
    }

    /// <summary>
    /// Simple request struct designers can enqueue via authoring triggers to force a miracle spawn for preview.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MiracleDesignerTrigger : IBufferElementData
    {
        public FixedString64Bytes DescriptorKey;
        public float3 Position;
        public MiracleType Type;
    }

    /// <summary>
    /// Component used alongside the MiracleDesignerTrigger buffer to identify the source.
    /// </summary>
    public struct MiracleDesignerTriggerSource : IComponentData
    {
        public Entity ProfileEntity;
        public MiracleType Type;
        public float3 Offset;
    }
    
    /// <summary>
    /// Per-miracle cooldown state for a given player/hand.
    /// Tracks cooldown timers and available charges.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct MiracleCooldown : IBufferElementData
    {
        /// <summary>Miracle ID.</summary>
        public MiracleId Id;
        
        /// <summary>Remaining cooldown time in seconds.</summary>
        public float RemainingSeconds;
        
        /// <summary>Available charges (1 for MVP, future multi-charge miracles).</summary>
        public byte ChargesAvailable;
    }
    
    /// <summary>
    /// Activation request from UI/gesture systems into ECS.
    /// Consumed by MiracleActivationSystem.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MiracleActivationRequest : IBufferElementData
    {
        /// <summary>Miracle ID to activate.</summary>
        public MiracleId Id;
        
        /// <summary>Target point in world space.</summary>
        public float3 TargetPoint;
        
        /// <summary>Target radius around the point.</summary>
        public float TargetRadius;
        
        /// <summary>Dispensation mode (Sustained vs Throw).</summary>
        public byte DispenseMode;
        
        /// <summary>Player/hand index (which god/hand).</summary>
        public byte PlayerIndex;
    }
}

