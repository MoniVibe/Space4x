using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Type of order a captain can receive.
    /// </summary>
    public enum CaptainOrderType : byte
    {
        None = 0,

        // Movement orders
        MoveTo = 1,
        Patrol = 2,
        Escort = 3,
        Retreat = 4,

        // Combat orders
        Attack = 10,
        Defend = 11,
        Intercept = 12,
        Blockade = 13,

        // Economic orders
        Mine = 20,
        Haul = 21,
        Trade = 22,
        Resupply = 23,

        // Support orders
        Repair = 30,
        Rescue = 31,
        Construct = 32,
        Survey = 33,

        // Special orders
        Standby = 40,
        Disengage = 41,
        Negotiate = 42
    }

    /// <summary>
    /// Status of a captain's current order.
    /// </summary>
    public enum CaptainOrderStatus : byte
    {
        None = 0,
        Received = 1,
        Validating = 2,
        PreFlight = 3,
        Executing = 4,
        Completed = 5,
        Failed = 6,
        Cancelled = 7,
        Escalated = 8
    }

    /// <summary>
    /// Autonomy level for captain decision making.
    /// </summary>
    public enum CaptainAutonomy : byte
    {
        /// <summary>
        /// Strict adherence to orders, no deviation.
        /// </summary>
        Strict = 0,

        /// <summary>
        /// Minor tactical adjustments allowed.
        /// </summary>
        Tactical = 1,

        /// <summary>
        /// Can reroute, delay, or request assistance.
        /// </summary>
        Operational = 2,

        /// <summary>
        /// Full autonomy - can abort, divert, or initiate new objectives.
        /// </summary>
        Strategic = 3
    }

    /// <summary>
    /// Current order assigned to a captain.
    /// </summary>
    public struct CaptainOrder : IComponentData
    {
        /// <summary>
        /// Type of order.
        /// </summary>
        public CaptainOrderType Type;

        /// <summary>
        /// Current status.
        /// </summary>
        public CaptainOrderStatus Status;

        /// <summary>
        /// Priority (lower = higher priority).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Target entity for the order.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Target position (if not entity-based).
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Tick when order was issued.
        /// </summary>
        public uint IssuedTick;

        /// <summary>
        /// Tick when order should timeout (0 = no timeout).
        /// </summary>
        public uint TimeoutTick;

        /// <summary>
        /// Entity that issued this order.
        /// </summary>
        public Entity IssuingAuthority;

        public static CaptainOrder Create(CaptainOrderType type, Entity target, byte priority, uint issuedTick, Entity authority)
        {
            return new CaptainOrder
            {
                Type = type,
                Status = CaptainOrderStatus.Received,
                Priority = priority,
                TargetEntity = target,
                TargetPosition = float3.zero,
                IssuedTick = issuedTick,
                TimeoutTick = 0,
                IssuingAuthority = authority
            };
        }

        public static CaptainOrder CreatePositional(CaptainOrderType type, float3 position, byte priority, uint issuedTick, Entity authority)
        {
            return new CaptainOrder
            {
                Type = type,
                Status = CaptainOrderStatus.Received,
                Priority = priority,
                TargetEntity = Entity.Null,
                TargetPosition = position,
                IssuedTick = issuedTick,
                TimeoutTick = 0,
                IssuingAuthority = authority
            };
        }
    }

    /// <summary>
    /// Captain state and decision-making context.
    /// </summary>
    public struct CaptainState : IComponentData
    {
        /// <summary>
        /// Autonomy level granted to this captain.
        /// </summary>
        public CaptainAutonomy Autonomy;

        /// <summary>
        /// Whether captain is ready to execute orders.
        /// </summary>
        public byte IsReady;

        /// <summary>
        /// Current confidence in mission success [0, 1].
        /// </summary>
        public half Confidence;

        /// <summary>
        /// Risk tolerance based on alignment [0, 1]. Higher = more risk-taking.
        /// </summary>
        public half RiskTolerance;

        /// <summary>
        /// Last tick when state was evaluated.
        /// </summary>
        public uint LastEvaluationTick;

        /// <summary>
        /// Number of successful orders completed.
        /// </summary>
        public uint SuccessCount;

        /// <summary>
        /// Number of failed orders.
        /// </summary>
        public uint FailureCount;

        public static CaptainState Default => new CaptainState
        {
            Autonomy = CaptainAutonomy.Tactical,
            IsReady = 0,
            Confidence = (half)0.5f,
            RiskTolerance = (half)0.5f,
            LastEvaluationTick = 0,
            SuccessCount = 0,
            FailureCount = 0
        };
    }

    /// <summary>
    /// Readiness thresholds for pre-flight checks.
    /// </summary>
    public struct CaptainReadiness : IComponentData
    {
        /// <summary>
        /// Minimum hull % required to proceed.
        /// </summary>
        public half MinHullRatio;

        /// <summary>
        /// Minimum morale required to proceed.
        /// </summary>
        public half MinMorale;

        /// <summary>
        /// Minimum fuel % required.
        /// </summary>
        public half MinFuelRatio;

        /// <summary>
        /// Minimum ammo % required.
        /// </summary>
        public half MinAmmoRatio;

        /// <summary>
        /// Maximum threat level to proceed (0 = any threat blocks, 1 = ignore threats).
        /// </summary>
        public half MaxThreatLevel;

        /// <summary>
        /// Current readiness score [0, 1].
        /// </summary>
        public half CurrentReadiness;

        /// <summary>
        /// Which checks failed (bitfield).
        /// </summary>
        public ReadinessFlags FailedChecks;

        public static CaptainReadiness Strict => new CaptainReadiness
        {
            MinHullRatio = (half)0.8f,
            MinMorale = (half)0.3f,
            MinFuelRatio = (half)0.7f,
            MinAmmoRatio = (half)0.6f,
            MaxThreatLevel = (half)0.3f,
            CurrentReadiness = (half)0f,
            FailedChecks = ReadinessFlags.None
        };

        public static CaptainReadiness Standard => new CaptainReadiness
        {
            MinHullRatio = (half)0.5f,
            MinMorale = (half)0f,
            MinFuelRatio = (half)0.4f,
            MinAmmoRatio = (half)0.3f,
            MaxThreatLevel = (half)0.5f,
            CurrentReadiness = (half)0f,
            FailedChecks = ReadinessFlags.None
        };

        public static CaptainReadiness Relaxed => new CaptainReadiness
        {
            MinHullRatio = (half)0.3f,
            MinMorale = (half)(-0.5f),
            MinFuelRatio = (half)0.2f,
            MinAmmoRatio = (half)0.1f,
            MaxThreatLevel = (half)0.8f,
            CurrentReadiness = (half)0f,
            FailedChecks = ReadinessFlags.None
        };
    }

    /// <summary>
    /// Flags for which readiness checks failed.
    /// </summary>
    [System.Flags]
    public enum ReadinessFlags : byte
    {
        None = 0,
        Hull = 1 << 0,
        Morale = 1 << 1,
        Fuel = 1 << 2,
        Ammo = 1 << 3,
        Threat = 1 << 4,
        Crew = 1 << 5
    }

    /// <summary>
    /// Request for escalation/assistance from a captain.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct EscalationRequest : IBufferElementData
    {
        /// <summary>
        /// Type of escalation.
        /// </summary>
        public EscalationType Type;

        /// <summary>
        /// Priority of the request.
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Reason for escalation.
        /// </summary>
        public EscalationReason Reason;

        /// <summary>
        /// Tick when request was made.
        /// </summary>
        public uint RequestTick;

        /// <summary>
        /// Whether request has been acknowledged.
        /// </summary>
        public byte Acknowledged;
    }

    /// <summary>
    /// Types of escalation requests.
    /// </summary>
    public enum EscalationType : byte
    {
        None = 0,
        Reinforcement = 1,
        Resupply = 2,
        Repair = 3,
        Evacuation = 4,
        NewOrders = 5,
        AbortMission = 6
    }

    /// <summary>
    /// Reasons for escalation.
    /// </summary>
    public enum EscalationReason : byte
    {
        None = 0,
        ThreatLevelExceeded = 1,
        ResourcesDepleted = 2,
        HullCritical = 3,
        MoraleCritical = 4,
        ObjectiveUnreachable = 5,
        OrderTimeout = 6,
        EnemyReinforcements = 7,
        AllyInDistress = 8
    }

    /// <summary>
    /// Order queue for pending orders.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CaptainOrderQueue : IBufferElementData
    {
        public CaptainOrder Order;
    }

    /// <summary>
    /// Utility functions for captain AI calculations.
    /// </summary>
    public static class CaptainAIUtility
    {
        /// <summary>
        /// Calculates risk tolerance based on alignment.
        /// Chaotic captains have higher tolerance, lawful lower.
        /// </summary>
        public static float CalculateRiskTolerance(in AlignmentTriplet alignment)
        {
            float law = (float)alignment.Law;
            float integrity = (float)alignment.Integrity;

            // Chaotic (low law) = higher risk tolerance
            // Low integrity = slightly higher risk tolerance
            float baseTolerance = 0.5f;
            float lawModifier = -law * 0.3f; // -0.3 to +0.3
            float integrityModifier = -integrity * 0.1f; // -0.1 to +0.1

            return math.clamp(baseTolerance + lawModifier + integrityModifier, 0.1f, 0.9f);
        }

        /// <summary>
        /// Adjusts readiness thresholds based on alignment.
        /// Lawful captains have stricter thresholds.
        /// </summary>
        public static CaptainReadiness AdjustForAlignment(CaptainReadiness baseReadiness, in AlignmentTriplet alignment)
        {
            float riskTolerance = CalculateRiskTolerance(alignment);
            float modifier = 1f - riskTolerance; // Higher tolerance = lower thresholds

            var adjusted = baseReadiness;
            adjusted.MinHullRatio = (half)((float)baseReadiness.MinHullRatio * modifier);
            adjusted.MinFuelRatio = (half)((float)baseReadiness.MinFuelRatio * modifier);
            adjusted.MinAmmoRatio = (half)((float)baseReadiness.MinAmmoRatio * modifier);
            adjusted.MaxThreatLevel = (half)math.lerp((float)baseReadiness.MaxThreatLevel, 1f, riskTolerance * 0.5f);

            return adjusted;
        }

        /// <summary>
        /// Determines if a Good-aligned captain should delay for ally assistance.
        /// </summary>
        public static bool ShouldAssistAlly(in AlignmentTriplet alignment, float allyDistress)
        {
            float good = (float)alignment.Good;
            if (good <= 0f)
            {
                return false;
            }

            // Good captains assist if ally is in significant distress
            return allyDistress > (1f - good) * 0.5f;
        }

        /// <summary>
        /// Determines if an Evil-aligned captain should take an opportunistic diversion.
        /// </summary>
        public static bool ShouldTakeOpportunity(in AlignmentTriplet alignment, float opportunityValue, float missionRisk)
        {
            float good = (float)alignment.Good;
            if (good >= 0f)
            {
                return false;
            }

            float evil = -good;
            // Evil captains divert if opportunity outweighs risk
            return opportunityValue * evil > missionRisk * 0.5f;
        }
    }
}

