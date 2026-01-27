using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.History
{
    /// <summary>
    /// Plays back transform history during rewind playback mode.
    /// Restores positions and rotations from history buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    [UpdateAfter(typeof(TransformHistoryRecordSystem))]
    public partial struct TransformHistoryPlaybackSystem : ISystem
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

            // Playback transform history for scenario-critical entities
            foreach (var (transformRef, historyBuffer, entity) in SystemAPI.Query<RefRW<LocalTransform>, DynamicBuffer<PositionHistorySample>>()
                         .WithAll<RewindableTag, PlaybackGuardTag>()
                         .WithEntityAccess())
            {
                if (historyBuffer.Length == 0)
                {
                    continue;
                }

                // Find closest sample to target tick
                PositionHistorySample? bestSample = null;
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
                    transformRef.ValueRW = new LocalTransform
                    {
                        Position = sample.Position,
                        Rotation = sample.Rotation,
                        Scale = transformRef.ValueRO.Scale
                    };
                }
            }
        }
    }
}
