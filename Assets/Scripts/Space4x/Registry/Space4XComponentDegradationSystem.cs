using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Applies gradual degradation and hazard damage to module health.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GameplayFixedStepSyncSystem))]
    public partial struct Space4XComponentDegradationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GameplayFixedStep>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = SystemAPI.GetSingleton<GameplayFixedStep>().FixedDeltaTime;

            foreach (var health in SystemAPI.Query<RefRW<ModuleHealth>>().WithNone<HazardDamageEvent>())
            {
                ApplyDegradation(ref health.ValueRW, deltaTime, 0f);
            }

            foreach (var (health, hazardEvents) in SystemAPI.Query<RefRW<ModuleHealth>, DynamicBuffer<HazardDamageEvent>>())
            {
                var damage = 0f;
                for (var i = 0; i < hazardEvents.Length; i++)
                {
                    damage += math.max(0f, hazardEvents[i].Amount);
                }

                hazardEvents.Clear();
                ApplyDegradation(ref health.ValueRW, deltaTime, damage);
            }
        }

        private static void ApplyDegradation(ref ModuleHealth health, float deltaTime, float incomingDamage)
        {
            var degradation = math.max(0f, health.DegradationPerSecond) * math.max(0f, deltaTime);
            var newHealth = health.CurrentHealth - degradation - math.max(0f, incomingDamage);
            newHealth = math.min(newHealth, health.MaxHealth);

            if (newHealth <= 0f)
            {
                health.CurrentHealth = 0f;
                health.Failed = 1;
                return;
            }

            health.CurrentHealth = newHealth;
            health.Failed = 0;
        }
    }
}
