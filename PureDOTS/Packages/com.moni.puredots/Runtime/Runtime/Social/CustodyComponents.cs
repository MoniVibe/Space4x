using System;
using PureDOTS.Runtime.Authority;
using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// What kind of custody relationship exists.
    /// This is intentionally generic so villages, ships, factions, and future domains can share it.
    /// </summary>
    public enum CustodyKind : byte
    {
        None = 0,
        PrisonerOfWar = 1,
        Hostage = 2,
        CriminalDetention = 3,
        SpyDetention = 4,
        PoliticalPrisoner = 5
    }

    /// <summary>
    /// Current custody status. Systems can treat this as a small state machine.
    /// </summary>
    public enum CustodyStatus : byte
    {
        None = 0,
        Captured = 1,
        Detained = 2,
        Negotiating = 3,
        Transporting = 4,
        Released = 5,
        Executed = 6,
        Escaped = 7
    }

    [Flags]
    public enum CustodyFlags : ushort
    {
        None = 0,
        HighValue = 1 << 0,
        Interrogation = 1 << 1,
        HumaneTreatment = 1 << 2,
        HarshTreatment = 1 << 3,
        PubliclyKnown = 1 << 4
    }

    /// <summary>
    /// Attach to an entity that is currently in custody (hostage/POW/prisoner/spy).
    /// "CaptorScope" is the controlling aggregate (ship/village/faction), and "HoldingEntity"
    /// is the physical/contextual holding location if applicable (ship interior, jail building, etc.).
    /// </summary>
    public struct CustodyState : IComponentData
    {
        public CustodyKind Kind;
        public CustodyStatus Status;
        public CustodyFlags Flags;

        public Entity CaptorScope;
        public Entity HoldingEntity;

        public Entity OriginalAffiliation;
        public uint CapturedTick;
        public uint LastStatusTick;

        public IssuedByAuthority IssuedByAuthority;
    }
}

