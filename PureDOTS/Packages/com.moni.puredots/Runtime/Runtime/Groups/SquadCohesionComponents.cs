using Unity.Entities;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Represents per-squad cohesion phase thresholds & ack policy.
    /// </summary>
    public struct SquadCohesionProfile : IComponentData
    {
        /// <summary>Normalized threshold where squad is considered tight.</summary>
        public float TightThreshold01;

        /// <summary>Normalized threshold where squad becomes loose/dispersed.</summary>
        public float LooseThreshold01;

        /// <summary>Who to notify when issuing tighten/flank orders.</summary>
        public Entity CommandAuthority;

        /// <summary>Minimum boldness/discipline to send non-verbal acks.</summary>
        public float AckDisciplineRequirement;

        public static SquadCohesionProfile Default => new SquadCohesionProfile
        {
            TightThreshold01 = 0.75f,
            LooseThreshold01 = 0.35f,
            CommandAuthority = Entity.Null,
            AckDisciplineRequirement = 0.5f
        };
    }

    /// <summary>
    /// Runtime state derived from formation spread, used by AI/hauling gates.
    /// </summary>
    public struct SquadCohesionState : IComponentData
    {
        public float NormalizedCohesion;
        public byte Flags;
        public uint LastUpdateTick;
        public uint LastBroadcastTick;
        public uint LastTelemetryTick;

        public bool IsTight => (Flags & FlagTight) != 0;
        public bool IsLoose => (Flags & FlagLoose) != 0;

        public const byte FlagTight = 1 << 0;
        public const byte FlagLoose = 1 << 1;
    }

    /// <summary>
    /// Tactical orders issued to squads (flank, tighten, hold, etc).
    /// </summary>
    public struct SquadTacticOrder : IComponentData
    {
        public SquadTacticKind Kind;
        public Entity Issuer;
        public Entity Target;
        public float FocusBudgetCost;
        public float DisciplineRequired;
        public byte AckMode; // 0 = none, 1 = visual
        public uint IssueTick;
    }

    public enum SquadTacticKind : byte
    {
        None = 0,
        Tighten = 1,
        Loosen = 2,
        FlankLeft = 3,
        FlankRight = 4,
        Collapse = 5,
        Retreat = 6
    }
}


