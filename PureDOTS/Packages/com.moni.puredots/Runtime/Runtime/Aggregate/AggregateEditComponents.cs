using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.IntergroupRelations;

namespace PureDOTS.Runtime.Aggregate
{
    public enum AggregateEditScopeKind : byte
    {
        Single = 0,
        AggregateType = 1,
        OrgKind = 2,
        AllAggregates = 3
    }

    public enum AggregateEditOp : byte
    {
        Set = 0,
        Add = 1,
        Multiply = 2,
        Clamp = 3
    }

    public enum AggregateEditField : byte
    {
        BirthRatePerTick = 0,
        NewbornIntelligenceMean = 1,
        NewbornIntelligenceVariance = 2,
        NewbornIntelligenceMin = 3,
        NewbornIntelligenceMax = 4,
        NewbornWillMean = 5,
        NewbornWillVariance = 6,
        NewbornWillMin = 7,
        NewbornWillMax = 8
    }

    [System.Flags]
    public enum AggregateEditFlags : byte
    {
        None = 0,
        ApplyToExisting = 1 << 0
    }

    public struct AggregateEditAuthority : IComponentData
    {
        public byte AllowEdits;
        public byte IsSandboxMode;
        public byte MarkSavesAsEdited;
        public uint AppliedCount;
        public uint LastAppliedTick;
        public int LastAppliedSequence;
    }

    [InternalBufferCapacity(8)]
    public struct AggregateEditCommand : IBufferElementData
    {
        public Entity Target;
        public int TargetOrgId;
        public AggregateEditScopeKind Scope;
        public AggregateType ScopeAggregateType;
        public OrgKind ScopeOrgKind;
        public AggregateEditField Field;
        public AggregateEditOp Op;
        public AggregateEditFlags Flags;
        public float Value;
        public float ValueB;
        public uint ApplyTick;
        public uint DurationTicks;
        public int Sequence;
        public FixedString64Bytes Source;
    }

    [InternalBufferCapacity(32)]
    public struct AggregateEditAuditEntry : IBufferElementData
    {
        public uint AppliedTick;
        public Entity Target;
        public int TargetOrgId;
        public AggregateEditScopeKind Scope;
        public AggregateType ScopeAggregateType;
        public OrgKind ScopeOrgKind;
        public AggregateEditField Field;
        public AggregateEditOp Op;
        public AggregateEditFlags Flags;
        public float Value;
        public float ValueB;
        public uint DurationTicks;
        public int Sequence;
        public byte Result;
        public FixedString64Bytes Source;
    }

    public struct AggregateStatDistribution
    {
        public float Mean;
        public float Variance;
        public float Min;
        public float Max;
    }

    public struct AggregatePopulationTuning : IComponentData
    {
        public float BirthRatePerTick;
        public AggregateStatDistribution Intelligence;
        public AggregateStatDistribution Will;
    }

    public struct AggregatePopulationDrift : IComponentData
    {
        public float IntelligenceTargetMean;
        public float WillTargetMean;
        public uint AppliedTick;
        public uint DurationTicks;
    }

    public static class AggregatePopulationTuningDefaults
    {
        public const float DefaultBirthRatePerTick = 0.001f;

        public static AggregateStatDistribution DefaultStatDistribution()
        {
            return new AggregateStatDistribution
            {
                Mean = 50f,
                Variance = 10f,
                Min = 0f,
                Max = 100f
            };
        }

        public static AggregatePopulationTuning DefaultTuning()
        {
            return new AggregatePopulationTuning
            {
                BirthRatePerTick = DefaultBirthRatePerTick,
                Intelligence = DefaultStatDistribution(),
                Will = DefaultStatDistribution()
            };
        }
    }
}
