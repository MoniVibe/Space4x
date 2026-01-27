using PureDOTS.Runtime.Components;
using PureDOTS.Input;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Records input snapshots (state + edges) per tick to a blob or buffer for deterministic replay.
    /// Guards recording with config flag to avoid overhead when not needed.
    /// Note: Runs in HistorySystemGroup, which executes after SimulationSystemGroup where CopyInputToEcsSystem runs.
    /// </summary>
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct InputRecordingSystem : ISystem
    {

        private Entity _historyEntity;
        private uint _cachedHorizonTicks;

        public void OnCreate(ref SystemState state)
        {
            _historyEntity = Entity.Null;
            _cachedHorizonTicks = 0;
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HistorySettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out RewindState rewindState))
            {
                return;
            }
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out HistorySettings historySettings))
            {
                return;
            }
            if (!historySettings.EnableInputRecording)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out TimeState timeState))
            {
                return;
            }
            uint currentTick = timeState.Tick;
            var historyEntity = EnsureHistoryEntity(ref state);

            var historyState = state.EntityManager.GetComponentData<InputHistoryState>(historyEntity);
            var snapshotBuffer = state.EntityManager.GetBuffer<InputSnapshotRecord>(historyEntity);
            var handEdgeBuffer = state.EntityManager.GetBuffer<HandInputEdge>(historyEntity);
            var cameraEdgeBuffer = state.EntityManager.GetBuffer<CameraInputEdge>(historyEntity);

            uint horizon = historyState.HorizonTicks != 0 ? historyState.HorizonTicks : ComputeHorizonTicks(timeState, historySettings);
            historyState.HorizonTicks = horizon;

            uint minTick = currentTick > horizon ? currentTick - horizon : 0;
            PruneHistory(minTick, snapshotBuffer, handEdgeBuffer, cameraEdgeBuffer);

            var record = new InputSnapshotRecord
            {
                Tick = currentTick,
                HandInput = default,
                CameraInput = default,
                HandEdgeStart = handEdgeBuffer.Length,
                HandEdgeCount = 0,
                CameraEdgeStart = cameraEdgeBuffer.Length,
                CameraEdgeCount = 0
            };

            foreach (var (handInput, handEdges, handEntity) in SystemAPI
                         .Query<RefRO<DivineHandInput>, DynamicBuffer<HandInputEdge>>()
                         .WithEntityAccess())
            {
                record.HandInput = handInput.ValueRO;
                record.HandEdgeStart = handEdgeBuffer.Length;
                record.HandEdgeCount = handEdges.Length;
                for (int i = 0; i < handEdges.Length; i++)
                {
                    handEdgeBuffer.Add(handEdges[i]);
                }
                break; // Only record first hand entity
            }

            foreach (var (cameraInput, cameraEdges, cameraEntity) in SystemAPI
                         .Query<RefRO<CameraInputState>, DynamicBuffer<CameraInputEdge>>()
                         .WithEntityAccess())
            {
                record.CameraInput = cameraInput.ValueRO;
                record.CameraEdgeStart = cameraEdgeBuffer.Length;
                record.CameraEdgeCount = cameraEdges.Length;
                for (int i = 0; i < cameraEdges.Length; i++)
                {
                    cameraEdgeBuffer.Add(cameraEdges[i]);
                }
                break; // Only record first camera entity
            }

            snapshotBuffer.Add(record);
            historyState.LastRecordedTick = currentTick;
            state.EntityManager.SetComponentData(historyEntity, historyState);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private Entity EnsureHistoryEntity(ref SystemState state)
        {
            if (_historyEntity != Entity.Null && state.EntityManager.Exists(_historyEntity))
            {
                return _historyEntity;
            }

            _historyEntity = state.EntityManager.CreateEntity(typeof(InputHistoryState));
            state.EntityManager.AddBuffer<InputSnapshotRecord>(_historyEntity);
            state.EntityManager.AddBuffer<HandInputEdge>(_historyEntity);
            state.EntityManager.AddBuffer<CameraInputEdge>(_historyEntity);
            state.EntityManager.SetComponentData(_historyEntity, new InputHistoryState
            {
                HorizonTicks = _cachedHorizonTicks,
                LastRecordedTick = 0
            });

            return _historyEntity;
        }

        private uint ComputeHorizonTicks(in TimeState timeState, in HistorySettings historySettings)
        {
            if (_cachedHorizonTicks != 0)
            {
                return _cachedHorizonTicks;
            }

            float ticksPerSecond = 1f / math.max(0.0001f, timeState.FixedDeltaTime);
            uint desired = (uint)math.max(1f, math.round(ticksPerSecond * 3f));
            desired = math.max(desired, (uint)math.round(historySettings.DefaultTicksPerSecond * 3f));

            _cachedHorizonTicks = desired + 4u;
            return _cachedHorizonTicks;
        }

        private static void PruneHistory(
            uint minTick,
            DynamicBuffer<InputSnapshotRecord> snapshots,
            DynamicBuffer<HandInputEdge> handEdges,
            DynamicBuffer<CameraInputEdge> cameraEdges)
        {
            if (snapshots.Length == 0)
            {
                return;
            }

            // Fast path when snapshots already in range
            if (snapshots[0].Tick >= minTick)
            {
                return;
            }

            var prunedSnapshots = new NativeList<InputSnapshotRecord>(snapshots.Length, Allocator.Temp);
            var prunedHand = new NativeList<HandInputEdge>(handEdges.Length, Allocator.Temp);
            var prunedCamera = new NativeList<CameraInputEdge>(cameraEdges.Length, Allocator.Temp);

            for (int i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot.Tick < minTick)
                {
                    continue;
                }

                snapshot.HandEdgeStart = prunedHand.Length;
                for (int h = 0; h < snapshot.HandEdgeCount; h++)
                {
                    prunedHand.Add(handEdges[snapshot.HandEdgeStart + h]);
                }

                snapshot.CameraEdgeStart = prunedCamera.Length;
                for (int c = 0; c < snapshot.CameraEdgeCount; c++)
                {
                    prunedCamera.Add(cameraEdges[snapshot.CameraEdgeStart + c]);
                }

                prunedSnapshots.Add(snapshot);
            }

            snapshots.Clear();
            handEdges.Clear();
            cameraEdges.Clear();

            for (int i = 0; i < prunedSnapshots.Length; i++)
            {
                snapshots.Add(prunedSnapshots[i]);
            }

            for (int i = 0; i < prunedHand.Length; i++)
            {
                handEdges.Add(prunedHand[i]);
            }

            for (int i = 0; i < prunedCamera.Length; i++)
            {
                cameraEdges.Add(prunedCamera[i]);
            }

            prunedSnapshots.Dispose();
            prunedHand.Dispose();
            prunedCamera.Dispose();
        }
    }
}

