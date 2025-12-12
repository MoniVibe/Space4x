using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates module stat multipliers for carriers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCarrierModuleRefitSystem))]
    public partial struct Space4XModuleStatAggregationSystem : ISystem
    {
        private ComponentLookup<ModuleStatModifier> _modifierLookup;
        private ComponentLookup<ModuleHealth> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _modifierLookup = state.GetComponentLookup<ModuleStatModifier>(true);
            _healthLookup = state.GetComponentLookup<ModuleHealth>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _modifierLookup.Update(ref state);
            _healthLookup.Update(ref state);

            foreach (var (slots, aggregate) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>, RefRW<ModuleStatAggregate>>())
            {
                var speed = 1f;
                var cargo = 1f;
                var energy = 1f;
                var refit = 1f;
                var repair = 1f;
                var active = 0;

                for (var i = 0; i < slots.Length; i++)
                {
                    var module = slots[i].CurrentModule;
                    if (module == Entity.Null || !_modifierLookup.HasComponent(module))
                    {
                        continue;
                    }

                    var weight = 1f;
                    if (_healthLookup.HasComponent(module))
                    {
                        var health = _healthLookup[module];
                        if (health.CurrentHealth <= 0f || health.Failed != 0)
                        {
                            continue;
                        }

                        if (health.MaxHealth > 0f)
                        {
                            weight = math.saturate(health.CurrentHealth / health.MaxHealth);
                        }
                    }

                    var modifier = _modifierLookup[module];
                    speed *= CombineMultiplier(modifier.SpeedMultiplier, weight);
                    cargo *= CombineMultiplier(modifier.CargoMultiplier, weight);
                    energy *= CombineMultiplier(modifier.EnergyMultiplier, weight);
                    refit *= CombineMultiplier(modifier.RefitRateMultiplier, weight);
                    repair *= CombineMultiplier(modifier.RepairRateMultiplier, weight);
                    active++;
                }

                aggregate.ValueRW = new ModuleStatAggregate
                {
                    SpeedMultiplier = math.max(0f, speed),
                    CargoMultiplier = math.max(0f, cargo),
                    EnergyMultiplier = math.max(0f, energy),
                    RefitRateMultiplier = math.max(0f, refit),
                    RepairRateMultiplier = math.max(0f, repair),
                    ActiveModuleCount = active
                };
            }
        }

        private static float CombineMultiplier(float multiplier, float weight)
        {
            if (multiplier <= 0f)
            {
                return 1f;
            }

            var delta = multiplier - 1f;
            return 1f + delta * math.saturate(weight);
        }
    }
}
