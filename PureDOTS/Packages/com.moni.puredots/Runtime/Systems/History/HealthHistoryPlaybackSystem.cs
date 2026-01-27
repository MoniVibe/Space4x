using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.History
{
    /// <summary>
    /// Plays back health history during rewind playback mode.
    /// Restores health values from history buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    [UpdateAfter(typeof(HealthHistoryRecordSystem))]
    public partial struct HealthHistoryPlaybackSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }
            if (rewindState.Mode != RewindMode.Rewind)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint targetTick = (uint)math.max(0, rewindState.TargetTick);

            // Playback health history for entities with VillagerNeeds
            foreach (var (needsRef, historyBuffer, entity) in SystemAPI.Query<RefRW<VillagerNeeds>, DynamicBuffer<HealthHistorySample>>()
                         .WithAll<RewindableTag, PlaybackGuardTag>()
                         .WithEntityAccess())
            {
                if (historyBuffer.Length == 0)
                {
                    continue;
                }

                // Find closest sample to target tick
                HealthHistorySample? bestSample = null;
                uint bestTickDiff = uint.MaxValue;

                for (int i = 0; i < historyBuffer.Length; i++)
                {
                    var sample = historyBuffer[i];
                    uint tickDiff = sample.Tick > targetTick 
                        ? sample.Tick - targetTick 
                        : targetTick - sample.Tick;
                    
                    if (tickDiff < bestTickDiff)
                    {
                        bestTickDiff = tickDiff;
                        bestSample = sample;
                    }
                }

                if (bestSample.HasValue)
                {
                    var sample = bestSample.Value;
                    var needs = needsRef.ValueRO;
                    needsRef.ValueRW = new VillagerNeeds
                    {
                        Food = needs.Food,
                        Rest = needs.Rest,
                        Sleep = needs.Sleep,
                        GeneralHealth = needs.GeneralHealth,
                        Health = sample.Health,
                        MaxHealth = sample.MaxHealth,
                        Hunger = needs.Hunger,
                        Energy = needs.Energy,
                        Morale = needs.Morale,
                        Temperature = needs.Temperature
                    };
                }
            }
        }
    }
}

