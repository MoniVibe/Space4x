using System;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Village
{
    /// <summary>
    /// Village-level workforce policy and emergency flags (conscription, defense, etc.).
    /// </summary>
    public struct VillageWorkforcePolicy : IComponentData
    {
        public float ConscriptionUrgency; // 0-1
        public float DefenseUrgency;      // 0-1
        public byte ConscriptionActive;   // bool
    }

    [System.Flags]
    public enum VillageOutlookFlags : byte
    {
        None = 0,
        Materialistic = 1 << 0,
        Warlike = 1 << 1,
        Ascetic = 1 << 2,
        Spiritual = 1 << 3,
        Expansionist = 1 << 4
    }

    /// <summary>
    /// Simple outlook flags describing village tendencies (materialistic, warlike, etc.).
    /// </summary>
    public struct VillageOutlook : IComponentData
    {
        public VillageOutlookFlags Flags;
    }

    /// <summary>
    /// Desired workforce distribution expressed as shortages per job type.
    /// </summary>
    public struct VillageWorkforceDemandEntry : IBufferElementData
    {
        public VillagerJob.JobType JobType;
        public float Shortage; // positive means we need more of this role
    }

    /// <summary>
    /// Per-job preference weights derived from village outlook/alignment.
    /// </summary>
    public struct VillageJobPreferenceEntry : IBufferElementData
    {
        public VillagerJob.JobType JobType;
        public float Weight;
    }

    /// <summary>
    /// Next scheduled initiative tick for a villager's workforce decision.
    /// </summary>
    public struct WorkforceDecisionCooldown : IComponentData
    {
        public uint NextCheckTick;
    }

    /// <summary>
    /// Intent produced by the workforce decision system; downstream systems can enact it.
    /// </summary>
    public struct VillagerWorkforceIntent : IComponentData
    {
        public VillagerJob.JobType DesiredJob;
        public float DesireWeight;
    }
}
