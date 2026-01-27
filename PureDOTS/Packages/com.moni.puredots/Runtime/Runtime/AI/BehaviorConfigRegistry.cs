using PureDOTS.Runtime.Telemetry;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Centralized AI behavior knobs shared across games. Values are deterministic and data-driven.
    /// </summary>
    public struct BehaviorConfigRegistry : IComponentData
    {
        public MindBehaviorConfig Mind;
        public GatherDeliverBehaviorConfig GatherDeliver;
        public CarrierBehaviorConfig Carrier;
        public HazardDodgeBehaviorConfig HazardDodge;
        public MovementBehaviorConfig Movement;
    }

    public struct MindBehaviorConfig
    {
        public int MindCadenceTicks;
        public int AggregateCadenceTicks;
        public int GatherBehaviorBudgetTicks;
        public int HazardBehaviorBudgetTicks;
        public int GoalChurnWindowTicks;
        public int GoalChurnMaxChanges;
    }

    public struct GatherDeliverBehaviorConfig
    {
        public float DefaultGatherRatePerSecond;
        public float CarryCapacityOverride;
        public float ReturnThresholdPercent;
        public float StorehouseSearchRadius;
        public float DropoffCooldownSeconds;
    }

    public struct CarrierBehaviorConfig
    {
        public int DepositCadenceTicks;
        public float StorehouseBufferRatio;
        public int CarrierIdleTimeoutTicks;
    }

    public struct HazardDodgeBehaviorConfig
    {
        public int RaycastCooldownTicks;
        public int SampleCount;
        public float HighUrgencyThreshold;
        public int OscillationWindowTicks;
        public int OscillationMaxTransitions;
        public int DodgeDistanceTargetMm;
    }

    public struct MovementBehaviorConfig
    {
        public float ArrivalDistance;
        public float AvoidanceBlendWeight;
        public float ThrottleRampSeconds;
    }

    public static class BehaviorConfigDefaults
    {
        public static BehaviorConfigRegistry Create()
        {
            return new BehaviorConfigRegistry
            {
                Mind = new MindBehaviorConfig
                {
                    MindCadenceTicks = 5,
                    AggregateCadenceTicks = 30,
                    GatherBehaviorBudgetTicks = 3,
                    HazardBehaviorBudgetTicks = 2,
                    GoalChurnWindowTicks = 60,
                    GoalChurnMaxChanges = 2
                },
                GatherDeliver = new GatherDeliverBehaviorConfig
                {
                    DefaultGatherRatePerSecond = 8f,
                    CarryCapacityOverride = 0f,
                    ReturnThresholdPercent = 0.95f,
                    StorehouseSearchRadius = 250f,
                    DropoffCooldownSeconds = 1f
                },
                Carrier = new CarrierBehaviorConfig
                {
                    DepositCadenceTicks = 3,
                    StorehouseBufferRatio = 0.1f,
                    CarrierIdleTimeoutTicks = 600
                },
                HazardDodge = new HazardDodgeBehaviorConfig
                {
                    RaycastCooldownTicks = 2,
                    SampleCount = 5,
                    HighUrgencyThreshold = 0.75f,
                    OscillationWindowTicks = 90,
                    OscillationMaxTransitions = 6,
                    DodgeDistanceTargetMm = 5000
                },
                Movement = new MovementBehaviorConfig
                {
                    ArrivalDistance = 2f,
                    AvoidanceBlendWeight = 0.35f,
                    ThrottleRampSeconds = 0.5f
                }
            };
        }
    }

    public struct BehaviorScenarioOverride : IComponentData
    {
        public BehaviorScenarioMind Mind;
        public BehaviorScenarioGather GatherDeliver;
        public BehaviorScenarioCarrier Carrier;
        public BehaviorScenarioHazard HazardDodge;
        public BehaviorScenarioMovement Movement;
        public BehaviorScenarioTelemetry Telemetry;

        public static BehaviorScenarioOverride CreateSentinel()
        {
            return new BehaviorScenarioOverride
            {
                Mind = BehaviorScenarioMind.CreateSentinel(),
                GatherDeliver = BehaviorScenarioGather.CreateSentinel(),
                Carrier = BehaviorScenarioCarrier.CreateSentinel(),
                HazardDodge = BehaviorScenarioHazard.CreateSentinel(),
                Movement = BehaviorScenarioMovement.CreateSentinel(),
                Telemetry = BehaviorScenarioTelemetry.CreateSentinel()
            };
        }
    }

    public struct BehaviorScenarioMind
    {
        public int MindCadenceTicks;
        public int AggregateCadenceTicks;
        public int GatherBehaviorBudgetTicks;
        public int HazardBehaviorBudgetTicks;
        public int GoalChurnWindowTicks;
        public int GoalChurnMaxChanges;

        public static BehaviorScenarioMind CreateSentinel()
        {
            return new BehaviorScenarioMind
            {
                MindCadenceTicks = -1,
                AggregateCadenceTicks = -1,
                GatherBehaviorBudgetTicks = -1,
                HazardBehaviorBudgetTicks = -1,
                GoalChurnWindowTicks = -1,
                GoalChurnMaxChanges = -1
            };
        }
    }

    public struct BehaviorScenarioGather
    {
        public float DefaultGatherRatePerSecond;
        public float CarryCapacityOverride;
        public float ReturnThresholdPercent;
        public float StorehouseSearchRadius;
        public float DropoffCooldownSeconds;

        public static BehaviorScenarioGather CreateSentinel()
        {
            return new BehaviorScenarioGather
            {
                DefaultGatherRatePerSecond = -1f,
                CarryCapacityOverride = -1f,
                ReturnThresholdPercent = -1f,
                StorehouseSearchRadius = -1f,
                DropoffCooldownSeconds = -1f
            };
        }
    }

    public struct BehaviorScenarioCarrier
    {
        public int DepositCadenceTicks;
        public float StorehouseBufferRatio;
        public int CarrierIdleTimeoutTicks;

        public static BehaviorScenarioCarrier CreateSentinel()
        {
            return new BehaviorScenarioCarrier
            {
                DepositCadenceTicks = -1,
                StorehouseBufferRatio = -1f,
                CarrierIdleTimeoutTicks = -1
            };
        }
    }

    public struct BehaviorScenarioHazard
    {
        public int RaycastCooldownTicks;
        public int SampleCount;
        public float HighUrgencyThreshold;
        public int OscillationWindowTicks;
        public int OscillationMaxTransitions;
        public int DodgeDistanceTargetMm;

        public static BehaviorScenarioHazard CreateSentinel()
        {
            return new BehaviorScenarioHazard
            {
                RaycastCooldownTicks = -1,
                SampleCount = -1,
                HighUrgencyThreshold = -1f,
                OscillationWindowTicks = -1,
                OscillationMaxTransitions = -1,
                DodgeDistanceTargetMm = -1
            };
        }
    }

    public struct BehaviorScenarioMovement
    {
        public float ArrivalDistance;
        public float AvoidanceBlendWeight;
        public float ThrottleRampSeconds;

        public static BehaviorScenarioMovement CreateSentinel()
        {
            return new BehaviorScenarioMovement
            {
                ArrivalDistance = -1f,
                AvoidanceBlendWeight = -1f,
                ThrottleRampSeconds = -1f
            };
        }
    }

    public struct BehaviorScenarioTelemetry
    {
        public int AggregateCadenceTicks;

        public static BehaviorScenarioTelemetry CreateSentinel()
        {
            return new BehaviorScenarioTelemetry
            {
                AggregateCadenceTicks = -1
            };
        }
    }

    public struct BehaviorScenarioOverrideComponent : IComponentData
    {
        public BehaviorScenarioOverride Value;
    }

    public static class BehaviorConfigOverrideUtility
    {
        public static void Apply(ref BehaviorConfigRegistry registry, ref BehaviorTelemetryConfig telemetry, in BehaviorScenarioOverride overrides)
        {
            OverrideInt(ref registry.Mind.MindCadenceTicks, overrides.Mind.MindCadenceTicks);
            OverrideInt(ref registry.Mind.AggregateCadenceTicks, overrides.Mind.AggregateCadenceTicks);
            OverrideInt(ref registry.Mind.GatherBehaviorBudgetTicks, overrides.Mind.GatherBehaviorBudgetTicks);
            OverrideInt(ref registry.Mind.HazardBehaviorBudgetTicks, overrides.Mind.HazardBehaviorBudgetTicks);
            OverrideInt(ref registry.Mind.GoalChurnWindowTicks, overrides.Mind.GoalChurnWindowTicks);
            OverrideInt(ref registry.Mind.GoalChurnMaxChanges, overrides.Mind.GoalChurnMaxChanges);

            OverrideFloat(ref registry.GatherDeliver.DefaultGatherRatePerSecond, overrides.GatherDeliver.DefaultGatherRatePerSecond);
            OverrideFloat(ref registry.GatherDeliver.CarryCapacityOverride, overrides.GatherDeliver.CarryCapacityOverride);
            OverrideFloat(ref registry.GatherDeliver.ReturnThresholdPercent, overrides.GatherDeliver.ReturnThresholdPercent);
            OverrideFloat(ref registry.GatherDeliver.StorehouseSearchRadius, overrides.GatherDeliver.StorehouseSearchRadius);
            OverrideFloat(ref registry.GatherDeliver.DropoffCooldownSeconds, overrides.GatherDeliver.DropoffCooldownSeconds);

            OverrideInt(ref registry.Carrier.DepositCadenceTicks, overrides.Carrier.DepositCadenceTicks);
            OverrideFloat(ref registry.Carrier.StorehouseBufferRatio, overrides.Carrier.StorehouseBufferRatio);
            OverrideInt(ref registry.Carrier.CarrierIdleTimeoutTicks, overrides.Carrier.CarrierIdleTimeoutTicks);

            OverrideInt(ref registry.HazardDodge.RaycastCooldownTicks, overrides.HazardDodge.RaycastCooldownTicks);
            OverrideInt(ref registry.HazardDodge.SampleCount, overrides.HazardDodge.SampleCount);
            OverrideFloat(ref registry.HazardDodge.HighUrgencyThreshold, overrides.HazardDodge.HighUrgencyThreshold);
            OverrideInt(ref registry.HazardDodge.OscillationWindowTicks, overrides.HazardDodge.OscillationWindowTicks);
            OverrideInt(ref registry.HazardDodge.OscillationMaxTransitions, overrides.HazardDodge.OscillationMaxTransitions);
            OverrideInt(ref registry.HazardDodge.DodgeDistanceTargetMm, overrides.HazardDodge.DodgeDistanceTargetMm);

            OverrideFloat(ref registry.Movement.ArrivalDistance, overrides.Movement.ArrivalDistance);
            OverrideFloat(ref registry.Movement.AvoidanceBlendWeight, overrides.Movement.AvoidanceBlendWeight);
            OverrideFloat(ref registry.Movement.ThrottleRampSeconds, overrides.Movement.ThrottleRampSeconds);

            OverrideInt(ref telemetry.AggregateCadenceTicks, overrides.Telemetry.AggregateCadenceTicks);
        }

        private static void OverrideInt(ref int target, int value)
        {
            if (value >= 0)
            {
                target = value;
            }
        }

        private static void OverrideFloat(ref float target, float value)
        {
            if (!float.IsNaN(value) && value >= 0f)
            {
                target = value;
            }
        }
    }
}
