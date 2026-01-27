using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using Unity.Entities;
using Unity.Loading;
using Unity.Scenes;
using UnityEngine;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Keeps auxiliary prefab and weak object references in sync with streaming section state.
    /// Mirrors Entities samples while respecting PureDOTS rewind gates.
    /// </summary>
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(StreamingLoaderSystem))]
    public partial struct StreamingSectionContentSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StreamingCoordinator>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var worldUnmanaged = state.WorldUnmanaged;

#if ENABLE_ENTITIES_CONTENT
            foreach (var (sectionState, prefabBuffer) in SystemAPI
                         .Query<RefRO<StreamingSectionState>, DynamicBuffer<StreamingSectionPrefabReference>>())
            {
                bool keepLoaded = ShouldHoldAssets(sectionState.ValueRO.Status);

                for (int i = 0; i < prefabBuffer.Length; i++)
                {
                    ref var element = ref prefabBuffer.ElementAt(i);
                    if (!element.Prefab.IsReferenceValid)
                    {
                        continue;
                    }

                    if (keepLoaded)
                    {
                        if (element.PrefabSceneEntity == Entity.Null)
                        {
                            var sceneEntity = SceneSystem.LoadPrefabAsync(worldUnmanaged, element.Prefab);
                            if (sceneEntity == Entity.Null)
                            {
                                Debug.LogError("[PureDOTS] Failed to start loading prefab reference for streaming section.");
                                continue;
                            }

                            element.PrefabSceneEntity = sceneEntity;
                        }
                    }
                    else if (element.PrefabSceneEntity != Entity.Null)
                    {
                        SceneSystem.UnloadScene(worldUnmanaged, element.PrefabSceneEntity);
                        element.PrefabSceneEntity = Entity.Null;
                    }
                }
            }
#endif

#if ENABLE_ENTITIES_CONTENT
            foreach (var (sectionState, weakBuffer) in SystemAPI
                         .Query<RefRO<StreamingSectionState>, DynamicBuffer<StreamingSectionWeakGameObjectReference>>())
            {
                bool keepLoaded = ShouldHoldAssets(sectionState.ValueRO.Status);

                for (int i = 0; i < weakBuffer.Length; i++)
                {
                    ref var element = ref weakBuffer.ElementAt(i);
                    if (!element.Reference.IsReferenceValid)
                    {
                        continue;
                    }

                    if (keepLoaded)
                    {
                        if (element.Reference.LoadingStatus == ObjectLoadingStatus.None)
                        {
                            element.Reference.LoadAsync();
                        }
                    }
                    else
                    {
                        if (element.Reference.LoadingStatus != ObjectLoadingStatus.None)
                        {
                            element.Reference.Release();
                        }
                    }
                }
            }
#endif
        }

        private static bool ShouldHoldAssets(StreamingSectionStatus status)
        {
            return status is StreamingSectionStatus.Loaded
                or StreamingSectionStatus.Loading
                or StreamingSectionStatus.QueuedLoad;
        }
    }
}

