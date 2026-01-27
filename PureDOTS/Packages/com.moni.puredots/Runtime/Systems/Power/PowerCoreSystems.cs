using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Power
{
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup), OrderFirst = true)]
    public partial struct PowerGenerationSystem : ISystem
    {
        private ComponentLookup<FuelConsumerState> _fuelStateLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerGenerator>();
            state.RequireForUpdate<TimeState>();
            _fuelStateLookup = state.GetComponentLookup<FuelConsumerState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            _fuelStateLookup.Update(ref state);

            foreach (var (generator, entity) in SystemAPI.Query<RefRW<PowerGenerator>>().WithEntityAccess())
            {
                var output = PowerCoreMath.CalculateActualOutput(
                    generator.ValueRO.TheoreticalMaxOutput,
                    generator.ValueRO.CurrentOutputPercent,
                    generator.ValueRO.Efficiency,
                    generator.ValueRO.DegradationLevel,
                    out var wasteHeat);

                if (_fuelStateLookup.HasComponent(entity))
                {
                    var fuelRatio = math.clamp(_fuelStateLookup[entity].FuelRatio, 0f, 1f);
                    output *= fuelRatio;
                    wasteHeat *= fuelRatio;
                }

                generator.ValueRW.WasteHeat = wasteHeat;

                if (SystemAPI.HasComponent<PowerDistribution>(entity))
                {
                    var distribution = SystemAPI.GetComponent<PowerDistribution>(entity);
                    distribution.InputPower = output;
                    SystemAPI.SetComponent(entity, distribution);
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerGenerationSystem))]
    public partial struct PowerDistributionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerDistribution>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var distribution in SystemAPI.Query<RefRW<PowerDistribution>>())
            {
                var result = PowerCoreMath.CalculateDistribution(
                    distribution.ValueRO.InputPower,
                    distribution.ValueRO.DistributionEfficiency,
                    distribution.ValueRO.ConduitDamage);

                distribution.ValueRW.OutputPower = result.OutputPower;
                distribution.ValueRW.TransmissionLoss = result.TransmissionLoss;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerDistributionSystem))]
    [UpdateBefore(typeof(PowerBudgetSystem))]
    public partial struct PowerAllocationSystem : ISystem
    {
        private ComponentLookup<ModuleDisabledUntil> _disabledLookup;
        private ComponentLookup<PowerEffectiveness> _effectivenessLookup;
        private ComponentLookup<HeatGenerationRate> _heatRateLookup;
        private ComponentLookup<PowerHeatProfile> _heatProfileLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerAllocationPercent>();
            state.RequireForUpdate<PowerConsumer>();
            state.RequireForUpdate<TimeState>();
            _disabledLookup = state.GetComponentLookup<ModuleDisabledUntil>(true);
            _effectivenessLookup = state.GetComponentLookup<PowerEffectiveness>(false);
            _heatRateLookup = state.GetComponentLookup<HeatGenerationRate>(false);
            _heatProfileLookup = state.GetComponentLookup<PowerHeatProfile>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _disabledLookup.Update(ref state);
            _effectivenessLookup.Update(ref state);
            _heatRateLookup.Update(ref state);
            _heatProfileLookup.Update(ref state);

            var time = SystemAPI.GetSingleton<TimeState>();
            var currentTick = time.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (allocation, consumer, entity) in SystemAPI
                         .Query<RefRW<PowerAllocationPercent>, RefRW<PowerConsumer>>()
                         .WithEntityAccess())
            {
                var allocationPercent = math.clamp(allocation.ValueRO.Value, 0f, 250f);
                allocation.ValueRW.Value = allocationPercent;

                var isDisabled = _disabledLookup.HasComponent(entity)
                    && _disabledLookup[entity].Tick > currentTick;

                var requestedDraw = isDisabled
                    ? 0f
                    : PowerCoreMath.CalculatePowerConsumption(consumer.ValueRO.BaselineDraw, allocationPercent);

                consumer.ValueRW.RequestedDraw = requestedDraw;

                var effectiveness = isDisabled
                    ? 0f
                    : PowerCoreMath.CalculateModuleEffectiveness(allocationPercent);

                var baseHeat = _heatProfileLookup.HasComponent(entity)
                    ? _heatProfileLookup[entity].BaseHeatGeneration
                    : consumer.ValueRO.BaselineDraw;

                var heatRate = isDisabled
                    ? 0f
                    : PowerCoreMath.CalculateHeatGeneration(baseHeat, allocationPercent);

                if (_effectivenessLookup.HasComponent(entity))
                {
                    _effectivenessLookup[entity] = new PowerEffectiveness { Value = effectiveness };
                }
                else
                {
                    ecb.AddComponent(entity, new PowerEffectiveness { Value = effectiveness });
                }

                if (_heatRateLookup.HasComponent(entity))
                {
                    _heatRateLookup[entity] = new HeatGenerationRate { Value = heatRate };
                }
                else
                {
                    ecb.AddComponent(entity, new HeatGenerationRate { Value = heatRate });
                }

                if (isDisabled)
                {
                    consumer.ValueRW.AllocatedDraw = 0f;
                    consumer.ValueRW.Online = 0;
                    consumer.ValueRW.Starved = 1;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerAllocationSystem))]
    public partial struct PowerHeatSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HeatState>();
            state.RequireForUpdate<HeatGenerationRate>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var deltaTime = time.FixedDeltaTime;

            foreach (var (heatState, heatRate) in SystemAPI.Query<RefRW<HeatState>, RefRO<HeatGenerationRate>>())
            {
                var netHeat = heatRate.ValueRO.Value - heatState.ValueRO.PassiveDissipation;
                heatState.ValueRW.CurrentHeat += netHeat * deltaTime;
                var maxHeat = math.max(0f, heatState.ValueRO.MaxHeatCapacity);
                heatState.ValueRW.CurrentHeat = math.clamp(heatState.ValueRW.CurrentHeat, 0f, maxHeat * 1.2f);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerAllocationSystem))]
    public partial struct PowerBurnoutSystem : ISystem
    {
        private ComponentLookup<PowerBurnoutSettings> _burnoutSettingsLookup;
        private ComponentLookup<ModuleDisabledUntil> _disabledLookup;
        private ComponentLookup<ModuleFaulted> _faultedLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerAllocationPercent>();
            state.RequireForUpdate<TimeState>();
            _burnoutSettingsLookup = state.GetComponentLookup<PowerBurnoutSettings>(true);
            _disabledLookup = state.GetComponentLookup<ModuleDisabledUntil>(true);
            _faultedLookup = state.GetComponentLookup<ModuleFaulted>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = time.FixedDeltaTime;
            var currentTick = time.Tick;

            _burnoutSettingsLookup.Update(ref state);
            _disabledLookup.Update(ref state);
            _faultedLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (allocation, entity) in SystemAPI
                         .Query<RefRO<PowerAllocationPercent>>()
                         .WithEntityAccess())
            {
                if (_disabledLookup.HasComponent(entity))
                {
                    var disabled = _disabledLookup[entity];
                    if (disabled.Tick <= currentTick)
                    {
                        ecb.RemoveComponent<ModuleDisabledUntil>(entity);
                        if (_faultedLookup.HasComponent(entity))
                        {
                            ecb.RemoveComponent<ModuleFaulted>(entity);
                        }
                    }

                    continue;
                }

                var allocationPercent = math.clamp(allocation.ValueRO.Value, 0f, 250f);
                if (allocationPercent <= 100f)
                {
                    continue;
                }

                var settings = _burnoutSettingsLookup.HasComponent(entity)
                    ? _burnoutSettingsLookup[entity]
                    : new PowerBurnoutSettings
                    {
                        Quality = PowerQuality.Standard,
                        CooldownTicks = 0u,
                        RiskMultiplier = 1f
                    };

                var risk = PowerCoreMath.CalculateBurnoutRisk(allocationPercent, settings.Quality, deltaTime);
                risk *= math.max(0f, settings.RiskMultiplier);
                if (risk <= 0f)
                {
                    continue;
                }

                var seed = math.hash(new uint3((uint)entity.Index, (uint)entity.Version, currentTick));
                if (seed == 0u)
                {
                    seed = 1u;
                }
                var random = new Unity.Mathematics.Random(seed);
                var roll = random.NextFloat(0f, 1f);
                if (roll >= risk)
                {
                    continue;
                }

                var cooldownTicks = settings.CooldownTicks;
                if (cooldownTicks == 0u)
                {
                    var seconds = 60f;
                    cooldownTicks = (uint)math.max(1f, math.round(seconds / math.max(0.0001f, deltaTime)));
                }

                ecb.AddComponent(entity, new ModuleDisabledUntil { Tick = currentTick + cooldownTicks });
                if (!_faultedLookup.HasComponent(entity))
                {
                    ecb.AddComponent<ModuleFaulted>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerAllocationSystem))]
    public partial struct PowerBudgetSystem : ISystem
    {
        private ComponentLookup<PowerDomainRef> _domainLookup;
        private ComponentLookup<PowerConsumer> _consumerLookup;
        private ComponentLookup<PowerDistribution> _distributionLookup;
        private ComponentLookup<PowerBattery> _batteryLookup;
        private ComponentLookup<PowerLedger> _ledgerLookup;
        private ComponentLookup<PowerBankTag> _bankLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerDistribution>();
            _domainLookup = state.GetComponentLookup<PowerDomainRef>(true);
            _consumerLookup = state.GetComponentLookup<PowerConsumer>(false);
            _distributionLookup = state.GetComponentLookup<PowerDistribution>(true);
            _batteryLookup = state.GetComponentLookup<PowerBattery>(true);
            _ledgerLookup = state.GetComponentLookup<PowerLedger>(false);
            _bankLookup = state.GetComponentLookup<PowerBankTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = math.max(0.0001f, time.FixedDeltaTime);

            _domainLookup.Update(ref state);
            _consumerLookup.Update(ref state);
            _distributionLookup.Update(ref state);
            _batteryLookup.Update(ref state);
            _ledgerLookup.Update(ref state);
            _bankLookup.Update(ref state);

            var consumerQuery = SystemAPI.QueryBuilder().WithAll<PowerConsumer>().Build();
            var consumerEntities = consumerQuery.ToEntityArray(Allocator.Temp);
            var domainMap = new NativeParallelMultiHashMap<Entity, Entity>(consumerEntities.Length, Allocator.Temp);

            for (int i = 0; i < consumerEntities.Length; i++)
            {
                var consumerEntity = consumerEntities[i];
                var domain = _domainLookup.HasComponent(consumerEntity)
                    ? _domainLookup[consumerEntity].Value
                    : consumerEntity;
                domainMap.Add(domain, consumerEntity);
            }

            var processedDomains = new NativeHashSet<Entity>(consumerEntities.Length, Allocator.Temp);
            var domainEntities = SystemAPI.QueryBuilder().WithAll<PowerDistribution>().Build().ToEntityArray(Allocator.Temp);

            for (int i = 0; i < domainEntities.Length; i++)
            {
                var domainEntity = domainEntities[i];
                processedDomains.Add(domainEntity);

                if (!_distributionLookup.HasComponent(domainEntity))
                {
                    continue;
                }

                var distribution = _distributionLookup[domainEntity];
                var availablePower = math.max(0f, distribution.OutputPower);

                if (_batteryLookup.HasComponent(domainEntity) && !_bankLookup.HasComponent(domainEntity))
                {
                    var battery = _batteryLookup[domainEntity];
                    var health = math.clamp(battery.Health, 0.3f, 1f);
                    var dischargeEfficiency = math.saturate(battery.DischargeEfficiency * health);
                    var maxDischargeMWs = math.min(battery.MaxDischargeRate * deltaTime, battery.CurrentStored);
                    var maxDeliveredMW = deltaTime > 0f ? (maxDischargeMWs * dischargeEfficiency) / deltaTime : 0f;
                    availablePower += math.max(0f, maxDeliveredMW);
                }

                var totalRequested = 0f;
                var totalAllocated = 0f;

                if (domainMap.TryGetFirstValue(domainEntity, out var consumerEntity, out var iterator))
                {
                    var consumerList = new NativeList<Entity>(Allocator.Temp);
                    do
                    {
                        consumerList.Add(consumerEntity);
                    }
                    while (domainMap.TryGetNextValue(out consumerEntity, ref iterator));

                    for (var c = 0; c < consumerList.Length; c++)
                    {
                        var entity = consumerList[c];
                        var consumer = _consumerLookup[entity];
                        consumer.AllocatedDraw = 0f;
                        consumer.Online = 0;
                        consumer.Starved = 1;
                        _consumerLookup[entity] = consumer;

                        var requested = consumer.RequestedDraw > 0f
                            ? math.max(consumer.RequestedDraw, consumer.BaselineDraw)
                            : math.max(consumer.BaselineDraw, 0f);
                        totalRequested += requested;
                    }

                    for (var priority = 0; priority <= byte.MaxValue; priority++)
                    {
                        for (var c = 0; c < consumerList.Length; c++)
                        {
                            var entity = consumerList[c];
                            var consumer = _consumerLookup[entity];
                            if (consumer.Priority != priority)
                            {
                                continue;
                            }

                            var requested = consumer.RequestedDraw > 0f
                                ? math.max(consumer.RequestedDraw, consumer.BaselineDraw)
                                : math.max(consumer.BaselineDraw, 0f);

                            var allocation = math.min(requested, availablePower);
                            availablePower -= allocation;
                            totalAllocated += allocation;

                            var minRequired = consumer.BaselineDraw * math.clamp(consumer.MinOperatingFraction, 0f, 1f);
                            var online = (allocation + 0.0001f) >= minRequired ? (byte)1 : (byte)0;
                            var starved = (allocation + 0.0001f) < minRequired || (allocation + 0.0001f) < requested
                                ? (byte)1
                                : (byte)0;

                            consumer.AllocatedDraw = allocation;
                            consumer.Online = online;
                            consumer.Starved = starved;
                            _consumerLookup[entity] = consumer;

                            if (availablePower <= 0f)
                            {
                                availablePower = 0f;
                            }
                        }

                        if (availablePower <= 0f)
                        {
                            break;
                        }
                    }

                    consumerList.Dispose();
                }

                var ledger = new PowerLedger
                {
                    GenerationMW = distribution.InputPower,
                    DistributionInputMW = distribution.InputPower,
                    DistributionOutputMW = distribution.OutputPower,
                    DistributionLossMW = distribution.TransmissionLoss,
                    TotalRequestedMW = totalRequested,
                    TotalAllocatedMW = totalAllocated,
                    SurplusMW = math.max(0f, availablePower),
                    DeficitMW = math.max(0f, totalRequested - totalAllocated),
                    BatteryChargeMW = 0f,
                    BatteryDischargeMW = 0f,
                    BatteryStoredMWs = _batteryLookup.HasComponent(domainEntity)
                        ? _batteryLookup[domainEntity].CurrentStored
                        : 0f
                };

                if (_ledgerLookup.HasComponent(domainEntity))
                {
                    _ledgerLookup[domainEntity] = ledger;
                }
                else
                {
                    state.EntityManager.AddComponentData(domainEntity, ledger);
                }
            }

            for (int i = 0; i < consumerEntities.Length; i++)
            {
                var consumerEntity = consumerEntities[i];
                var domain = _domainLookup.HasComponent(consumerEntity)
                    ? _domainLookup[consumerEntity].Value
                    : consumerEntity;

                if (processedDomains.Contains(domain))
                {
                    continue;
                }

                var consumer = _consumerLookup[consumerEntity];
                consumer.AllocatedDraw = 0f;
                consumer.Online = 0;
                consumer.Starved = 1;
                _consumerLookup[consumerEntity] = consumer;
            }

            consumerEntities.Dispose();
            domainMap.Dispose();
            processedDomains.Dispose();
            domainEntities.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerBudgetSystem))]
    public partial struct BatterySelfDischargeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerBattery>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = time.FixedDeltaTime;

            foreach (var battery in SystemAPI.Query<RefRW<PowerBattery>>())
            {
                var discharge = PowerCoreMath.CalculateSelfDischarge(
                    battery.ValueRO.CurrentStored,
                    battery.ValueRO.SelfDischargeRate,
                    deltaTime);

                battery.ValueRW.CurrentStored = math.max(0f, battery.ValueRO.CurrentStored - discharge);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(BatterySelfDischargeSystem))]
    public partial struct BatteryChargeDischargeSystem : ISystem
    {
        private ComponentLookup<PowerLedger> _ledgerLookup;
        private ComponentLookup<PowerBankTag> _bankLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerBattery>();
            state.RequireForUpdate<PowerDistribution>();
            state.RequireForUpdate<TimeState>();
            _ledgerLookup = state.GetComponentLookup<PowerLedger>(false);
            _bankLookup = state.GetComponentLookup<PowerBankTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = math.max(0.0001f, time.FixedDeltaTime);

            _ledgerLookup.Update(ref state);
            _bankLookup.Update(ref state);

            foreach (var (battery, distribution, entity) in SystemAPI
                         .Query<RefRW<PowerBattery>, RefRO<PowerDistribution>>()
                         .WithEntityAccess())
            {
                if (_bankLookup.HasComponent(entity))
                {
                    continue;
                }

                var ledger = _ledgerLookup.HasComponent(entity)
                    ? _ledgerLookup[entity]
                    : default;

                var health = battery.ValueRO.MaxCycles > 0
                    ? PowerCoreMath.CalculateBatteryHealth(battery.ValueRO.CycleCount, battery.ValueRO.MaxCycles)
                    : math.clamp(battery.ValueRO.Health, 0.3f, 1f);
                battery.ValueRW.Health = health;

                var maxCapacity = battery.ValueRO.MaxCapacity * health;
                var chargeEfficiency = math.saturate(battery.ValueRO.ChargeEfficiency * health);
                var dischargeEfficiency = math.saturate(battery.ValueRO.DischargeEfficiency * health);

                var net = distribution.ValueRO.OutputPower - ledger.TotalAllocatedMW;
                ledger.BatteryChargeMW = 0f;
                ledger.BatteryDischargeMW = 0f;

                if (net > 0f)
                {
                    var chargeResult = PowerCoreMath.ChargeBattery(
                        battery.ValueRO.CurrentStored,
                        maxCapacity,
                        battery.ValueRO.MaxChargeRate,
                        chargeEfficiency,
                        net,
                        deltaTime);

                    battery.ValueRW.CurrentStored = chargeResult.NewStored;
                    ledger.BatteryChargeMW = chargeResult.PowerConsumed;
                    ledger.SurplusMW = math.max(0f, net - chargeResult.PowerConsumed);
                    ledger.DeficitMW = 0f;
                    if (chargeResult.ChargedAmount > 0f)
                    {
                        battery.ValueRW.CycleCount += 1;
                    }
                }
                else if (net < 0f)
                {
                    var deficit = math.abs(net);
                    var dischargeResult = PowerCoreMath.DischargeBattery(
                        battery.ValueRO.CurrentStored,
                        battery.ValueRO.MaxDischargeRate,
                        dischargeEfficiency,
                        deficit,
                        deltaTime);

                    battery.ValueRW.CurrentStored = dischargeResult.NewStored;
                    ledger.BatteryDischargeMW = dischargeResult.PowerDelivered;
                    ledger.DeficitMW = math.max(0f, deficit - dischargeResult.PowerDelivered);
                    ledger.SurplusMW = 0f;
                    if (dischargeResult.DischargedAmount > 0f)
                    {
                        battery.ValueRW.CycleCount += 1;
                    }
                }
                else
                {
                    ledger.SurplusMW = 0f;
                    ledger.DeficitMW = 0f;
                }

                ledger.BatteryStoredMWs = battery.ValueRO.CurrentStored;

                if (_ledgerLookup.HasComponent(entity))
                {
                    _ledgerLookup[entity] = ledger;
                }
                else
                {
                    state.EntityManager.AddComponentData(entity, ledger);
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(BatteryChargeDischargeSystem))]
    public partial struct PowerBankAssignmentSystem : ISystem
    {
        private ComponentLookup<PowerDomainRef> _domainLookup;
        private ComponentLookup<PowerBattery> _batteryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerBankRequirement>();
            _domainLookup = state.GetComponentLookup<PowerDomainRef>(true);
            _batteryLookup = state.GetComponentLookup<PowerBattery>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _domainLookup.Update(ref state);
            _batteryLookup.Update(ref state);

            var bankEntities = SystemAPI.QueryBuilder()
                .WithAll<PowerBankTag, PowerBattery>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            var bankDomains = new NativeArray<Entity>(bankEntities.Length, Allocator.Temp);
            var bankCaps = new NativeArray<float>(bankEntities.Length, Allocator.Temp);

            for (int i = 0; i < bankEntities.Length; i++)
            {
                var bankEntity = bankEntities[i];
                var domain = _domainLookup.HasComponent(bankEntity)
                    ? _domainLookup[bankEntity].Value
                    : bankEntity;
                bankDomains[i] = domain;
                bankCaps[i] = _batteryLookup.HasComponent(bankEntity)
                    ? _batteryLookup[bankEntity].MaxCapacity
                    : 0f;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (requirement, entity) in SystemAPI
                         .Query<RefRW<PowerBankRequirement>>()
                         .WithEntityAccess()
                         .WithNone<AssignedPowerBank>())
            {
                var domain = _domainLookup.HasComponent(entity)
                    ? _domainLookup[entity].Value
                    : entity;

                Entity selectedBank = Entity.Null;
                for (int i = 0; i < bankEntities.Length; i++)
                {
                    if (bankDomains[i] != domain)
                    {
                        continue;
                    }

                    if (bankCaps[i] >= requirement.ValueRO.MinimumBankCapacity)
                    {
                        selectedBank = bankEntities[i];
                        break;
                    }
                }

                if (selectedBank != Entity.Null)
                {
                    requirement.ValueRW.PowerBank = selectedBank;
                    ecb.AddComponent<AssignedPowerBank>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            bankEntities.Dispose();
            bankDomains.Dispose();
            bankCaps.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerBankAssignmentSystem))]
    public partial struct PowerBankChargeSystem : ISystem
    {
        private ComponentLookup<PowerLedger> _ledgerLookup;
        private ComponentLookup<PowerDomainRef> _domainLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerBankTag>();
            state.RequireForUpdate<PowerLedger>();
            state.RequireForUpdate<TimeState>();
            _ledgerLookup = state.GetComponentLookup<PowerLedger>(true);
            _domainLookup = state.GetComponentLookup<PowerDomainRef>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = math.max(0.0001f, time.FixedDeltaTime);

            _ledgerLookup.Update(ref state);
            _domainLookup.Update(ref state);

            var ledgerQuery = SystemAPI.QueryBuilder().WithAll<PowerLedger>().Build();
            var ledgerCount = ledgerQuery.CalculateEntityCount();
            var surplusMap = new NativeHashMap<Entity, float>(math.max(ledgerCount, 1), Allocator.Temp);
            foreach (var (ledger, entity) in SystemAPI.Query<RefRO<PowerLedger>>().WithEntityAccess())
            {
                surplusMap[entity] = math.max(0f, ledger.ValueRO.SurplusMW);
            }

            foreach (var (battery, entity) in SystemAPI
                         .Query<RefRW<PowerBattery>>()
                         .WithAll<PowerBankTag>()
                         .WithEntityAccess())
            {
                var domain = _domainLookup.HasComponent(entity)
                    ? _domainLookup[entity].Value
                    : entity;

                if (!surplusMap.TryGetValue(domain, out var surplus) || surplus <= 0f)
                {
                    continue;
                }

                var health = math.clamp(battery.ValueRO.Health, 0.3f, 1f);
                var maxCapacity = battery.ValueRO.MaxCapacity * health;
                var efficiency = math.saturate(battery.ValueRO.ChargeEfficiency * health);

                var chargeResult = PowerCoreMath.ChargeBattery(
                    battery.ValueRO.CurrentStored,
                    maxCapacity,
                    battery.ValueRO.MaxChargeRate,
                    efficiency,
                    surplus,
                    deltaTime);

                battery.ValueRW.CurrentStored = chargeResult.NewStored;
                surplus = math.max(0f, surplus - chargeResult.PowerConsumed);
                surplusMap[domain] = surplus;
            }

            surplusMap.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateAfter(typeof(PowerBankChargeSystem))]
    public partial struct PowerBankDrawSystem : ISystem
    {
        private ComponentLookup<PowerBankDrawResult> _drawResultLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PowerBankDrawRequest>();
            state.RequireForUpdate<PowerBattery>();
            state.RequireForUpdate<TimeState>();
            _drawResultLookup = state.GetComponentLookup<PowerBankDrawResult>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = math.max(0.0001f, time.FixedDeltaTime);

            _drawResultLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (battery, requests, entity) in SystemAPI
                         .Query<RefRW<PowerBattery>, DynamicBuffer<PowerBankDrawRequest>>()
                         .WithAll<PowerBankTag>()
                         .WithEntityAccess())
            {
                if (requests.Length == 0)
                {
                    continue;
                }

                var health = math.clamp(battery.ValueRO.Health, 0.3f, 1f);
                var efficiency = math.saturate(battery.ValueRO.DischargeEfficiency * health);
                var maxDischargeThisTick = math.max(0f, battery.ValueRO.MaxDischargeRate) * deltaTime;

                for (var i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    var desired = math.max(0f, request.AmountMWs);
                    var allowable = math.min(desired, maxDischargeThisTick);
                    allowable = math.min(allowable, math.max(0f, battery.ValueRO.CurrentStored));

                    var delivered = allowable * efficiency;
                    battery.ValueRW.CurrentStored = math.max(0f, battery.ValueRO.CurrentStored - allowable);
                    maxDischargeThisTick = math.max(0f, maxDischargeThisTick - allowable);

                    if (request.Consumer != Entity.Null)
                    {
                        var result = new PowerBankDrawResult
                        {
                            RequestedMWs = desired,
                            FulfilledMWs = delivered,
                            ShortfallMWs = math.max(0f, desired - delivered),
                            Starved = delivered + 0.0001f < desired ? (byte)1 : (byte)0
                        };

                        if (_drawResultLookup.HasComponent(request.Consumer))
                        {
                            _drawResultLookup[request.Consumer] = result;
                        }
                        else
                        {
                            ecb.AddComponent(request.Consumer, result);
                        }
                    }
                }

                requests.Clear();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
