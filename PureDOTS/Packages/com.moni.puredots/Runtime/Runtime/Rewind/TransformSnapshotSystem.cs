using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rewind;

namespace PureDOTS.Runtime.Rewind
{
    /// <summary>
    /// Captures/Restores LocalTransform snapshots based on RewindState mode.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Time.TimeSystem))]
    public partial struct TransformSnapshotSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewind = SystemAPI.GetSingleton<RewindState>();
            var timeState = SystemAPI.GetSingleton<TimeState>();
            int currentTick = (int)timeState.Tick;

            if (rewind.Mode == RewindMode.Rewind)
            {
                // Restore snapshots
                foreach (var (buffer, lt) in SystemAPI
                             .Query<DynamicBuffer<TransformSnapshot>, RefRW<LocalTransform>>())
                {
                    Restore(buffer, ref lt.ValueRW, currentTick);
                }
            }
            else
            {
                // Capture snapshots (Play/Step/Paused write current)
                foreach (var (buffer, lt) in SystemAPI
                             .Query<DynamicBuffer<TransformSnapshot>, RefRO<LocalTransform>>())
                {
                    Capture(buffer, in lt.ValueRO, currentTick, rewind.MaxHistoryTicks);
                }
            }
        }

        private static void Capture(DynamicBuffer<TransformSnapshot> buffer,
                                    in LocalTransform lt,
                                    int tick,
                                    int capacity)
        {
            var snap = new TransformSnapshot
            {
                Tick = tick,
                Position = lt.Position,
                Rotation = lt.Rotation,
                Scale = lt.Scale
            };

            if (buffer.Length < capacity)
            {
                buffer.Add(snap);
            }
            else
            {
                int index = tick % math.max(1, capacity);
                if (index < buffer.Length)
                    buffer[index] = snap;
                else
                    buffer.Add(snap);
            }
        }

        private static void Restore(DynamicBuffer<TransformSnapshot> buffer,
                                    ref LocalTransform lt,
                                    int tick)
        {
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick <= tick)
                {
                    lt.Position = buffer[i].Position;
                    lt.Rotation = buffer[i].Rotation;
                    lt.Scale = buffer[i].Scale;
                    return;
                }
            }
        }

        [BurstCompile] public void OnDestroy(ref SystemState state) { }
    }
}

