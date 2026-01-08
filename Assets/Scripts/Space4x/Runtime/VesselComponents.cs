using PureDOTS.Runtime.Agency;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    /// <summary>
    /// Movement component for vessels (mining vessels, carriers, etc.)
    /// Similar to VillagerMovement but designed for ships
    /// </summary>
    public struct VesselMovement : IComponentData
    {
        public float3 Velocity;
        public float BaseSpeed;
        public float CurrentSpeed;
        public float Acceleration;
        public float Deceleration;
        public float TurnSpeed;
        public float SlowdownDistance;
        public float ArrivalDistance;
        public quaternion DesiredRotation;
        public byte IsMoving;
        public uint LastMoveTick;
    }

    public struct VesselTurnRateState : IComponentData
    {
        public float LastAngularSpeed;
        public byte Initialized;
    }

    /// <summary>
    /// AI state for vessels - tracks targets and goals
    /// </summary>
    public struct VesselAIState : IComponentData
    {
        public enum Goal : byte
        {
            None = 0,
            Mining = 1,
            Returning = 2,
            Idle = 3,
            Formation = 4,
            Patrol = 5,
            Escort = 6
        }

        public enum State : byte
        {
            Idle = 0,
            MovingToTarget = 1,
            Mining = 2,
            Returning = 3
        }

        public State CurrentState;
        public Goal CurrentGoal;
        public Entity TargetEntity; // Asteroid or carrier to target
        public float3 TargetPosition;
        public float StateTimer;
        public uint StateStartTick;
    }

    /// <summary>
    /// Binding that maps shared AI action indices to vessel goals.
    /// Similar to VillagerAIUtilityBinding but for vessels.
    /// </summary>
    public struct VesselAIUtilityBinding : IComponentData
    {
        public FixedList32Bytes<VesselAIState.Goal> Goals;
    }

    /// <summary>
    /// Links a vessel to its pilot entity (individual profile owner).
    /// </summary>
    public struct VesselPilotLink : IComponentData
    {
        public Entity Pilot;
    }

    /// <summary>
    /// Default control claim values for a pilot/operator controlling this craft.
    /// </summary>
    public struct PilotControlClaimConfig : IComponentData
    {
        public AgencyDomain Domains;
        public float Pressure;
        public float Legitimacy;
        public float Hostility;
        public float Consent;

        public static PilotControlClaimConfig Default => new PilotControlClaimConfig
        {
            Domains = AgencyDomain.Movement | AgencyDomain.Combat | AgencyDomain.Sensors | AgencyDomain.Communications,
            Pressure = 1.2f,
            Legitimacy = 0.8f,
            Hostility = 0.05f,
            Consent = 0.6f
        };
    }

    /// <summary>
    /// Mobility capabilities derived from engine type and build.
    /// </summary>
    public enum VesselThrustMode : byte
    {
        ForwardOnly = 0,
        Omnidirectional = 1,
        Vectored = 2
    }

    /// <summary>
    /// Movement capabilities for a vessel (strafe, reverse thrust, kiting).
    /// </summary>
    public struct VesselMobilityProfile : IComponentData
    {
        public VesselThrustMode ThrustMode;
        public float ReverseSpeedMultiplier;
        public float StrafeSpeedMultiplier;
        public byte AllowKiting;
        public float TurnMultiplier;

        public static VesselMobilityProfile Default => new VesselMobilityProfile
        {
            ThrustMode = VesselThrustMode.ForwardOnly,
            ReverseSpeedMultiplier = 0f,
            StrafeSpeedMultiplier = 0f,
            AllowKiting = 0,
            TurnMultiplier = 1f
        };
    }

    /// <summary>
    /// Physical characteristics for collision response and mass scaling.
    /// </summary>
    public struct VesselPhysicalProperties : IComponentData
    {
        public float Radius;
        public float BaseMass;
        public float HullDensity;
        public float CargoMassPerUnit;
        public float Restitution;
        public float TangentialDamping;

        public static VesselPhysicalProperties Default => new VesselPhysicalProperties
        {
            Radius = 0.6f,
            BaseMass = 5f,
            HullDensity = 1f,
            CargoMassPerUnit = 0.02f,
            Restitution = 0.12f,
            TangentialDamping = 0.25f
        };
    }

    /// <summary>
    /// Quality factors derived from production, blueprints, and integration.
    /// </summary>
    public struct VesselQuality : IComponentData
    {
        public float HullQuality;
        public float SystemsQuality;
        public float MobilityQuality;
        public float IntegrationQuality;

        public static VesselQuality Default => new VesselQuality
        {
            HullQuality = 0.5f,
            SystemsQuality = 0.5f,
            MobilityQuality = 0.5f,
            IntegrationQuality = 0.5f
        };
    }

    /// <summary>
    /// Tunable movement style modifiers derived from pilot/captain profiles.
    /// </summary>
    public struct VesselMotionProfileConfig : IComponentData
    {
        public float DeliberateSpeedMultiplier;
        public float DeliberateTurnMultiplier;
        public float DeliberateSlowdownMultiplier;
        public float EconomyAccelerationMultiplier;
        public float EconomyDecelerationMultiplier;
        public float ChaoticSpeedMultiplier;
        public float ChaoticTurnMultiplier;
        public float ChaoticAccelerationMultiplier;
        public float ChaoticDecelerationMultiplier;
        public float ChaoticSlowdownMultiplier;
        public float ChaoticDeviationStrength;
        public float ChaoticDeviationMinDistance;
        public float IntelligentTurnMultiplier;
        public float IntelligentSlowdownMultiplier;
        public float TurnSlowdownFactor;
        public float CapitalShipSpeedMultiplier;
        public float CapitalShipTurnMultiplier;
        public float CapitalShipAccelerationMultiplier;
        public float CapitalShipDecelerationMultiplier;
        public float MiningUndockSpeedMultiplier;
        public float MiningApproachSpeedMultiplier;
        public float MiningLatchSpeedMultiplier;
        public float MiningDetachSpeedMultiplier;
        public float MiningReturnSpeedMultiplier;
        public float MiningDockSpeedMultiplier;
        public float MinerRiskSpeedMultiplier;
        public float MinerRiskDeviationMultiplier;
        public float MinerRiskSlowdownMultiplier;
        public float MinerRiskArrivalMultiplier;

        public static VesselMotionProfileConfig Default => new VesselMotionProfileConfig
        {
            DeliberateSpeedMultiplier = 0.78f,
            DeliberateTurnMultiplier = 0.65f,
            DeliberateSlowdownMultiplier = 1.25f,
            EconomyAccelerationMultiplier = 0.7f,
            EconomyDecelerationMultiplier = 0.85f,
            ChaoticSpeedMultiplier = 1.2f,
            ChaoticTurnMultiplier = 1.4f,
            ChaoticAccelerationMultiplier = 1.3f,
            ChaoticDecelerationMultiplier = 1.15f,
            ChaoticSlowdownMultiplier = 0.8f,
            ChaoticDeviationStrength = 0.35f,
            ChaoticDeviationMinDistance = 6f,
            IntelligentTurnMultiplier = 1.15f,
            IntelligentSlowdownMultiplier = 0.9f,
            TurnSlowdownFactor = 0f,
            CapitalShipSpeedMultiplier = 0.85f,
            CapitalShipTurnMultiplier = 0.8f,
            CapitalShipAccelerationMultiplier = 0.75f,
            CapitalShipDecelerationMultiplier = 0.85f,
            MiningUndockSpeedMultiplier = 0.35f,
            MiningApproachSpeedMultiplier = 0.8f,
            MiningLatchSpeedMultiplier = 0.45f,
            MiningDetachSpeedMultiplier = 0.55f,
            MiningReturnSpeedMultiplier = 0.95f,
            MiningDockSpeedMultiplier = 0.4f,
            MinerRiskSpeedMultiplier = 1.25f,
            MinerRiskDeviationMultiplier = 1.4f,
            MinerRiskSlowdownMultiplier = 0.8f,
            MinerRiskArrivalMultiplier = 0.7f
        };
    }
}
