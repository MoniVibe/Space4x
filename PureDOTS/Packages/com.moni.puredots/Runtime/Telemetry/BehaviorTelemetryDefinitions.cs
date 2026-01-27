using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    public enum BehaviorId : ushort
    {
        None = 0,
        HazardDodge = 1,
        GatherDeliver = 2,
        VillagerCore = 3,
        FleetCore = 4,
        ModuleIntegrity = 5,
        ShieldEnvelope = 6,
        LimbVitality = 7,
        ProjectileDamage = 8,
        MoraleState = 9
    }

    public enum BehaviorMetricId : ushort
    {
        HazardRaycastHits = 1,
        HazardAvoidanceTransitions = 2,
        HazardDodgeDistanceMm = 3,
        GatherMinedMilli = 100,
        GatherDepositedMilli = 101,
        GatherCarrierCargoMilli = 102,
        VillagerNeedMaxMilli = 200,
        VillagerFocusCurrentMilli = 201,
        VillagerFleeEvents = 202,
        FleetCohesionMilli = 300,
        FleetMoraleMilli = 301,
        FleetStrikeCraftLoadMilli = 302,
        ModuleHullMilli = 400,
        ModuleArmorMilli = 401,
        ModuleEfficiencyMilli = 402,
        ModuleDamageEvents = 403,
        ShieldFrontLoadMilli = 500,
        ShieldRearLoadMilli = 501,
        ShieldBubbleStrengthMilli = 502,
        LimbHealthMilli = 600,
        LimbStaminaMilli = 601,
        LimbManaMilli = 602,
        ProjectileShotsFired = 700,
        ProjectileShotsHit = 701,
        ProjectileDamageMilli = 702,
        MoraleCurrentMilli = 800,
        MoraleDeltaMilli = 801,
    }

    public enum BehaviorInvariantId : ushort
    {
        HazardNoOscillation = 1,
        GatherConservation = 100,
        VillagerFocusNonNegative = 200,
        VillagerNeedClamped = 201,
        FleetCohesionBounds = 300,
        ModuleHealthNonNegative = 400,
        ShieldLoadWithinBounds = 500,
        LimbVitalityNonNegative = 600,
        ProjectileDamageNonNegative = 700,
        MoraleWithinBounds = 800
    }

    public enum BehaviorTelemetryRecordKind : byte
    {
        Metric = 0,
        Invariant = 1
    }

    /// <summary>
    /// Global configuration for behavior telemetry aggregation cadence.
    /// </summary>
    public struct BehaviorTelemetryConfig : IComponentData
    {
        public int AggregateCadenceTicks;
    }

    /// <summary>
    /// Marker singleton for the telemetry state entity.
    /// </summary>
    public struct BehaviorTelemetryState : IComponentData { }

    /// <summary>
    /// Per-agent hazard avoidance counters (interval values reset each cadence).
    /// </summary>
    public struct HazardDodgeTelemetry : IComponentData
    {
        public uint RaycastHitsInterval;
        public uint AvoidanceTransitionsInterval;
        public int DodgeDistanceMmInterval;
        public uint HighUrgencyTicksInterval;
        public byte WasAvoidingLastTick;
    }

    /// <summary>
    /// Per-agent gather/deliver counters (mixed interval + snapshot fields).
    /// </summary>
    public struct GatherDeliverTelemetry : IComponentData
    {
        public int MinedAmountMilliInterval;
        public int DepositedAmountMilliInterval;
        public int CarrierCargoMilliSnapshot;
        public uint StuckTicksInterval;
    }

    /// <summary>
    /// Per-agent villager telemetry accumulation (needs/focus/flee).
    /// </summary>
    public struct VillagerCoreTelemetry : IComponentData
    {
        public float NeedAccumulator;
        public uint NeedSampleCount;
        public float FocusSnapshot;
        public uint FleeEvents;
        public byte WasFleeingLastTick;
        public byte FocusNegativeDetected;
        public byte NeedExceededDetected;
    }

    /// <summary>
    /// Per-fleet telemetry accumulation for cohesion/morale/strike craft load.
    /// </summary>
    public struct FleetCoreTelemetry : IComponentData
    {
        public float CohesionAccumulator;
        public uint CohesionSamples;
        public float MoraleAccumulator;
        public uint MoraleSamples;
        public float StrikeCraftLoadAccumulator;
        public uint StrikeCraftSamples;
        public byte CohesionOutOfRange;
    }

    /// <summary>
    /// Per-module hull/armor/efficiency sampling.
    /// </summary>
    public struct ModuleHealthTelemetry : IComponentData
    {
        public float HullAccumulator;
        public uint HullSamples;
        public float ArmorAccumulator;
        public uint ArmorSamples;
        public float EfficiencyAccumulator;
        public uint EfficiencySamples;
        public uint DamageEventsInterval;
        public byte HullBelowZero;
        public byte ArmorBelowZero;
    }

    /// <summary>
    /// Per-entity shield load telemetry (front/rear/bubble).
    /// </summary>
    public struct ShieldEnvelopeTelemetry : IComponentData
    {
        public float FrontLoadAccumulator;
        public float RearLoadAccumulator;
        public float BubbleStrengthAccumulator;
        public uint Samples;
        public byte OverCapacityDetected;
    }

    /// <summary>
    /// Limb-style telemetry for organic entities (health/stamina/mana).
    /// </summary>
    public struct LimbVitalityTelemetry : IComponentData
    {
        public float HealthAccumulator;
        public float StaminaAccumulator;
        public float ManaAccumulator;
        public uint Samples;
        public byte HealthNegativeDetected;
        public byte StaminaNegativeDetected;
        public byte ManaNegativeDetected;
    }

    /// <summary>
    /// Projectile firing telemetry (counts + damage).
    /// </summary>
    public struct ProjectileDamageTelemetry : IComponentData
    {
        public uint ShotsFiredInterval;
        public uint ShotsHitInterval;
        public float DamageMilliAccumulator;
        public byte NegativeDamageDetected;
    }

    /// <summary>
    /// Per-entity morale telemetry (current value + deltas).
    /// </summary>
    public struct MoraleTelemetry : IComponentData
    {
        public float MoraleAccumulator;
        public uint MoraleSamples;
        public float DeltaAccumulator;
        public byte OutOfBoundsDetected;
    }

    /// <summary>
    /// Aggregated output record consumed by headless loggers / scenario reports.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct BehaviorTelemetryRecord : IBufferElementData
    {
        public uint Tick;
        public BehaviorId Behavior;
        public BehaviorTelemetryRecordKind Kind;
        public ushort MetricOrInvariantId;
        public long ValueA;
        public long ValueB;
        public byte Passed;
    }

    public static class BehaviorTelemetryMath
    {
        public static int ToMilli(float value) => (int)Math.Round(value * 1000f);
    }
}
