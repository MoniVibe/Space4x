using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.Assertions;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Executes the buffered streaming commands via <see cref="SceneSystem"/>.
    /// </summary>
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(StreamingScannerSystem))]
    public partial struct StreamingLoaderSystem : ISystem
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

            var coordinatorEntity = SystemAPI.GetSingletonEntity<StreamingCoordinator>();
            var coordinator = SystemAPI.GetComponent<StreamingCoordinator>(coordinatorEntity);
            Assert.AreEqual(coordinator.WorldSequenceNumber, (uint)state.WorldUnmanaged.SequenceNumber,
                "[PureDOTS] StreamingCoordinator belongs to a different world.");
            var commands = state.EntityManager.GetBuffer<StreamingSectionCommand>(coordinatorEntity);
            var statsHandle = SystemAPI.GetComponentRW<StreamingStatistics>(coordinatorEntity);
            var stats = statsHandle.ValueRO;

            var entityManager = state.EntityManager;
            bool instantCompletion = entityManager.HasComponent<StreamingTestDriver>(coordinatorEntity) &&
                                     entityManager.GetComponentData<StreamingTestDriver>(coordinatorEntity).InstantCompletion;

            if (commands.Length == 0)
            {
                statsHandle.ValueRW = stats;
                return;
            }

            uint currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            var worldUnmanaged = state.WorldUnmanaged;

            int activeLoads = 0;
            foreach (var sectionState in SystemAPI.Query<RefRO<StreamingSectionState>>())
            {
                if (sectionState.ValueRO.Status == StreamingSectionStatus.Loading)
                {
                    activeLoads++;
                }
            }

            int maxConcurrentLoads = math.max(0, coordinator.MaxConcurrentLoads);
            int maxLoadsPerTick = math.max(0, coordinator.MaxLoadsPerTick);
            int maxUnloadsPerTick = math.max(0, coordinator.MaxUnloadsPerTick);

            int loadsIssued = 0;
            int unloadsIssued = 0;

            var array = commands.AsNativeArray();
            if (array.Length > stats.PeakPendingCommands)
            {
                stats.PeakPendingCommands = array.Length;
            }
            if (array.Length > 1)
            {
                array.Sort(new StreamingCommandComparer());
            }

            var leftovers = new NativeList<StreamingSectionCommand>(array.Length, Allocator.Temp);
            var seenSections = new NativeHashMap<Entity, StreamingSectionAction>(array.Length, Allocator.Temp);

            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (!entityManager.Exists(command.SectionEntity))
                {
                    continue;
                }

                if (seenSections.TryGetValue(command.SectionEntity, out var existingAction))
                {
                    if (existingAction != command.Action)
                    {
                        UnityEngine.Debug.LogError($"[PureDOTS] Conflicting streaming commands detected for entity {command.SectionEntity.Index}. Skipping duplicate.");
                    }

                    continue;
                }

                seenSections.TryAdd(command.SectionEntity, command.Action);

                var descriptor = entityManager.GetComponentData<StreamingSectionDescriptor>(command.SectionEntity);
                var sectionState = entityManager.GetComponentData<StreamingSectionState>(command.SectionEntity);
                bool hasRuntime = entityManager.HasComponent<StreamingSectionRuntime>(command.SectionEntity);
                var runtime = hasRuntime
                    ? entityManager.GetComponentData<StreamingSectionRuntime>(command.SectionEntity)
                    : new StreamingSectionRuntime { SceneEntity = Entity.Null };

                switch (command.Action)
                {
                    case StreamingSectionAction.Load:
                        if (sectionState.Status != StreamingSectionStatus.QueuedLoad &&
                            sectionState.Status != StreamingSectionStatus.Unloaded)
                        {
                            continue;
                        }

                        if ((maxConcurrentLoads > 0 && activeLoads >= maxConcurrentLoads) ||
                            (maxLoadsPerTick > 0 && loadsIssued >= maxLoadsPerTick))
                        {
                            leftovers.Add(command);
                            continue;
                        }

                        if (!instantCompletion)
                        {
                            if (descriptor.SceneGuid == default)
                            {
                                UnityEngine.Debug.LogWarning($"[PureDOTS] Streaming section '{descriptor.Identifier}' is missing a Scene GUID. Marking as Error.");
                                sectionState.Status = StreamingSectionStatus.Error;
                                sectionState.CooldownUntilTick = currentTick + coordinator.CooldownTicks;
                                entityManager.SetComponentData(command.SectionEntity, sectionState);
                                break;
                            }

                            if (runtime.SceneEntity == Entity.Null || !entityManager.Exists(runtime.SceneEntity))
                            {
                                runtime.SceneEntity = SceneSystem.LoadSceneAsync(worldUnmanaged, descriptor.SceneGuid);
                                if (runtime.SceneEntity == Entity.Null)
                                {
                                    UnityEngine.Debug.LogError($"[PureDOTS] Failed to start loading scene '{descriptor.Identifier}' ({descriptor.SceneGuid}).");
                                    sectionState.Status = StreamingSectionStatus.Error;
                                    sectionState.CooldownUntilTick = currentTick + coordinator.CooldownTicks;
                                    entityManager.SetComponentData(command.SectionEntity, sectionState);
                                    break;
                                }

                                if (hasRuntime)
                                {
                                    entityManager.SetComponentData(command.SectionEntity, runtime);
                                }
                                else
                                {
                                    entityManager.AddComponentData(command.SectionEntity, runtime);
                                    hasRuntime = true;
                                }
                            }

                            sectionState.Status = StreamingSectionStatus.Loading;
                        }
                        else
                        {
                            sectionState.Status = StreamingSectionStatus.Loaded;
                            if (!hasRuntime)
                            {
                                entityManager.AddComponentData(command.SectionEntity, new StreamingSectionRuntime
                                {
                                    SceneEntity = Entity.Null
                                });
                                hasRuntime = true;
                            }
                            else if (runtime.SceneEntity != Entity.Null)
                            {
                                runtime.SceneEntity = Entity.Null;
                                entityManager.SetComponentData(command.SectionEntity, runtime);
                            }
                        }

                        sectionState.LastSeenTick = currentTick;
                        entityManager.SetComponentData(command.SectionEntity, sectionState);
                        activeLoads++;
                        loadsIssued++;
                        if (stats.FirstLoadTick == StreamingStatistics.TickUnset)
                        {
                            stats.FirstLoadTick = currentTick;
                        }
                        break;

                    case StreamingSectionAction.Unload:
                        if (sectionState.Status != StreamingSectionStatus.QueuedUnload &&
                            sectionState.Status != StreamingSectionStatus.Loaded &&
                            sectionState.Status != StreamingSectionStatus.Loading)
                        {
                            continue;
                        }

                        if (maxUnloadsPerTick > 0 && unloadsIssued >= maxUnloadsPerTick)
                        {
                            leftovers.Add(command);
                            continue;
                        }

                        if (!instantCompletion)
                        {
                            if (runtime.SceneEntity != Entity.Null)
                            {
                                SceneSystem.UnloadScene(worldUnmanaged, runtime.SceneEntity);
                            }

                            sectionState.Status = StreamingSectionStatus.Unloading;
                        }
                        else
                        {
                            sectionState.Status = StreamingSectionStatus.Unloaded;
                            if (hasRuntime && runtime.SceneEntity != Entity.Null)
                            {
                                runtime.SceneEntity = Entity.Null;
                                entityManager.SetComponentData(command.SectionEntity, runtime);
                            }
                        }

                        sectionState.LastSeenTick = currentTick;
                        entityManager.SetComponentData(command.SectionEntity, sectionState);
                        unloadsIssued++;
                        if (stats.FirstUnloadTick == StreamingStatistics.TickUnset)
                        {
                            stats.FirstUnloadTick = currentTick;
                        }
                        break;
                }
            }

            commands.Clear();
            for (int i = 0; i < leftovers.Length; i++)
            {
                commands.Add(leftovers[i]);
            }
            leftovers.Dispose();
            seenSections.Dispose();

            statsHandle.ValueRW = stats;
        }

        private struct StreamingCommandComparer : IComparer<StreamingSectionCommand>
        {
            public int Compare(StreamingSectionCommand x, StreamingSectionCommand y)
            {
                return x.Score.CompareTo(y.Score);
            }
        }
    }
}
