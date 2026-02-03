using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Continuous moral alignment values used by behavior and compliance systems.
    /// </summary>
    public struct AlignmentTriplet : IComponentData
    {
        public half Law;
        public half Good;
        public half Integrity;

        public readonly float3 AsFloat3() => new float3((float)Law, (float)Good, (float)Integrity);

        public static AlignmentTriplet FromFloats(float law, float good, float integrity)
        {
            return new AlignmentTriplet
            {
                Law = (half)math.clamp(law, -1f, 1f),
                Good = (half)math.clamp(good, -1f, 1f),
                Integrity = (half)math.clamp(integrity, -1f, 1f)
            };
        }
    }

    /// <summary>
    /// Ethics axes available to crews. The <see cref="Count"/> value is a convenience marker for iteration.
    /// </summary>
    public enum EthicAxisId : byte
    {
        War = 0,
        Materialist = 1,
        Authoritarian = 2,
        Xenophobia = 3,
        Expansionist = 4,
        Count = 5
    }

    /// <summary>
    /// Sparse axis conviction stored per-entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EthicAxisValue : IBufferElementData
    {
        public EthicAxisId Axis;
        public half Value;
    }

    /// <summary>
    /// Identifies an individual's race. Authoring provides the lookup table.
    /// </summary>
    public struct RaceId : IComponentData
    {
        public ushort Value;
    }

    /// <summary>
    /// Identifies an individual's culture profile.
    /// </summary>
    public struct CultureId : IComponentData
    {
        public ushort Value;
    }

    /// <summary>
    /// Outlook identifiers shared across authoring, narrative, and DOTS systems.
    /// </summary>
    public enum OutlookId : ushort
    {
        Neutral = 0,
        Loyalist = 1,
        Opportunist = 2,
        Fanatic = 3,
        Mutinous = 4
    }

    /// <summary>
    /// Individual outlook weight. Multiple entries may exist, but only the top three are surfaced for crews.
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct OutlookEntry : IBufferElementData
    {
        public OutlookId OutlookId;
        public half Weight;
    }

    /// <summary>
    /// Aggregated race composition for a crew or colony.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct RacePresence : IBufferElementData
    {
        public ushort RaceId;
        public int Count;
    }

    /// <summary>
    /// Aggregated culture composition for a crew or colony.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct CulturePresence : IBufferElementData
    {
        public ushort CultureId;
        public int Count;
    }

    /// <summary>
    /// Aggregated outlook values (already filtered to top three).
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct TopOutlook : IBufferElementData
    {
        public OutlookId OutlookId;
        public half Weight;
    }

    /// <summary>
    /// Types of organizations an entity can affiliate with.
    /// </summary>
    public enum AffiliationType : byte
    {
        Empire = 0,
        Faction = 1,
        Army = 2,
        Fleet = 3,
        Band = 4,
        Squad = 5,
        Company = 6,
        Guild = 7,
        Colony = 8,
        Corporation = 9,
        Cooperative = 10
    }

    /// <summary>
    /// Affiliation membership entry. Loyalty ∈ [0,1] moderates mutiny/desertion severity.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct AffiliationTag : IBufferElementData
    {
        public AffiliationType Type;
        public Entity Target;
        public half Loyalty;
    }

    /// <summary>
    /// Marks an entity as operating under false pretenses. Compliance system uses suspicion instead of surfacing breaches.
    /// </summary>
    public struct SpyRole : IComponentData
    {
    }

    /// <summary>
    /// Contract binding prevents lawful entities from breaking ranks before the term expires.
    /// </summary>
    public struct ContractBinding : IComponentData
    {
        public uint ExpirationTick;
    }

    /// <summary>
    /// Tracks how close authorities are to exposing a spy.
    /// </summary>
    public struct SuspicionScore : IComponentData
    {
        public half Value;
    }

    /// <summary>
    /// Inclusive min/max ranges for alignment expectations.
    /// </summary>
    public struct AlignmentWindow
    {
        public half LawMin;
        public half LawMax;
        public half GoodMin;
        public half GoodMax;
        public half IntegrityMin;
        public half IntegrityMax;

        public readonly float3 Min => new float3((float)LawMin, (float)GoodMin, (float)IntegrityMin);
        public readonly float3 Max => new float3((float)LawMax, (float)GoodMax, (float)IntegrityMax);

        public readonly float3 ComputeDeviation(float3 value)
        {
            float lawDeviation = 0f;
            if (value.x < (float)LawMin)
            {
                lawDeviation = (float)LawMin - value.x;
            }
            else if (value.x > (float)LawMax)
            {
                lawDeviation = value.x - (float)LawMax;
            }

            float goodDeviation = 0f;
            if (value.y < (float)GoodMin)
            {
                goodDeviation = (float)GoodMin - value.y;
            }
            else if (value.y > (float)GoodMax)
            {
                goodDeviation = value.y - (float)GoodMax;
            }

            float integrityDeviation = 0f;
            if (value.z < (float)IntegrityMin)
            {
                integrityDeviation = (float)IntegrityMin - value.z;
            }
            else if (value.z > (float)IntegrityMax)
            {
                integrityDeviation = value.z - (float)IntegrityMax;
            }

            return new float3(lawDeviation, goodDeviation, integrityDeviation);
        }
    }

    /// <summary>
    /// Exposes organizational doctrine for compliance evaluation.
    /// </summary>
    public struct DoctrineProfile : IComponentData
    {
        public AlignmentWindow AlignmentWindow;
        public half AxisTolerance;
        public half OutlookTolerance;
        public half ChaosMutinyThreshold;
        public half LawfulContractFloor;
        public half SuspicionGain;
    }

    /// <summary>
    /// Expected ethic axis ranges for organization members.
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct DoctrineAxisExpectation : IBufferElementData
    {
        public EthicAxisId Axis;
        public half Min;
        public half Max;
    }

    /// <summary>
    /// Required outlooks for membership.
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct DoctrineOutlookExpectation : IBufferElementData
    {
        public OutlookId OutlookId;
        public half MinimumWeight;
    }

    /// <summary>
    /// Compliance signal produced by the evaluation system.
    /// </summary>
    public enum ComplianceBreachType : byte
    {
        Mutiny = 0,
        Desertion = 1,
        Independence = 2
    }

    /// <summary>
    /// Result entry written when an alignment or doctrine breach is detected.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct ComplianceBreach : IBufferElementData
    {
        public Entity Affiliation;
        public ComplianceBreachType Type;
        public half Severity;
    }

    /// <summary>
    /// Planner ticket produced from a compliance breach for AI/narrative consumers.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct ComplianceTicket : IBufferElementData
    {
        public Entity Affiliation;
        public ComplianceBreachType Type;
        public half Severity;
        public uint Tick;
    }

    /// <summary>
    /// Singleton marker for the compliance ticket queue consumed by planner/narrative systems.
    /// </summary>
    public struct ComplianceTicketQueue : IComponentData
    {
    }

    /// <summary>
    /// Queue entry emitted by the ticket queue system.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ComplianceTicketQueueEntry : IBufferElementData
    {
        public Entity Source;
        public Entity Affiliation;
        public ComplianceBreachType Type;
        public half Severity;
        public uint Tick;
    }

    /// <summary>
    /// Planner inbox marker for downstream AI/narrative systems to consume compliance tickets.
    /// </summary>
    public struct CompliancePlannerInbox : IComponentData
    {
    }

    /// <summary>
    /// Planner-friendly ticket entry.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CompliancePlannerTicket : IBufferElementData
    {
        public Entity Source;
        public Entity Affiliation;
        public ComplianceBreachType Type;
        public half Severity;
        public uint Tick;
    }

    /// <summary>
    /// Lightweight disciplinary record for court-martial style consequences.
    /// </summary>
    public enum DisciplinaryInfractionKind : byte
    {
        MercyInCombat = 0,
        DisobeyedOrders = 1
    }

    /// <summary>
    /// Records a single disciplinary infraction for an entity.
    /// </summary>
    public struct DisciplinaryRecord : IComponentData
    {
        public Entity Faction;
        public DisciplinaryInfractionKind Kind;
        public half Severity;
        public uint Tick;
    }

    /// <summary>
    /// Alignment helper utility functions.
    /// </summary>
    public static class AlignmentMath
    {
        public static float Chaos(in AlignmentTriplet alignment)
        {
            return math.saturate(0.5f * (1f - (float)alignment.Law));
        }

        public static float Lawfulness(in AlignmentTriplet alignment)
        {
            return math.saturate(0.5f * (1f + (float)alignment.Law));
        }

        public static float IntegrityNormalized(in AlignmentTriplet alignment)
        {
            return math.saturate(0.5f * (1f + (float)alignment.Integrity));
        }
    }
}
