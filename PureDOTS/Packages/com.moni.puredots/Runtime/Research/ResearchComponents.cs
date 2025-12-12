using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Research
{
    public enum ScienceFieldId : byte
    {
        None,
        Materials,
        Propulsion,
        Data,
        Subspace,
        Biology
    }

    public enum ThreatIntentId : byte
    {
        None,
        Probe,
        Ambush,
        Escalate
    }

    public struct AnomalyConfig : IComponentData
    {
        public ScienceFieldId FieldId;
        public float BaseYieldPerTick;
        public float ChargeCapacity;
        public float RechargePerTick;
        public float BandwidthCost;
        public byte IsPermanent;
    }

    public struct AnomalyState : IComponentData
    {
        public float RemainingCharge;
        public byte ActiveHarvesters;
        public float RechargeProgress;
    }

    public struct AnomalyVisualIntent : IComponentData
    {
        public ScienceFieldId FieldId;
        public byte StrengthBucket;
    }

    public struct CarrierResearchModuleTag : IComponentData
    {
    }

    public struct CarrierResearchAssignment : IComponentData
    {
        public Entity TargetAnomaly;
        public float CooldownTicks;
        public float NextHarvestInTicks;
    }

    public struct CarrierThreatIntent : IComponentData
    {
        public ThreatIntentId Intent;
        public byte Confidence;
    }

    public struct ResearchBandwidthState : IComponentData
    {
        public float CurrentBandwidth;
        public float Capacity;
        public float RefillPerTick;
        public float LossFraction;
    }

    public struct ResearchHarvestState : IComponentData
    {
        public float PendingRawPoints;
        public float RefinedPoints;
        public ScienceFieldId FocusField;
    }

    [InternalBufferCapacity(4)]
    public struct ResearchTransferRequest : IBufferElementData
    {
        public Entity SourceAnomaly;
        public float RequestedPoints;
        public float LossPerUnit;
    }

    [InternalBufferCapacity(4)]
    public struct ResearchTransferResult : IBufferElementData
    {
        public Entity SourceAnomaly;
        public float DeliveredPoints;
        public float LostPoints;
    }

    public struct ResearchTelemetry : IComponentData
    {
        public ulong CompletedHarvests;
        public float TotalBandwidthUsed;
        public float TotalLoss;
    }
}
