using System;
using Unity.Entities;

namespace PureDOTS.Runtime.Modules
{
    public enum ModulePosture : byte
    {
        Off = 0,
        Standby = 1,
        Online = 2,
        Emergency = 3
    }

    public enum ModuleCapabilityKind : byte
    {
        None = 0,
        ThrustAuthority = 1,
        TurnAuthority = 2
    }

    public struct ModuleSpecBlob
    {
        public float PowerDrawOff;
        public float PowerDrawStandby;
        public float PowerDrawOnline;
        public float PowerDrawEmergency;
        public float TauColdToOnline;
        public float TauWarmToOnline;
        public float TauOnlineToStandby;
        public float TauStandbyToOff;
        public float MaxOutput;
        public float RampRateLimit;
        public ModuleCapabilityKind Capability;
    }

    public struct ModuleSpec : IComponentData
    {
        public BlobAssetReference<ModuleSpecBlob> Spec;
    }

    public struct ModuleRuntimeState : IComponentData
    {
        public ModulePosture Posture;
        public float NormalizedOutput;
        public float TargetOutput;
        public float TimeInState;
    }

    public struct ModulePowerRequest : IComponentData
    {
        public float RequestedPower;
    }

    public struct ModulePowerAllocation : IComponentData
    {
        public float AllocatedPower;
        public float SupplyRatio;
    }

    [Flags]
    public enum ModuleCommandFlags : byte
    {
        None = 0,
        Posture = 1 << 0,
        TargetOutput = 1 << 1,
        LockPosture = 1 << 2
    }

    [InternalBufferCapacity(2)]
    public struct ModuleCommand : IBufferElementData
    {
        public ModulePosture Posture;
        public float TargetOutput;
        public ModuleCommandFlags Flags;
    }

    [InternalBufferCapacity(4)]
    public struct ModuleAttachment : IBufferElementData
    {
        public Entity Module;
    }

    public struct ModuleOwner : IComponentData
    {
        public Entity Owner;
    }

    public struct ModulePowerSupply : IComponentData
    {
        public float AvailablePower;
    }

    public struct ModulePowerBudget : IComponentData
    {
        public float TotalRequested;
        public float TotalAllocated;
        public float SupplyRatio;
    }

    public struct ModuleCapabilityOutput : IComponentData
    {
        public float ThrustAuthority;
        public float TurnAuthority;
    }

    public struct EngineeringCohesion : IComponentData
    {
        public float Value;
    }

    public struct NavigationCohesion : IComponentData
    {
        public float Value;
    }

    public struct BridgeTechLevel : IComponentData
    {
        public float Value;
    }
}
