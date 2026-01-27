using Unity.Entities;

namespace PureDOTS.Runtime.Power
{
    public struct PowerGenerator : IComponentData
    {
        public float TheoreticalMaxOutput; // MW
        public float CurrentOutputPercent;
        public float Efficiency;
        public float DegradationLevel;
        public float WasteHeat; // MW
        public byte TechLevel;
    }

    public struct PowerDistribution : IComponentData
    {
        public float InputPower; // MW
        public float TransmissionLoss; // MW
        public float OutputPower; // MW
        public float DistributionEfficiency;
        public float ConduitDamage;
        public byte TechLevel;
    }

    public struct PowerBattery : IComponentData
    {
        public float MaxCapacity; // MWs
        public float CurrentStored; // MWs
        public float MaxChargeRate; // MW
        public float MaxDischargeRate; // MW
        public float SelfDischargeRate;
        public float ChargeEfficiency;
        public float DischargeEfficiency;
        public float Health;
        public int CycleCount;
        public int MaxCycles;
        public byte TechLevel;
    }

    public struct PowerConsumer : IComponentData
    {
        public float BaselineDraw; // MW
        public float RequestedDraw; // MW
        public float MinOperatingFraction;
        public byte Priority;
        public float AllocatedDraw; // MW
        public byte Online;
        public byte Starved;
    }

    public struct PowerAllocationPercent : IComponentData
    {
        public float Value; // 0-250
    }

    public struct PowerEffectiveness : IComponentData
    {
        public float Value; // 0-1.8
    }

    public struct HeatGenerationRate : IComponentData
    {
        public float Value; // MW
    }

    public struct HeatState : IComponentData
    {
        public float CurrentHeat;
        public float MaxHeatCapacity;
        public float PassiveDissipation;
    }

    public struct PowerHeatProfile : IComponentData
    {
        public float BaseHeatGeneration;
    }

    public struct PowerBurnoutSettings : IComponentData
    {
        public PowerQuality Quality;
        public uint CooldownTicks;
        public float RiskMultiplier;
    }

    public struct ModuleFaulted : IComponentData
    {
    }

    public struct ModuleDisabledUntil : IComponentData
    {
        public uint Tick;
    }

    public struct PowerLedger : IComponentData
    {
        public float GenerationMW;
        public float DistributionInputMW;
        public float DistributionOutputMW;
        public float DistributionLossMW;
        public float TotalRequestedMW;
        public float TotalAllocatedMW;
        public float SurplusMW;
        public float DeficitMW;
        public float BatteryChargeMW;
        public float BatteryDischargeMW;
        public float BatteryStoredMWs;
    }

    public struct PowerDomainRef : IComponentData
    {
        public Entity Value;
    }

    public struct PowerBankTag : IComponentData
    {
    }

    public struct PowerBankRequirement : IComponentData
    {
        public float MinimumBankCapacity; // MWs
        public float RecommendedCapacity; // MWs
        public Entity PowerBank;
        public byte CanOperateWithoutBank;
        public float NoBankPenalty;
    }

    public struct AssignedPowerBank : IComponentData
    {
    }

    [InternalBufferCapacity(2)]
    public struct PowerBankDrawRequest : IBufferElementData
    {
        public float AmountMWs;
        public Entity Consumer;
    }

    public struct PowerBankDrawResult : IComponentData
    {
        public float RequestedMWs;
        public float FulfilledMWs;
        public float ShortfallMWs;
        public byte Starved;
    }
}
