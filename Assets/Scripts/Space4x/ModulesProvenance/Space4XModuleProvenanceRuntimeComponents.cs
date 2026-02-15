using Unity.Collections;
using Unity.Entities;

namespace Space4X.ModulesProvenance
{
    public struct FacilityDivisionCapability : IComponentData
    {
        public DivisionId DivisionId;
        public float Capability;
        public float BaselineProcessMaturity;
    }

    [InternalBufferCapacity(8)]
    public struct FacilityBlueprintMaturity : IBufferElementData
    {
        public BlueprintId BlueprintId;
        public float ProcessMaturity;
    }

    public struct WorkforceQuality : IComponentData
    {
        public float SkillCooling;
        public float SkillPower;
        public float SkillOptics;
        public float SkillMount;
        public float SkillFirmware;
    }

    [InternalBufferCapacity(8)]
    public struct MaterialBatch : IBufferElementData
    {
        public FixedString64Bytes MaterialId;
        public float Qty;
        public float Quality;
    }

    [InternalBufferCapacity(8)]
    public struct LimbBatch : IBufferElementData
    {
        public BlueprintId BlueprintId;
        public DivisionId DivisionId;
        public float Qty;
        public float LimbQuality;
    }

    [InternalBufferCapacity(8)]
    public struct ModuleItem : IBufferElementData
    {
        public BlueprintId BlueprintId;
        public float QualityCooling;
        public float QualityPower;
        public float QualityOptics;
        public float QualityMount;
        public float QualityFirmware;
        public float IntegrationQuality;
        public uint ProvenanceDigest;
    }

    public struct ModuleCommissionSpec : IComponentData
    {
        public float MinCoolingQuality;
        public float MinPowerQuality;
        public float MinOpticsQuality;
        public float MinMountQuality;
        public float MinFirmwareQuality;
        public float MinIntegrationQuality;
        public byte RejectBelowSpec;
    }

    [InternalBufferCapacity(16)]
    public struct ModuleScrapEvent : IBufferElementData
    {
        public BlueprintId BlueprintId;
        public DivisionId DivisionId;
        public float ObservedQuality;
        public float RequiredQuality;
        public uint Tick;
        public uint ReasonCode;
    }

    public struct ModuleQualityTargets : IComponentData
    {
        public float LimbQualityTarget;
        public float IntegrationQualityTarget;
    }
}
