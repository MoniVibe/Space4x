using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum ReverseEngineeringTaskType : byte
    {
        ForensicScan = 0,
        DestructiveAnalysis = 1,
        SynthesizePrototype = 2
    }

    [InternalBufferCapacity(16)]
    public struct ReverseEngineeringEvidence : IBufferElementData
    {
        public ushort BlueprintId;
        public byte Stage; // 0=Raw, 1=Scanned, 2=Analyzed
        public byte Fidelity; // 0-100
        public byte Integrity; // 0-100
        public byte CoverageEfficiency;
        public byte CoverageReliability;
        public byte CoverageMass;
        public byte CoveragePower;
        public byte CoverageSignature;
        public byte CoverageDurability;
        public uint EvidenceSeed;
        public uint SourceTick;
    }

    [InternalBufferCapacity(4)]
    public struct ReverseEngineeringTask : IBufferElementData
    {
        public uint TaskId;
        public ReverseEngineeringTaskType Type;
        public ushort BlueprintId;
        public byte EvidenceNeeded;
        public uint EvidenceHash;
        public float DurationSeconds;
        public float Progress;
        public uint AttemptIndex;
        public uint TeamHash;
    }

    [InternalBufferCapacity(8)]
    public struct ReverseEngineeringBlueprintVariant : IBufferElementData
    {
        public uint VariantId;
        public ushort BlueprintId;
        public byte Quality;
        public byte RemainingRuns;
        public float EfficiencyScalar;
        public float ReliabilityScalar;
        public float MassScalar;
        public float PowerScalar;
        public float SignatureScalar;
        public float DurabilityScalar;
        public uint EvidenceHash;
        public uint Seed;
    }

    [InternalBufferCapacity(6)]
    public struct ReverseEngineeringBlueprintProgress : IBufferElementData
    {
        public ushort BlueprintId;
        public uint AttemptCount;
    }

    public struct ReverseEngineeringState : IComponentData
    {
        public uint NextTaskId;
        public uint NextVariantId;
    }

    public struct ReverseEngineeringConfig : IComponentData
    {
        public float ForensicScanDurationSeconds;
        public float DestructiveAnalysisDurationSeconds;
        public float SynthesisDurationSeconds;
        public byte EvidencePerSynthesis;
        public byte IntegrityCostAnalysis;
        public byte IntegrityCostSynthesis;
        public byte MaxEvidencePerBlueprint;
        public byte MaxVariantRuns;

        public static ReverseEngineeringConfig Default => new ReverseEngineeringConfig
        {
            ForensicScanDurationSeconds = 6f,
            DestructiveAnalysisDurationSeconds = 10f,
            SynthesisDurationSeconds = 14f,
            EvidencePerSynthesis = 3,
            IntegrityCostAnalysis = 8,
            IntegrityCostSynthesis = 12,
            MaxEvidencePerBlueprint = 12,
            MaxVariantRuns = 3
        };
    }
}
