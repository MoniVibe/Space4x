using PureDOTS.Runtime.Components;
using PureDOTS.Input;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Feeds recorded input snapshots into ECS instead of live input during playback mode.
    /// Integrates with RewindState to enable deterministic replay.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(CopyInputToEcsSystem))]
    public partial struct InputPlaybackSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode == RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<InputHistoryState>(out var historyEntity))
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint targetTick = timeState.Tick;

            var snapshots = SystemAPI.GetBuffer<InputSnapshotRecord>(historyEntity);
            var handEdges = SystemAPI.GetBuffer<HandInputEdge>(historyEntity);
            var cameraEdges = SystemAPI.GetBuffer<CameraInputEdge>(historyEntity);

            if (!TryGetSnapshotForTick(snapshots, targetTick, out var snapshot))
            {
                return;
            }

            foreach (var (handInputRef, handEntity) in SystemAPI.Query<RefRW<DivineHandInput>>().WithEntityAccess())
            {
                handInputRef.ValueRW = snapshot.HandInput;

                if (state.EntityManager.HasBuffer<HandInputEdge>(handEntity))
                {
                    var edges = state.EntityManager.GetBuffer<HandInputEdge>(handEntity);
                    edges.Clear();
                    for (int i = 0; i < snapshot.HandEdgeCount; i++)
                    {
                        edges.Add(handEdges[snapshot.HandEdgeStart + i]);
                    }
                }
                else if (snapshot.HandEdgeCount > 0)
                {
                    var edges = state.EntityManager.AddBuffer<HandInputEdge>(handEntity);
                    for (int i = 0; i < snapshot.HandEdgeCount; i++)
                    {
                        edges.Add(handEdges[snapshot.HandEdgeStart + i]);
                    }
                }
            }

            foreach (var (cameraInputRef, cameraEntity) in SystemAPI.Query<RefRW<CameraInputState>>().WithEntityAccess())
            {
                cameraInputRef.ValueRW = snapshot.CameraInput;

                if (state.EntityManager.HasBuffer<CameraInputEdge>(cameraEntity))
                {
                    var edges = state.EntityManager.GetBuffer<CameraInputEdge>(cameraEntity);
                    edges.Clear();
                    for (int i = 0; i < snapshot.CameraEdgeCount; i++)
                    {
                        edges.Add(cameraEdges[snapshot.CameraEdgeStart + i]);
                    }
                }
                else if (snapshot.CameraEdgeCount > 0)
                {
                    var edges = state.EntityManager.AddBuffer<CameraInputEdge>(cameraEntity);
                    for (int i = 0; i < snapshot.CameraEdgeCount; i++)
                    {
                        edges.Add(cameraEdges[snapshot.CameraEdgeStart + i]);
                    }
                }
            }
        }

        private static bool TryGetSnapshotForTick(
            DynamicBuffer<InputSnapshotRecord> snapshots,
            uint targetTick,
            out InputSnapshotRecord snapshot)
        {
            snapshot = default;
            if (snapshots.Length == 0)
            {
                return false;
            }

            for (int i = snapshots.Length - 1; i >= 0; i--)
            {
                if (snapshots[i].Tick <= targetTick)
                {
                    snapshot = snapshots[i];
                    return true;
                }
            }

            return false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
