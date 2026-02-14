using Unity.Collections;
using Unity.Entities;

namespace Space4X.ModulesQuality
{
    public struct Space4XModulePipelineTag : IComponentData
    {
    }

    public struct Space4XModulePipelineWorkerTag : IComponentData
    {
    }

    public enum Space4XFacilityType : byte
    {
        MaterialRefinery = 0,
        PartForge = 1,
        ModuleAssembler = 2,
        Shipyard = 3
    }

    public enum Space4XResearchDirection : byte
    {
        Balanced = 0,
        Throughput = 1,
        Quality = 2
    }

    public struct Space4XFacility : IComponentData
    {
        public Space4XFacilityType Type;
        public float QualityTarget;
    }

    public struct Space4XFacilityCapability : IComponentData
    {
        // Process-capability proxy similar to Cpk intuition (higher is more consistent).
        public float ProcessCapability;
    }

    public struct Space4XWorkforceSlot : IComponentData
    {
        public byte RequiredWorkers;
    }

    public struct Space4XAssignedWorkers : IComponentData
    {
        public byte WorkerCount;
        public byte Tier0Count;
        public byte Tier1Count;
        public byte Tier2Count;
        public float AverageSkill;
    }

    public struct Space4XWorkerSkill : IComponentData
    {
        public float Skill;
        public byte Tier;
    }

    public struct Space4XWorkerAssignment : IComponentData
    {
        public Entity Facility;
    }

    public struct Space4XFacilityProcessState : IComponentData
    {
        public float Progress;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XMaterialBatch : IBufferElementData
    {
        public uint BatchId;
        public float Quantity;
        public float MaterialQuality;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XPartBatch : IBufferElementData
    {
        public uint PartId;
        public float Quantity;
        public float PartQuality;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XModuleItem : IBufferElementData
    {
        public uint ModuleId;
        public float ModuleQuality;
        public uint ProvenanceDigest;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XShipModuleIntegration : IBufferElementData
    {
        public uint ShipId;
        public byte Slot;
        public uint ModuleId;
        public float InstallQuality;
    }

    public struct Space4XModulePipelineRuntime : IComponentData
    {
        public Entity Refinery;
        public Entity Forge;
        public Entity Assembler;
        public Entity Shipyard;

        public uint PartsProduced;
        public uint ModulesAssembled;
        public uint InstallsCompleted;

        public float PartQualitySum;
        public float ModuleQualitySum;
        public float InstallQualitySum;

        public uint Digest;
        public uint NextBatchId;
        public uint NextPartId;
        public uint NextModuleId;

        public byte Initialized;
        public byte MetricsEmitted;
    }

    public struct Space4XModulePipelineResearchBias : IComponentData
    {
        public Space4XResearchDirection Direction;
    }
}
