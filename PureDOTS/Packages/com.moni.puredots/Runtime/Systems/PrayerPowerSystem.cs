using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages prayer power regeneration and consumption.
    /// Updates mana pool based on sources and active consumers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    public partial struct PrayerPowerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Ensure PrayerPower singleton exists (should be initialized by bootstrap, but create if missing)
            if (!SystemAPI.HasSingleton<PrayerPower>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new PrayerPower
                {
                    CurrentMana = 100f,
                    MaxMana = 100f,
                    RegenRate = 1f,
                    LastRegenTick = timeState.Tick
                });
                // Return early on first frame to allow systems to initialize
                if (timeState.Tick == 0)
                {
                    return;
                }
            }

            var prayerPower = SystemAPI.GetSingletonRW<PrayerPower>();
            var deltaTime = timeState.FixedDeltaTime;

            // Collect generation from sources
            float totalGeneration = 0f;
            foreach (var (source, transform) in SystemAPI.Query<RefRO<PrayerPowerSource>, RefRO<LocalTransform>>())
            {
                if (source.ValueRO.IsActive)
                {
                    totalGeneration += source.ValueRO.GenerationRate;
                }
            }

            // Collect consumption from active consumers
            float totalConsumption = 0f;
            foreach (var consumer in SystemAPI.Query<RefRO<PrayerPowerConsumer>>())
            {
                if (consumer.ValueRO.RequiresPower)
                {
                    totalConsumption += consumer.ValueRO.ConsumptionRate;
                }
            }

            // Update mana
            var netChange = (totalGeneration - totalConsumption) * deltaTime;
            var newMana = math.clamp(prayerPower.ValueRO.CurrentMana + netChange, 0f, prayerPower.ValueRO.MaxMana);
            prayerPower.ValueRW.CurrentMana = newMana;
            prayerPower.ValueRW.LastRegenTick = timeState.Tick;
        }
    }
}

