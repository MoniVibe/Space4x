using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Performs BFS from core segments to determine connectivity.
    /// Marks detached segments that are no longer connected to any core.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlatformSegmentDestructionSystem))]
    public partial struct PlatformConnectivitySystem : ISystem
    {
        private BufferLookup<PlatformSegmentState> _segmentStatesLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullDefRegistry>();
            _segmentStatesLookup = state.GetBufferLookup<PlatformSegmentState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hullRegistry = SystemAPI.GetSingleton<HullDefRegistry>();

            if (!hullRegistry.Registry.IsCreated)
            {
                return;
            }

            ref var hullRegistryBlob = ref hullRegistry.Registry.Value;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            _segmentStatesLookup.Update(ref state);

            foreach (var (hullRef, entity) in SystemAPI.Query<
                RefRO<PlatformHullRef>>().WithEntityAccess())
            {
                if (!SystemAPI.HasComponent<NeedConnectivityUpdate>(entity))
                {
                    continue;
                }

                var hullId = hullRef.ValueRO.HullId;
                if (hullId < 0 || hullId >= hullRegistryBlob.Hulls.Length)
                {
                    ecb.RemoveComponent<NeedConnectivityUpdate>(entity);
                    continue;
                }

                if (!_segmentStatesLookup.HasBuffer(entity))
                {
                    ecb.RemoveComponent<NeedConnectivityUpdate>(entity);
                    continue;
                }

                var segmentStates = _segmentStatesLookup[entity];
                ref var hullDef = ref hullRegistryBlob.Hulls[hullId];

                UpdateConnectivity(
                    ref segmentStates,
                    in hullDef,
                    ref hullRegistryBlob);

                ecb.RemoveComponent<NeedConnectivityUpdate>(entity);
            }
        }

        [BurstCompile]
        private static void UpdateConnectivity(
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            in HullDef hullDef,
            ref HullDefRegistryBlob hullRegistry)
        {
            if (hullDef.SegmentCount == 0)
            {
                return;
            }

            var visited = new NativeHashSet<int>(segmentStates.Length, Allocator.Temp);
            var queue = new NativeQueue<int>(Allocator.Temp);

            for (int i = 0; i < segmentStates.Length; i++)
            {
                var segmentState = segmentStates[i];
                var segmentIndex = segmentState.SegmentIndex;

                if ((segmentState.Status & SegmentStatusFlags.Destroyed) != 0)
                {
                    continue;
                }

                var segmentOffset = hullDef.SegmentOffset + segmentIndex;
                if (segmentOffset < 0 || segmentOffset >= hullRegistry.Segments.Length)
                {
                    continue;
                }

                ref var segmentDef = ref hullRegistry.Segments[segmentOffset];

                if (segmentDef.IsCore != 0)
                {
                    queue.Enqueue(segmentIndex);
                    visited.Add(segmentIndex);
                }
            }

            while (queue.TryDequeue(out var currentIndex))
            {
                var currentOffset = hullDef.SegmentOffset + currentIndex;
                if (currentOffset < 0 || currentOffset >= hullRegistry.Segments.Length)
                {
                    continue;
                }

                ref var currentDef = ref hullRegistry.Segments[currentOffset];

                var neighborStart = currentDef.NeighborStart;
                var neighborCount = currentDef.NeighborCount;

                for (int i = 0; i < neighborCount; i++)
                {
                    var neighborIndex = hullRegistry.SegmentAdjacency[neighborStart + i];
                    if (visited.Contains(neighborIndex))
                    {
                        continue;
                    }

                    var neighborStateIndex = FindSegmentStateIndex(in segmentStates, neighborIndex);
                    if (neighborStateIndex < 0)
                    {
                        continue;
                    }

                    var neighborState = segmentStates[neighborStateIndex];
                    if ((neighborState.Status & SegmentStatusFlags.Destroyed) != 0)
                    {
                        continue;
                    }

                    visited.Add(neighborIndex);
                    queue.Enqueue(neighborIndex);
                }
            }

            for (int i = 0; i < segmentStates.Length; i++)
            {
                var segmentState = segmentStates[i];
                var segmentIndex = segmentState.SegmentIndex;

                if ((segmentState.Status & SegmentStatusFlags.Destroyed) != 0)
                {
                    continue;
                }

                if (visited.Contains(segmentIndex))
                {
                    segmentState.Status |= SegmentStatusFlags.ConnectedToCore;
                    segmentState.Status &= ~SegmentStatusFlags.Detached;
                }
                else
                {
                    segmentState.Status |= SegmentStatusFlags.Detached;
                    segmentState.Status &= ~SegmentStatusFlags.ConnectedToCore;
                }

                segmentStates[i] = segmentState;
            }
        }

        [BurstCompile]
        private static int FindSegmentStateIndex(in DynamicBuffer<PlatformSegmentState> segmentStates, int segmentIndex)
        {
            for (int i = 0; i < segmentStates.Length; i++)
            {
                if (segmentStates[i].SegmentIndex == segmentIndex)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}

