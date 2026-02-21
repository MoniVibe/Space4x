using System;
using PureDOTS.Runtime.Power;
using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that adds ship-scale power generation, storage, and consumer slots.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Ship Power")]
    public sealed class Space4XShipPowerAuthoring : MonoBehaviour
    {
        [Header("Reactor")]
        public Space4XReactorType ReactorType = Space4XReactorType.FusionStandard;
        public bool UseReactorDefaults = true;
        public float OutputMW = 3000f;
        [Range(0f, 1f)] public float Efficiency = 0.88f;
        public float IdleDrawMW = 300f;
        public float HotRestartSeconds = 1f;
        public float ColdRestartSeconds = 60f;
        [Range(0f, 2.5f)] public float OutputPercent = 1f;
        public byte ReactorTechLevel = 8;

        [Header("Distribution")]
        public bool UseDistributionTechDefaults = true;
        [Range(0f, 1f)] public float DistributionEfficiency = 0.93f;
        [Range(0f, 1f)] public float ConduitDamage = 0f;
        public byte DistributionTechLevel = 8;

        [Header("Battery")]
        public float BatteryCapacityMWs = 12000f;
        public float BatteryStoredMWs = 6000f;
        public float BatteryMaxChargeRateMW = 800f;
        public float BatteryMaxDischargeRateMW = 600f;
        public float BatterySelfDischargeRate = 0.0002f;
        [Range(0f, 1f)] public float BatteryChargeEfficiency = 0.95f;
        [Range(0f, 1f)] public float BatteryDischargeEfficiency = 0.95f;
        public byte BatteryTechLevel = 8;

        [Header("Capacitor Bank")]
        public bool AddCapacitorBank = true;
        public float CapacitorCapacityMWs = 5000f;
        public float CapacitorStoredMWs = 0f;
        public float CapacitorMaxChargeRateMW = 500f;
        public float CapacitorMaxDischargeRateMW = 3500f;
        public float CapacitorSelfDischargeRate = 0f;
        [Range(0f, 1f)] public float CapacitorChargeEfficiency = 0.95f;
        [Range(0f, 1f)] public float CapacitorDischargeEfficiency = 0.95f;
        public byte CapacitorTechLevel = 8;

        [Header("Focus")]
        public ShipPowerFocusMode DefaultFocusMode = ShipPowerFocusMode.Balanced;

        [Header("Special Energy")]
        public bool AddSpecialEnergy = true;
        public float SpecialEnergyBaseMax = 40f;
        public float SpecialEnergyBaseRegenPerSecond = 3f;
        public float SpecialEnergyReactorOutputToMax = 0.02f;
        public float SpecialEnergyReactorOutputToRegen = 0.0015f;
        [Range(0f, 2f)] public float SpecialEnergyReactorEfficiencyRegenMultiplier = 1f;
        [Range(0f, 1f)] public float SpecialEnergyRestartRegenPenaltyMultiplier = 0.2f;
        [Range(0f, 3f)] public float SpecialEnergyActivationCostMultiplier = 1f;
        [Range(0f, 1f)] public float SpecialEnergyStartFill01 = 1f;

        [Header("Consumers")]
        public ShipPowerConsumerSettings MobilityConsumer = ShipPowerConsumerSettings.Create(300f, 0.2f, 10, 8000f, 2500f);
        public ShipPowerConsumerSettings WeaponsConsumer = ShipPowerConsumerSettings.Create(800f, 0.25f, 30, 8000f, 2500f);
        public ShipPowerConsumerSettings ShieldsConsumer = ShipPowerConsumerSettings.Create(600f, 0.3f, 20, 12000f, 3500f);
        public ShipPowerConsumerSettings SensorsConsumer = ShipPowerConsumerSettings.Create(120f, 0.1f, 40, 4000f, 1500f);
        public ShipPowerConsumerSettings StealthConsumer = ShipPowerConsumerSettings.Create(400f, 0.1f, 50, 6000f, 2000f);
        public ShipPowerConsumerSettings LifeSupportConsumer = ShipPowerConsumerSettings.Create(80f, 0.5f, 0, 3000f, 1200f);

        [Serializable]
        public struct ShipPowerConsumerSettings
        {
            public bool Enabled;
            public float BaselineDraw;
            public float MinOperatingFraction;
            public byte Priority;
            public float HeatCapacity;
            public float HeatDissipation;

            public static ShipPowerConsumerSettings Create(float baselineDraw, float minOperatingFraction, byte priority,
                float heatCapacity, float heatDissipation)
            {
                return new ShipPowerConsumerSettings
                {
                    Enabled = true,
                    BaselineDraw = baselineDraw,
                    MinOperatingFraction = minOperatingFraction,
                    Priority = priority,
                    HeatCapacity = heatCapacity,
                    HeatDissipation = heatDissipation
                };
            }
        }

        private struct ReactorDefaults
        {
            public float OutputMW;
            public float Efficiency;
            public float IdleDrawMW;
            public float HotRestartSeconds;
            public float ColdRestartSeconds;
        }

        private static ReactorDefaults GetDefaults(Space4XReactorType type)
        {
            return type switch
            {
                Space4XReactorType.FusionMicro => new ReactorDefaults
                {
                    OutputMW = 1200f,
                    Efficiency = 0.82f,
                    IdleDrawMW = 150f,
                    HotRestartSeconds = 0.5f,
                    ColdRestartSeconds = 30f
                },
                Space4XReactorType.FusionStandard => new ReactorDefaults
                {
                    OutputMW = 3000f,
                    Efficiency = 0.88f,
                    IdleDrawMW = 300f,
                    HotRestartSeconds = 1f,
                    ColdRestartSeconds = 60f
                },
                Space4XReactorType.FusionHeavy => new ReactorDefaults
                {
                    OutputMW = 7000f,
                    Efficiency = 0.92f,
                    IdleDrawMW = 600f,
                    HotRestartSeconds = 2f,
                    ColdRestartSeconds = 90f
                },
                Space4XReactorType.AntimatterCapital => new ReactorDefaults
                {
                    OutputMW = 20000f,
                    Efficiency = 0.98f,
                    IdleDrawMW = 2000f,
                    HotRestartSeconds = 5f,
                    ColdRestartSeconds = 180f
                },
                _ => new ReactorDefaults
                {
                    OutputMW = 2000f,
                    Efficiency = 0.85f,
                    IdleDrawMW = 200f,
                    HotRestartSeconds = 1f,
                    ColdRestartSeconds = 60f
                }
            };
        }

        public sealed class Baker : Unity.Entities.Baker<Space4XShipPowerAuthoring>
        {
            public override void Bake(Space4XShipPowerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var defaults = GetDefaults(authoring.ReactorType);

                var output = authoring.UseReactorDefaults ? defaults.OutputMW : authoring.OutputMW;
                var efficiency = authoring.UseReactorDefaults ? defaults.Efficiency : authoring.Efficiency;
                var idleDraw = authoring.UseReactorDefaults ? defaults.IdleDrawMW : authoring.IdleDrawMW;
                var hotRestart = authoring.UseReactorDefaults ? defaults.HotRestartSeconds : authoring.HotRestartSeconds;
                var coldRestart = authoring.UseReactorDefaults ? defaults.ColdRestartSeconds : authoring.ColdRestartSeconds;

                var distributionEfficiency = authoring.UseDistributionTechDefaults
                    ? PowerCoreMath.GetDistributionEfficiency(authoring.DistributionTechLevel)
                    : authoring.DistributionEfficiency;

                AddComponent(entity, new PowerGenerator
                {
                    TheoreticalMaxOutput = math.max(0f, output),
                    CurrentOutputPercent = math.saturate(authoring.OutputPercent),
                    Efficiency = math.saturate(efficiency),
                    DegradationLevel = 0f,
                    WasteHeat = 0f,
                    TechLevel = authoring.ReactorTechLevel
                });

                AddComponent(entity, new PowerDistribution
                {
                    InputPower = 0f,
                    OutputPower = 0f,
                    TransmissionLoss = 0f,
                    DistributionEfficiency = math.saturate(distributionEfficiency),
                    ConduitDamage = math.saturate(authoring.ConduitDamage),
                    TechLevel = authoring.DistributionTechLevel
                });

                var batteryCapacity = math.max(0f, authoring.BatteryCapacityMWs);
                AddComponent(entity, new PowerBattery
                {
                    MaxCapacity = batteryCapacity,
                    CurrentStored = math.clamp(authoring.BatteryStoredMWs, 0f, batteryCapacity),
                    MaxChargeRate = math.max(0f, authoring.BatteryMaxChargeRateMW),
                    MaxDischargeRate = math.max(0f, authoring.BatteryMaxDischargeRateMW),
                    SelfDischargeRate = math.max(0f, authoring.BatterySelfDischargeRate),
                    ChargeEfficiency = math.saturate(authoring.BatteryChargeEfficiency),
                    DischargeEfficiency = math.saturate(authoring.BatteryDischargeEfficiency),
                    Health = 1f,
                    CycleCount = 0,
                    MaxCycles = 5000,
                    TechLevel = authoring.BatteryTechLevel
                });

                AddComponent(entity, new ShipReactorSpec
                {
                    Type = authoring.ReactorType,
                    OutputMW = math.max(0f, output),
                    Efficiency = math.saturate(efficiency),
                    IdleDrawMW = math.max(0f, idleDraw),
                    HotRestartSeconds = math.max(0f, hotRestart),
                    ColdRestartSeconds = math.max(0f, coldRestart)
                });

                AddComponent(entity, new ShipPowerFocus { Mode = authoring.DefaultFocusMode });
                AddComponent(entity, new RestartState
                {
                    Warmth = 0f,
                    RestartTimer = 0f,
                    Mode = RestartMode.Cold
                });

                if (authoring.AddSpecialEnergy)
                {
                    var specialConfig = new ShipSpecialEnergyConfig
                    {
                        BaseMax = math.max(0f, authoring.SpecialEnergyBaseMax),
                        BaseRegenPerSecond = math.max(0f, authoring.SpecialEnergyBaseRegenPerSecond),
                        ReactorOutputToMax = math.max(0f, authoring.SpecialEnergyReactorOutputToMax),
                        ReactorOutputToRegen = math.max(0f, authoring.SpecialEnergyReactorOutputToRegen),
                        ReactorEfficiencyRegenMultiplier = math.max(0f, authoring.SpecialEnergyReactorEfficiencyRegenMultiplier),
                        RestartRegenPenaltyMultiplier = math.clamp(authoring.SpecialEnergyRestartRegenPenaltyMultiplier, 0f, 1f),
                        ActivationCostMultiplier = math.max(0.05f, authoring.SpecialEnergyActivationCostMultiplier)
                    };

                    var initialMax = math.max(0f, specialConfig.BaseMax + math.max(0f, output) * specialConfig.ReactorOutputToMax);
                    var initialRegen = math.max(0f, specialConfig.BaseRegenPerSecond + math.max(0f, output) * specialConfig.ReactorOutputToRegen);
                    initialRegen *= math.max(0f, efficiency) * specialConfig.ReactorEfficiencyRegenMultiplier;
                    var initialCurrent = initialMax * math.saturate(authoring.SpecialEnergyStartFill01);

                    AddComponent(entity, specialConfig);
                    AddComponent(entity, new ShipSpecialEnergyState
                    {
                        Current = initialCurrent,
                        EffectiveMax = initialMax,
                        EffectiveRegenPerSecond = initialRegen,
                        LastSpent = 0f,
                        LastSpendTick = 0,
                        FailedSpendAttempts = 0,
                        LastUpdatedTick = 0
                    });
                    AddBuffer<ShipSpecialEnergyPassiveModifier>(entity);
                    AddBuffer<ShipSpecialEnergySpendRequest>(entity);
                }

                var consumers = AddBuffer<ShipPowerConsumer>(entity);
                AddConsumer(this, entity, consumers, ShipPowerConsumerType.Mobility, authoring.MobilityConsumer);
                AddConsumer(this, entity, consumers, ShipPowerConsumerType.Weapons, authoring.WeaponsConsumer);
                AddConsumer(this, entity, consumers, ShipPowerConsumerType.Shields, authoring.ShieldsConsumer);
                AddConsumer(this, entity, consumers, ShipPowerConsumerType.Sensors, authoring.SensorsConsumer);
                AddConsumer(this, entity, consumers, ShipPowerConsumerType.Stealth, authoring.StealthConsumer);
                AddConsumer(this, entity, consumers, ShipPowerConsumerType.LifeSupport, authoring.LifeSupportConsumer);

                if (authoring.AddCapacitorBank)
                {
                    var bankEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    var capacity = math.max(0f, authoring.CapacitorCapacityMWs);
                    AddComponent(bankEntity, new PowerBattery
                    {
                        MaxCapacity = capacity,
                        CurrentStored = math.clamp(authoring.CapacitorStoredMWs, 0f, capacity),
                        MaxChargeRate = math.max(0f, authoring.CapacitorMaxChargeRateMW),
                        MaxDischargeRate = math.max(0f, authoring.CapacitorMaxDischargeRateMW),
                        SelfDischargeRate = math.max(0f, authoring.CapacitorSelfDischargeRate),
                        ChargeEfficiency = math.saturate(authoring.CapacitorChargeEfficiency),
                        DischargeEfficiency = math.saturate(authoring.CapacitorDischargeEfficiency),
                        Health = 1f,
                        CycleCount = 0,
                        MaxCycles = 10000,
                        TechLevel = authoring.CapacitorTechLevel
                    });
                    AddComponent(bankEntity, new PowerBankTag());
                    AddComponent(bankEntity, new ShipCapacitorBankTag());
                    AddComponent(bankEntity, new PowerDomainRef { Value = entity });
                }
            }

            private static void AddConsumer(Baker baker, Entity owner, DynamicBuffer<ShipPowerConsumer> buffer,
                ShipPowerConsumerType type, ShipPowerConsumerSettings settings)
            {
                if (!settings.Enabled)
                {
                    return;
                }

                var consumerEntity = baker.CreateAdditionalEntity(TransformUsageFlags.None);
                baker.AddComponent(consumerEntity, new PowerConsumer
                {
                    BaselineDraw = math.max(0f, settings.BaselineDraw),
                    RequestedDraw = 0f,
                    MinOperatingFraction = math.clamp(settings.MinOperatingFraction, 0f, 1f),
                    Priority = settings.Priority,
                    AllocatedDraw = 0f,
                    Online = 0,
                    Starved = 0
                });
                baker.AddComponent(consumerEntity, new PowerAllocationPercent { Value = 100f });
                baker.AddComponent(consumerEntity, new PowerAllocationTarget { Value = 100f });
                baker.AddComponent(consumerEntity, new PowerHeatProfile
                {
                    BaseHeatGeneration = math.max(0f, settings.BaselineDraw)
                });
                baker.AddComponent(consumerEntity, new HeatState
                {
                    CurrentHeat = 0f,
                    MaxHeatCapacity = math.max(0f, settings.HeatCapacity),
                    PassiveDissipation = math.max(0f, settings.HeatDissipation)
                });
                baker.AddComponent(consumerEntity, new PowerDomainRef { Value = owner });

                buffer.Add(new ShipPowerConsumer
                {
                    Type = type,
                    Consumer = consumerEntity
                });
            }
        }
    }
}
