using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    public partial struct CarrierModuleStatAggregationJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<ModuleStatModifier> ModifierLookup;
        [ReadOnly] public ComponentLookup<ModuleHealth> HealthLookup;
        [ReadOnly] public ComponentLookup<ShipModule> ModuleLookup;
        [NativeDisableParallelForRestriction] public ComponentLookup<CarrierPowerBudget> PowerLookup;

        public void Execute(Entity carrier, ref CarrierModuleStatTotals totals, DynamicBuffer<CarrierModuleSlot> slots)
        {
            var aggregated = new CarrierModuleStatTotals();
            var power = PowerLookup.HasComponent(carrier) ? PowerLookup[carrier] : default;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!ModifierLookup.HasComponent(slot.InstalledModule))
                {
                    continue;
                }

                var modifier = ModifierLookup[slot.InstalledModule];
                var healthScale = 1f;
                byte integrity = 100;
                bool healthBelowThreshold = false;

                if (HealthLookup.HasComponent(slot.InstalledModule))
                {
                    var health = HealthLookup[slot.InstalledModule];
                    integrity = health.Integrity;
                    healthBelowThreshold = integrity <= health.FailureThreshold;
                    healthScale = math.clamp(integrity / 100f, 0f, 1f);
                }

                aggregated.TotalMass += modifier.Mass;
                aggregated.TotalPowerDraw += modifier.PowerDraw * healthScale;
                aggregated.TotalPowerGeneration += modifier.PowerGeneration * healthScale;
                aggregated.TotalCargoCapacity += modifier.CargoCapacity * healthScale;
                aggregated.TotalMiningRate += modifier.MiningRate * healthScale;
                aggregated.TotalRepairRateBonus += modifier.RepairRateBonus * healthScale;

                power.CurrentDraw += modifier.PowerDraw * healthScale;
                power.CurrentGeneration += modifier.PowerGeneration * healthScale;

                if (ModuleLookup.HasComponent(slot.InstalledModule))
                {
                    var module = ModuleLookup[slot.InstalledModule];
                    if (module.State == ModuleState.Destroyed)
                    {
                        aggregated.DestroyedModuleCount++;
                    }
                    else if (module.State == ModuleState.Damaged || healthBelowThreshold)
                    {
                        aggregated.DamagedModuleCount++;
                    }
                }
                else if (integrity == 0)
                {
                    aggregated.DestroyedModuleCount++;
                }
            }

            totals = aggregated;

            if (PowerLookup.HasComponent(carrier))
            {
                power.OverBudget = power.MaxPowerOutput > 0f && power.CurrentDraw > power.MaxPowerOutput;
                PowerLookup[carrier] = power;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CarrierModuleStatAggregationSystem : SystemBase
    {
        private ComponentLookup<ModuleStatModifier> _modifierLookup;
        private ComponentLookup<ModuleHealth> _healthLookup;
        private ComponentLookup<ShipModule> _moduleLookup;
        private ComponentLookup<CarrierPowerBudget> _powerLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
            RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _modifierLookup = GetComponentLookup<ModuleStatModifier>(true);
            _healthLookup = GetComponentLookup<ModuleHealth>(true);
            _moduleLookup = GetComponentLookup<ShipModule>(true);
            _powerLookup = GetComponentLookup<CarrierPowerBudget>(false);

            var aggregationJob = new CarrierModuleStatAggregationJob
            {
                ModifierLookup = _modifierLookup,
                HealthLookup = _healthLookup,
                ModuleLookup = _moduleLookup,
                PowerLookup = _powerLookup
            };

            Dependency = aggregationJob.ScheduleParallel(Dependency);
            Dependency.Complete();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ModuleDegradationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
            RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var scaledDelta = timeState.FixedDeltaTime * timeState.CurrentSpeedMultiplier;

            var repairQueueLookup = GetBufferLookup<ModuleRepairTicket>();

            foreach (var (health, module, degradation, parent, entity) in SystemAPI
                         .Query<RefRW<ModuleHealth>, RefRW<ShipModule>, RefRO<ModuleDegradation>, RefRO<Parent>>()
                         .WithEntityAccess())
            {
                if (module.ValueRO.State == ModuleState.Destroyed)
                {
                    continue;
                }

                var decay = math.max(0f, degradation.ValueRO.PassivePerSecond);
                if (module.ValueRO.State == ModuleState.Active)
                {
                    decay += math.max(0f, degradation.ValueRO.ActivePerSecond);
                }

                var newIntegrity = math.max(0f, health.ValueRO.Integrity - decay * scaledDelta);
                var updatedIntegrity = (byte)newIntegrity;
                health.ValueRW.Integrity = updatedIntegrity;

                if (updatedIntegrity == 0)
                {
                    module.ValueRW.State = ModuleState.Destroyed;
                }

                if (updatedIntegrity <= health.ValueRO.FailureThreshold)
                {
                    module.ValueRW.State = ModuleState.Damaged;

                    if (!health.ValueRO.NeedsRepair)
                    {
                        health.ValueRW.MarkRepairRequested();
                    }

                    if (!repairQueueLookup.HasBuffer(parent.ValueRO.Value))
                    {
                        continue;
                    }

                    var buffer = repairQueueLookup[parent.ValueRO.Value];
                    if (ContainsTicket(buffer, entity))
                    {
                        continue;
                    }

                    buffer.Add(new ModuleRepairTicket
                    {
                        Module = entity,
                        Kind = ModuleRepairKind.Field,
                        Priority = health.ValueRO.RepairPriority,
                        RemainingWork = math.max(0.1f, (100 - updatedIntegrity) * 0.1f)
                    });
                }
            }
        }

        private static bool ContainsTicket(DynamicBuffer<ModuleRepairTicket> buffer, Entity module)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Module == module)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModuleDegradationSystem))]
    public partial class CarrierModuleRepairSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
            RequireForUpdate<RewindState>();
        }

        protected override void OnUpdate()
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var delta = timeState.FixedDeltaTime * timeState.CurrentSpeedMultiplier;
            var healthLookup = GetComponentLookup<ModuleHealth>(false);
            var moduleLookup = GetComponentLookup<ShipModule>(false);

            foreach (var (refitState, carrierEntity) in SystemAPI.Query<RefRO<CarrierRefitState>>().WithEntityAccess())
            {
                if (!EntityManager.HasBuffer<ModuleRepairTicket>(carrierEntity))
                {
                    continue;
                }

                var tickets = EntityManager.GetBuffer<ModuleRepairTicket>(carrierEntity);
                if (tickets.IsEmpty)
                {
                    continue;
                }

                var index = FindHighestPriorityIndex(tickets);
                if (index < 0)
                {
                    continue;
                }

                var ticket = tickets[index];
                var repairRate = ticket.Kind == ModuleRepairKind.Station && refitState.ValueRO.AtRefitFacility
                    ? math.max(refitState.ValueRO.StationRefitRate, refitState.ValueRO.FieldRefitRate)
                    : refitState.ValueRO.FieldRefitRate;

                if (refitState.ValueRO.AtRefitFacility)
                {
                    repairRate = math.max(repairRate, refitState.ValueRO.StationRefitRate);
                }

                if (repairRate <= 0f)
                {
                    continue;
                }

                ticket.RemainingWork -= repairRate * delta;
                if (ticket.RemainingWork <= 0f)
                {
                    if (healthLookup.HasComponent(ticket.Module))
                    {
                        var health = healthLookup[ticket.Module];
                        health.Integrity = 100;
                        health.ClearRepairRequested();
                        healthLookup[ticket.Module] = health;
                    }

                    if (moduleLookup.HasComponent(ticket.Module))
                    {
                        var module = moduleLookup[ticket.Module];
                        if (module.State != ModuleState.Destroyed)
                        {
                            module.State = ModuleState.Standby;
                            moduleLookup[ticket.Module] = module;
                        }
                    }

                    tickets.RemoveAtSwapBack(index);
                }
                else
                {
                    tickets[index] = ticket;
                }
            }
        }

        private static int FindHighestPriorityIndex(DynamicBuffer<ModuleRepairTicket> tickets)
        {
            var bestIndex = -1;
            byte bestPriority = 0;

            for (int i = 0; i < tickets.Length; i++)
            {
                if (bestIndex == -1 || tickets[i].Priority > bestPriority)
                {
                    bestIndex = i;
                    bestPriority = tickets[i].Priority;
                }
            }

            return bestIndex;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierModuleRepairSystem))]
    public partial class CarrierModuleRefitSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
            RequireForUpdate<RewindState>();
        }

        protected override void OnUpdate()
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var delta = timeState.FixedDeltaTime * timeState.CurrentSpeedMultiplier;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var powerLookup = GetComponentLookup<CarrierPowerBudget>(true);

            foreach (var (refitState, carrierEntity) in SystemAPI.Query<RefRO<CarrierRefitState>>().WithEntityAccess())
            {
                if (!EntityManager.HasBuffer<CarrierModuleSlot>(carrierEntity) || !EntityManager.HasBuffer<CarrierModuleRefitRequest>(carrierEntity))
                {
                    continue;
                }

                var slots = EntityManager.GetBuffer<CarrierModuleSlot>(carrierEntity);
                var requests = EntityManager.GetBuffer<CarrierModuleRefitRequest>(carrierEntity);

                if (requests.IsEmpty)
                {
                    continue;
                }

                var refitRate = refitState.ValueRO.AtRefitFacility
                    ? math.max(refitState.ValueRO.StationRefitRate, refitState.ValueRO.FieldRefitRate)
                    : refitState.ValueRO.FieldRefitRate;

                if (refitRate <= 0f)
                {
                    continue;
                }

                for (int i = requests.Length - 1; i >= 0; i--)
                {
                    var request = requests[i];
                    if (request.RequiresStation && !refitState.ValueRO.AtRefitFacility)
                    {
                        continue;
                    }

                    if (powerLookup.HasComponent(carrierEntity))
                    {
                        var power = powerLookup[carrierEntity];
                        if (power.MaxPowerOutput > 0f && power.CurrentDraw > power.MaxPowerOutput)
                        {
                            // Block refit when over budget to avoid growing the deficit.
                            continue;
                        }
                    }

                    request.WorkRemaining -= refitRate * delta;

                    if (request.WorkRemaining > 0f)
                    {
                        requests[i] = request;
                        continue;
                    }

                    var newModule = Entity.Null;
                    if (request.NewModulePrefab != Entity.Null)
                    {
                        newModule = EntityManager.Instantiate(request.NewModulePrefab);
                        ecb.AddComponent(newModule, new Parent { Value = carrierEntity });
                    }

                    ReplaceSlotModule(slots, request.SlotIndex, request.ExistingModule, newModule, ref ecb);
                    requests.RemoveAtSwapBack(i);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static void ReplaceSlotModule(DynamicBuffer<CarrierModuleSlot> slots, byte slotIndex,
            Entity existing, Entity replacement, ref EntityCommandBuffer ecb)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].SlotIndex != slotIndex)
                {
                    continue;
                }

                var slot = slots[i];

                if (existing != Entity.Null)
                {
                    ecb.DestroyEntity(existing);
                }

                slot.InstalledModule = replacement;
                slots[i] = slot;
                return;
            }
        }
    }
}
