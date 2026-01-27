using Unity.Entities;

namespace PureDOTS.Runtime.Agency
{
    public enum CompromiseKind : byte
    {
        Infiltration = 0,
        Spoof = 1,
        HostileOverride = 2,
        Corruption = 3,
        JammedMeaning = 4
    }

    public struct CompromiseState : IComponentData
    {
        public byte IsCompromised;
        public byte Suspicion;
        public byte Severity;
        public CompromiseKind Kind;
        public Entity Source;
        public uint SinceTick;
        public uint LastEvidenceTick;
    }

    public enum CompromiseResponseMode : byte
    {
        Isolate = 0,
        Disconnect = 1,
        AttemptRecovery = 2,
        ImmediatePurge = 3
    }

    public enum FriendlyFirePenaltyMode : byte
    {
        Normal = 0,
        WaivedIfCompromised = 1,
        CommendedIfCompromised = 2
    }

    public struct CompromiseDoctrine : IComponentData
    {
        public byte QuarantineThreshold;
        public byte PurgeThreshold;
        public CompromiseResponseMode PreferredResponse;
        public FriendlyFirePenaltyMode FriendlyFirePenaltyMode;
        public uint RecoveryBudgetTicks;
    }
}
