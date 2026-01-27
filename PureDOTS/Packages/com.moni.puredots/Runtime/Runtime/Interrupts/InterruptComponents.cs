using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Interrupts
{
    /// <summary>
    /// Interrupt types for entity behavior changes.
    /// Interrupts are a specialization of Signals - immediate, high-priority events
    /// that directly affect entity behavior rather than broadcasting to area.
    /// </summary>
    public enum InterruptType : byte
    {
        None = 0,

        // Combat interrupts
        UnderAttack = 1,
        TookDamage = 2,
        LostTarget = 3,
        TargetDestroyed = 4,
        WeaponReady = 5,
        OutOfAmmo = 6,

        // Perception interrupts
        NewThreatDetected = 10,
        LostThreat = 11,
        AllyInDanger = 12,
        ResourceSpotted = 13,
        ObjectiveSpotted = 14,
        SmellSignalDetected = 15,
        SoundSignalDetected = 16,
        EMSignalDetected = 17,
        CommsMessageReceived = 18,
        CommsAckReceived = 19,

        // Group/Order interrupts
        NewOrder = 20,
        OrderCancelled = 21,
        ObjectiveChanged = 22,
        GroupFormed = 23,
        GroupDisbanded = 24,
        LeaderChanged = 25,

        // State interrupts
        LowHealth = 30,
        LowResources = 31,
        StatusEffectApplied = 32,
        StatusEffectRemoved = 33,
        AbilityReady = 34,
        AbilityFailed = 35,
        LieDetected = 36,
        IdentityExposed = 37,

        // Infiltration interrupts
        InfiltrationExposed = 60,
        InfiltrationExtractionStarted = 61,
        InfiltrationExtractionCompleted = 62,
        InfiltrationExtractionFailed = 63,
        IntelGathered = 64,

        // Custom interrupts (for game-specific extensions)
        Custom0 = 100,
        Custom1 = 101,
        Custom2 = 102,
        Custom3 = 103,
        Custom4 = 104,
        Custom5 = 105,
        Custom6 = 106,
        Custom7 = 107
    }

    /// <summary>
    /// Interrupt priority (higher = more urgent).
    /// </summary>
    public enum InterruptPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3,
        Critical = 4
    }

    /// <summary>
    /// Single interrupt entry in InterruptBuffer.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct Interrupt : IBufferElementData
    {
        /// <summary>
        /// Type of interrupt.
        /// </summary>
        public InterruptType Type;

        /// <summary>
        /// Priority level.
        /// </summary>
        public InterruptPriority Priority;

        /// <summary>
        /// Entity that caused/emitted this interrupt.
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Tick when interrupt was emitted.
        /// </summary>
        public uint Timestamp;

        /// <summary>
        /// Optional target entity (e.g., attacker, threat, objective).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Optional target position.
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Optional payload data (simple numeric value).
        /// </summary>
        public float PayloadValue;

        /// <summary>
        /// Optional payload ID (for referencing external data).
        /// </summary>
        public FixedString32Bytes PayloadId;

        /// <summary>
        /// Whether this interrupt has been processed.
        /// </summary>
        public byte IsProcessed;
    }

    /// <summary>
    /// Compact intent component for consuming interrupts.
    /// Written by InterruptHandlerSystem, consumed by GOAP/behavior systems.
    /// Phase 1: Simple mode + target.
    /// Phase 2: Extended with parameters, conditions, etc.
    /// </summary>
    public struct EntityIntent : IComponentData
    {
        /// <summary>
        /// Desired high-level behavior mode.
        /// </summary>
        public IntentMode Mode;

        /// <summary>
        /// Optional target entity.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Optional target position.
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Interrupt that triggered this intent.
        /// </summary>
        public InterruptType TriggeringInterrupt;

        /// <summary>
        /// Tick when intent was set.
        /// </summary>
        public uint IntentSetTick;

        /// <summary>
        /// Intent priority (from interrupt priority).
        /// </summary>
        public InterruptPriority Priority;

        /// <summary>
        /// Whether intent is still valid.
        /// </summary>
        public byte IsValid;
    }

    /// <summary>
    /// High-level intent modes for entity behavior.
    /// </summary>
    public enum IntentMode : byte
    {
        Idle = 0,
        MoveTo = 1,
        Attack = 2,
        Flee = 3,
        UseAbility = 4,
        ExecuteOrder = 5,
        Gather = 6,
        Build = 7,
        Defend = 8,
        Patrol = 9,
        Follow = 10,
        Deliver = 11,
        Custom0 = 100,
        Custom1 = 101,
        Custom2 = 102,
        Custom3 = 103
    }

    /// <summary>
    /// Static utility class for emitting interrupts (library functions, not a system).
    /// Systems call these helpers to emit interrupts without needing a separate system.
    /// </summary>
    public static class InterruptUtils
    {
        /// <summary>
        /// Emits an interrupt to an entity's interrupt buffer.
        /// </summary>
        public static void Emit(
            ref DynamicBuffer<Interrupt> interruptBuffer,
            InterruptType type,
            InterruptPriority priority,
            Entity sourceEntity,
            uint currentTick,
            Entity targetEntity = default,
            float3 targetPosition = default,
            float payloadValue = 0f,
            FixedString32Bytes payloadId = default)
        {
            interruptBuffer.Add(new Interrupt
            {
                Type = type,
                Priority = priority,
                SourceEntity = sourceEntity,
                Timestamp = currentTick,
                TargetEntity = targetEntity,
                TargetPosition = targetPosition,
                PayloadValue = payloadValue,
                PayloadId = payloadId,
                IsProcessed = 0
            });
        }

        /// <summary>
        /// Emits a combat interrupt.
        /// </summary>
        public static void EmitCombat(
            ref DynamicBuffer<Interrupt> interruptBuffer,
            InterruptType type,
            Entity attacker,
            Entity target,
            uint currentTick,
            InterruptPriority priority = InterruptPriority.High)
        {
            Emit(ref interruptBuffer, type, priority, attacker, currentTick, target);
        }

        /// <summary>
        /// Emits a perception interrupt.
        /// </summary>
        public static void EmitPerception(
            ref DynamicBuffer<Interrupt> interruptBuffer,
            InterruptType type,
            Entity detector,
            Entity detectedEntity,
            float3 detectedPosition,
            uint currentTick,
            InterruptPriority priority = InterruptPriority.Normal)
        {
            Emit(ref interruptBuffer, type, priority, detector, currentTick, detectedEntity, detectedPosition);
        }

        /// <summary>
        /// Emits a group/order interrupt.
        /// </summary>
        public static void EmitOrder(
            ref DynamicBuffer<Interrupt> interruptBuffer,
            InterruptType type,
            Entity groupEntity,
            Entity targetEntity,
            float3 targetPosition,
            uint currentTick,
            InterruptPriority priority = InterruptPriority.Normal)
        {
            Emit(ref interruptBuffer, type, priority, groupEntity, currentTick, targetEntity, targetPosition);
        }
    }
}
