using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.History
{
    /// <summary>
    /// Records transform history for scenario-critical entities (villagers, ships, villages, carriers).
    /// Runs during Record mode only.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct TransformHistoryRecordSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var historySettings = SystemAPI.GetSingleton<HistorySettings>();

            // Record stride: every N ticks based on history tier
            var strideTicks = (uint)math.max(1f, historySettings.DefaultStrideSeconds / math.max(0.0001f, timeState.FixedDeltaTime));
            if (timeState.Tick % strideTicks != 0)
            {
                return;
            }

            // Record transform history for scenario-critical entities
            foreach (var (transform, historyBuffer, entity) in SystemAPI.Query<RefRO<LocalTransform>, DynamicBuffer<PositionHistorySample>>()
                         .WithAll<RewindableTag>()
                         .WithEntityAccess())
            {
                var sample = new PositionHistorySample
                {
                    Tick = timeState.Tick,
                    Position = transform.ValueRO.Position,
                    Rotation = transform.ValueRO.Rotation
                };

                PruneOldSamples(historyBuffer, timeState.Tick, historySettings.DefaultHorizonSeconds, timeState.FixedDeltaTime);
                historyBuffer.Add(sample);
            }
        }

        private static void PruneOldSamples(
            DynamicBuffer<PositionHistorySample> buffer,
            uint currentTick,
            float horizonSeconds,
            float fixedDt)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            var maxHistoryTicks = (uint)math.max(1f, horizonSeconds / math.max(0.0001f, fixedDt));
            var cutoffTick = currentTick > maxHistoryTicks ? currentTick - maxHistoryTicks : 0u;

            var firstValidIndex = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Tick >= cutoffTick)
                {
                    firstValidIndex = i;
                    break;
                }
            }

            if (firstValidIndex > 0)
            {
                buffer.RemoveRange(0, firstValidIndex);
            }
        }
    }
}
