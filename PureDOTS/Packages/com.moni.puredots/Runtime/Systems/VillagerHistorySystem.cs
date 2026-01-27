using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Villager;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Records villager state history for deterministic replay and rewind.
    /// Captures job start/end, need events, and morale shifts at configurable cadence.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct VillagerHistorySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HistorySettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var historySettings = SystemAPI.GetSingleton<HistorySettings>();

            // Only record during Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Determine stride based on history tier
            var strideTicks = GetStrideTicks(historySettings);
            if (timeState.Tick % strideTicks != 0)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Record job history samples
            foreach (var (job, ticket, progress, transform, historyBuffer, entity) in SystemAPI.Query<
                         RefRO<VillagerJob>,
                         RefRO<VillagerJobTicket>,
                         RefRO<VillagerJobProgress>,
                         RefRO<LocalTransform>,
                         DynamicBuffer<VillagerJobHistorySample>>()
                         .WithEntityAccess())
            {
                var sample = new VillagerJobHistorySample
                {
                    Tick = timeState.Tick,
                    TicketId = ticket.ValueRO.TicketId,
                    Phase = job.ValueRO.Phase,
                    Gathered = progress.ValueRO.Gathered,
                    Delivered = progress.ValueRO.Delivered,
                    TargetPosition = transform.ValueRO.Position
                };

                historyBuffer.Add(sample);

                // Prune old samples based on history settings
                PruneOldSamples(historyBuffer, timeState.Tick, historySettings);
            }

            // Record need/mood history samples (if buffer exists)
            foreach (var (needs, mood, historyBuffer, entity) in SystemAPI.Query<
                         RefRO<VillagerNeeds>,
                         RefRO<VillagerMood>,
                         DynamicBuffer<VillagerHistorySample>>()
                         .WithEntityAccess())
            {
                var sample = new VillagerHistorySample
                {
                    Tick = timeState.Tick,
                    Health = needs.ValueRO.Health,
                    Hunger = needs.ValueRO.HungerFloat,
                    Energy = needs.ValueRO.EnergyFloat,
                    Morale = needs.ValueRO.MoraleFloat,
                    Mood = mood.ValueRO.Mood,
                    Wellbeing = mood.ValueRO.Wellbeing
                };

                historyBuffer.Add(sample);

                // Prune old samples
                PruneOldSamples(historyBuffer, timeState.Tick, historySettings);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static uint GetStrideTicks(in HistorySettings settings)
        {
            // Default to critical stride (1 second) for villagers
            var strideSeconds = settings.CriticalStrideSeconds > 0f
                ? settings.CriticalStrideSeconds
                : settings.DefaultStrideSeconds;
            var ticksPerSecond = settings.DefaultTicksPerSecond > 0f
                ? settings.DefaultTicksPerSecond
                : 60f;
            return (uint)math.max(1, math.round(strideSeconds * ticksPerSecond));
        }

        private static void PruneOldSamples(DynamicBuffer<VillagerJobHistorySample> buffer, uint currentTick, in HistorySettings settings)
        {
            // Remove samples older than horizon
            var horizonTicks = (uint)math.round(settings.DefaultHorizonSeconds * settings.DefaultTicksPerSecond);
            var cutoffTick = currentTick >= horizonTicks ? currentTick - horizonTicks : 0;

            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static void PruneOldSamples(DynamicBuffer<VillagerHistorySample> buffer, uint currentTick, in HistorySettings settings)
        {
            // Remove samples older than horizon
            var horizonTicks = (uint)math.round(settings.DefaultHorizonSeconds * settings.DefaultTicksPerSecond);
            var cutoffTick = currentTick >= horizonTicks ? currentTick - horizonTicks : 0;

            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick < cutoffTick)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private static uint GetTickFromSample(VillagerJobHistorySample sample)
        {
            return sample.Tick;
        }

        private static uint GetTickFromSample(VillagerHistorySample sample)
        {
            return sample.Tick;
        }
    }

}

