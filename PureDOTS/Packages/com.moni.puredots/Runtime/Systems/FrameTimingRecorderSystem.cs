using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Profiling;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Aggregates per-group frame timings and allocation diagnostics for debug tooling.
    /// Executes ahead of presentation systems so overlay consumers read the latest data.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    // DebugDisplaySystem is legacy and compiled out unless PUREDOTS_LEGACY_CAMERA is defined.
    public sealed partial class FrameTimingRecorderSystem : SystemBase
    {
        private static readonly FrameTimingGroup[] s_OutputOrder =
        {
            FrameTimingGroup.Camera,  // Highest priority - runs first
            FrameTimingGroup.Time,
            FrameTimingGroup.Environment,
            FrameTimingGroup.Spatial,
            FrameTimingGroup.Transport,
            FrameTimingGroup.AI,
            FrameTimingGroup.Villager,
            FrameTimingGroup.Resource,
            FrameTimingGroup.Miracle,
            FrameTimingGroup.Gameplay,
            FrameTimingGroup.Hand,
            FrameTimingGroup.History,
            FrameTimingGroup.Presentation
        };

        private EntityQuery _streamQuery;
        private Dictionary<FrameTimingGroup, FrameTimingSample> _pendingSamples;
        private int _previousGc0;
        private int _previousGc1;
        private int _previousGc2;

        protected override void OnCreate()
        {
            _streamQuery = GetEntityQuery(ComponentType.ReadWrite<FrameTimingStream>());
            _pendingSamples = new Dictionary<FrameTimingGroup, FrameTimingSample>(16);
            TryEnsureStreamEntity(out _);
        }

        protected override void OnDestroy()
        {
            _pendingSamples.Clear();
        }

        /// <summary>
        /// Records a timing sample from a system group.
        /// </summary>
        public void RecordGroupTiming(FrameTimingGroup group, float durationMs, int systemCount, bool isCatchUp)
        {
            _pendingSamples ??= new Dictionary<FrameTimingGroup, FrameTimingSample>(16);

            if (durationMs < 0f || float.IsNaN(durationMs) || float.IsInfinity(durationMs))
            {
                return;
            }

            var sample = new FrameTimingSample
            {
                Group = group,
                DurationMs = durationMs,
                BudgetMs = FrameTimingUtility.GetBudgetMs(group),
                Flags = isCatchUp ? FrameTimingFlags.CatchUp : FrameTimingFlags.None,
                SystemCount = systemCount
            };

            if (sample.BudgetMs > 0f && durationMs > sample.BudgetMs)
            {
                sample.Flags |= FrameTimingFlags.BudgetExceeded;
            }

            _pendingSamples[group] = sample;
        }

        /// <summary>
        /// Helper invoked by system groups to push a sample into the recorder if present.
        /// </summary>
        public static void RecordGroupTiming(World world, FrameTimingGroup group, float durationMs, int systemCount, bool isCatchUp)
        {
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var recorder = world.GetExistingSystemManaged<FrameTimingRecorderSystem>();
            recorder?.RecordGroupTiming(group, durationMs, systemCount, isCatchUp);
        }

        /// <summary>
        /// Determines whether the active world is currently running a rewind catch-up.
        /// </summary>
        public static bool IsCatchUp(World world)
        {
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            if (!query.TryGetSingleton(out RewindState rewindState))
            {
                return false;
            }

            return rewindState.Mode == RewindMode.CatchUp;
        }

        /// <summary>
        /// Exposes group labels to consumers outside this system.
        /// </summary>
        public static FixedString32Bytes GetGroupLabel(FrameTimingGroup group) => FrameTimingUtility.GetGroupLabel(group);

        protected override void OnUpdate()
        {
            if (!TryEnsureStreamEntity(out var entity))
            {
                return;
            }
            var buffer = EntityManager.GetBuffer<FrameTimingSample>(entity);
            buffer.Clear();

            for (int i = 0; i < s_OutputOrder.Length; i++)
            {
                var group = s_OutputOrder[i];
                if (_pendingSamples.TryGetValue(group, out var sample))
                {
                    buffer.Add(sample);
                }
            }

            _pendingSamples.Clear();

            var stream = EntityManager.GetComponentData<FrameTimingStream>(entity);
            stream.Version++;
            if (SystemAPI.TryGetSingleton(out TimeState timeState))
            {
                stream.LastTick = timeState.Tick;
            }
            EntityManager.SetComponentData(entity, stream);

            var diagnostics = EntityManager.GetComponentData<AllocationDiagnostics>(entity);

            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(1);
            var gc2 = GC.CollectionCount(2);
            diagnostics.GcCollectionsGeneration0 = gc0 - _previousGc0;
            diagnostics.GcCollectionsGeneration1 = gc1 - _previousGc1;
            diagnostics.GcCollectionsGeneration2 = gc2 - _previousGc2;
            _previousGc0 = gc0;
            _previousGc1 = gc1;
            _previousGc2 = gc2;

            diagnostics.TotalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
            diagnostics.TotalReservedBytes = Profiler.GetTotalReservedMemoryLong();
            diagnostics.TotalUnusedReservedBytes = Profiler.GetTotalUnusedReservedMemoryLong();

            EntityManager.SetComponentData(entity, diagnostics);
        }

        private bool TryEnsureStreamEntity(out Entity entity)
        {
            entity = Entity.Null;
            var entityCount = _streamQuery.CalculateEntityCount();
            if (entityCount == 0)
            {
                entity = EntityManager.CreateEntity(typeof(FrameTimingStream));
                EntityManager.SetComponentData(entity, new FrameTimingStream
                {
                    Version = 0,
                    LastTick = 0
                });
                EntityManager.AddBuffer<FrameTimingSample>(entity);
                EntityManager.AddComponentData(entity, new AllocationDiagnostics());
            }
            else if (entityCount == 1)
            {
                entity = _streamQuery.GetSingletonEntity();
                if (!EntityManager.HasBuffer<FrameTimingSample>(entity))
                {
                    EntityManager.AddBuffer<FrameTimingSample>(entity);
                }
                if (!EntityManager.HasComponent<AllocationDiagnostics>(entity))
                {
                    EntityManager.AddComponentData(entity, new AllocationDiagnostics());
                }
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
