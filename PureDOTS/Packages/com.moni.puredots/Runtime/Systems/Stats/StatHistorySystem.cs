using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Stats
{
    /// <summary>
    /// System that records stat history samples for rewind compatibility.
    /// Samples are taken at configurable intervals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StatXPAccumulationSystem))]
    public partial struct StatHistorySystem : ISystem
    {
        /// <summary>
        /// Interval in ticks between stat history samples.
        /// </summary>
        private const uint SampleInterval = 60; // Sample every 60 ticks (1 second at 60 FPS)

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Only sample at intervals
            if (currentTick % SampleInterval != 0)
            {
                return;
            }

            foreach (var (stats, historyBuffer, entity) in SystemAPI.Query<RefRO<IndividualStats>, DynamicBuffer<StatHistorySample>>()
                .WithEntityAccess())
            {
                var statsData = stats.ValueRO;
                var buffer = historyBuffer;
                buffer.Add(new StatHistorySample
                {
                    Tick = currentTick,
                    Command = statsData.Command,
                    Tactics = statsData.Tactics,
                    Logistics = statsData.Logistics,
                    Diplomacy = statsData.Diplomacy,
                    Engineering = statsData.Engineering,
                    Resolve = statsData.Resolve,
                    GeneralXP = 0f // Will be populated if entity has PhysiqueFinesseWill
                });

                // If entity has PhysiqueFinesseWill, also record GeneralXP
                if (SystemAPI.HasComponent<PhysiqueFinesseWill>(entity))
                {
                    var pfw = SystemAPI.GetComponent<PhysiqueFinesseWill>(entity);
                    var lastSample = buffer[buffer.Length - 1];
                    lastSample.GeneralXP = pfw.GeneralXP;
                    buffer[buffer.Length - 1] = lastSample;
                }

            }

        }
    }
}

