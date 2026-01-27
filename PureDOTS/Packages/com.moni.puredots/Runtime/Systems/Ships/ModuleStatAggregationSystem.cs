using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Skills;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Aggregates installed module stats per carrier and applies skill-based modifiers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierModuleBootstrapSystem))]
    public partial struct ModuleStatAggregationSystem : ISystem
    {
        private ComponentLookup<SkillSet> _skillLookup;
        private ComponentLookup<ModuleHealth> _moduleHealthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierModuleSlot>();
            state.RequireForUpdate<TimeState>();
            _skillLookup = state.GetComponentLookup<SkillSet>(true);
            _moduleHealthLookup = state.GetComponentLookup<ModuleHealth>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            _skillLookup.Update(ref state);
            _moduleHealthLookup.Update(ref state);

            foreach (var (slots, aggregateRef, entity) in SystemAPI
                         .Query<DynamicBuffer<CarrierModuleSlot>, RefRW<CarrierModuleAggregate>>()
                         .WithEntityAccess())
            {
                float totalMass = 0f;
                float totalPower = 0f;
                float offense = 0f;
                float defense = 0f;
                float utility = 0f;
                float efficiencySum = 0f;
                var moduleCount = 0;
                byte degradedCount = 0;
                byte failedCount = 0;

                for (int i = 0; i < slots.Length; i++)
                {
                    var moduleEntity = slots[i].InstalledModule;
                    if (moduleEntity == Entity.Null || !state.EntityManager.HasComponent<ShipModule>(moduleEntity))
                    {
                        continue;
                    }

                    var module = state.EntityManager.GetComponentData<ShipModule>(moduleEntity);
                    float efficiency = math.clamp(module.EfficiencyPercent / 100f, 0f, 2f);

                    if (_moduleHealthLookup.HasComponent(moduleEntity))
                    {
                        var health = _moduleHealthLookup[moduleEntity];
                        ModuleMaintenanceUtility.ResolveState(ref health);
                        _moduleHealthLookup[moduleEntity] = health;

                        if (health.State == ModuleHealthState.Destroyed)
                        {
                            failedCount++;
                            continue;
                        }

                        if (health.State == ModuleHealthState.Failed)
                        {
                            failedCount++;
                            efficiency = 0f;
                        }
                        else if (health.State == ModuleHealthState.Degraded)
                        {
                            degradedCount++;
                            var ratio = health.MaxHealth > 0f ? math.saturate(health.Health / health.MaxHealth) : 0f;
                            efficiency *= ratio;
                        }
                    }

                    totalMass += module.Mass;
                    totalPower += module.PowerRequired;
                    offense += module.OffenseRating * efficiency;
                    defense += module.DefenseRating * efficiency;
                    utility += module.UtilityRating * efficiency;
                    efficiencySum += efficiency;
                    moduleCount++;
                }

                var aggregate = aggregateRef.ValueRO;
                aggregate.TotalMass = totalMass;
                aggregate.TotalPowerRequired = totalPower;
                aggregate.OffenseRating = offense;
                aggregate.DefenseRating = defense;
                aggregate.UtilityRating = utility;
                aggregate.EfficiencyScalar = moduleCount > 0 ? efficiencySum / moduleCount : 0f;
                aggregate.DegradedCount = degradedCount;
                aggregate.FailedCount = failedCount;
                aggregate.LastUpdateTick = timeState.Tick;

                if (_skillLookup.HasComponent(entity))
                {
                    var skills = _skillLookup[entity];
                    var engineeringLevel = skills.GetLevel(SkillId.ShipEngineering);
                    var engineeringBonus = math.min(0.3f, engineeringLevel * 0.005f);
                    aggregate.EfficiencyScalar *= 1f + engineeringBonus;
                    aggregate.TotalPowerRequired *= math.max(0.5f, 1f - engineeringBonus);
                }

                aggregateRef.ValueRW = aggregate;
            }
        }
    }
}
