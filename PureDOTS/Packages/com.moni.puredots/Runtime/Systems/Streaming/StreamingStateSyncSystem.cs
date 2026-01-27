using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine.Assertions;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Synchronises section state with the underlying SceneSystem status.
    /// </summary>
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(StreamingLoaderSystem))]
    public partial struct StreamingStateSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StreamingCoordinator>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var coordinator = SystemAPI.GetSingleton<StreamingCoordinator>();
            Assert.AreEqual(coordinator.WorldSequenceNumber, (uint)state.WorldUnmanaged.SequenceNumber,
                "[PureDOTS] StreamingCoordinator belongs to a different world.");

            uint currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            var worldUnmanaged = state.WorldUnmanaged;

            foreach (var (sectionState, runtime) in SystemAPI.Query<RefRW<StreamingSectionState>, RefRW<StreamingSectionRuntime>>())
            {
                var sceneEntity = runtime.ValueRO.SceneEntity;
                if (sceneEntity == Entity.Null || !state.EntityManager.Exists(sceneEntity))
                {
                    if (sceneEntity != Entity.Null)
                    {
                        runtime.ValueRW.SceneEntity = Entity.Null;
                    }

                    switch (sectionState.ValueRO.Status)
                    {
                        case StreamingSectionStatus.Loading:
                        case StreamingSectionStatus.Loaded:
                        case StreamingSectionStatus.Unloading:
                            sectionState.ValueRW.Status = StreamingSectionStatus.Unloaded;
                            break;
                    }

                    continue;
                }

                var status = SceneSystem.GetSceneStreamingState(worldUnmanaged, sceneEntity);
                switch (status)
                {
                    case SceneSystem.SceneStreamingState.LoadedSuccessfully:
                    case SceneSystem.SceneStreamingState.LoadedSectionEntities:
                    case SceneSystem.SceneStreamingState.LoadedWithSectionErrors:
                        if (sectionState.ValueRO.Status != StreamingSectionStatus.QueuedUnload)
                        {
                            sectionState.ValueRW.Status = StreamingSectionStatus.Loaded;
                        }
                        break;

                    case SceneSystem.SceneStreamingState.Loading:
                        if (sectionState.ValueRO.Status != StreamingSectionStatus.QueuedUnload)
                        {
                            sectionState.ValueRW.Status = StreamingSectionStatus.Loading;
                        }
                        break;

                    case SceneSystem.SceneStreamingState.Unloading:
                        sectionState.ValueRW.Status = StreamingSectionStatus.Unloading;
                        break;

                    case SceneSystem.SceneStreamingState.Unloaded:
                    default:
                        sectionState.ValueRW.Status = StreamingSectionStatus.Unloaded;
                        runtime.ValueRW.SceneEntity = Entity.Null;
                        break;
                    case SceneSystem.SceneStreamingState.FailedLoadingSceneHeader:
                        sectionState.ValueRW.Status = StreamingSectionStatus.Error;
                        runtime.ValueRW.SceneEntity = Entity.Null;
                        break;
                }

                sectionState.ValueRW.LastSeenTick = currentTick;
            }
        }
    }
}
