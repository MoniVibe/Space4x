using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Builds region-level graph from world generation (COLD path).
    /// Creates RegionNode entities and RegionEdge connections.
    /// Event-driven: only rebuilds when dirty regions/edges are marked.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateBefore(typeof(NavGraphHierarchySystem))]
    public partial struct RegionGraphBuildSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Get hierarchy entity
            if (!SystemAPI.TryGetSingletonEntity<NavGraphHierarchy>(out var hierarchyEntity))
            {
                return;
            }

            var hierarchy = SystemAPI.GetComponent<NavGraphHierarchy>(hierarchyEntity);

            // Check for dirty regions/edges
            bool hasDirtyRegions = false;
            if (state.EntityManager.HasBuffer<DirtyRegion>(hierarchyEntity))
            {
                var dirtyRegions = state.EntityManager.GetBuffer<DirtyRegion>(hierarchyEntity);
                hasDirtyRegions = dirtyRegions.Length > 0;
            }

            bool hasDirtyEdges = false;
            if (state.EntityManager.HasBuffer<DirtyEdge>(hierarchyEntity))
            {
                var dirtyEdges = state.EntityManager.GetBuffer<DirtyEdge>(hierarchyEntity);
                hasDirtyEdges = dirtyEdges.Length > 0;
            }

            // Only rebuild if graph doesn't exist or if dirty regions/edges are marked
            if (hierarchy.RegionGraphEntity == Entity.Null)
            {
                // Initial build - create region graph
                var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

                var regionGraphEntity = ecb.CreateEntity();
                ecb.AddComponent<RegionNode>(regionGraphEntity, new RegionNode
                {
                    RegionId = 0,
                    Center = float3.zero,
                    BoundsMin = new float3(-1000f, 0f, -1000f),
                    BoundsMax = new float3(1000f, 0f, 1000f),
                    BiomeType = 0,
                    BaseCost = 1f
                });

                ecb.AddBuffer<RegionEdge>(regionGraphEntity);
                ecb.Playback(state.EntityManager);
            }
            else if (hasDirtyRegions || hasDirtyEdges)
            {
                // Incremental rebuild - process dirty regions/edges
                // TODO: In a full implementation, this would:
                // 1. Process DirtyRegion list to update affected RegionNodes
                // 2. Process DirtyEdge list to update affected RegionEdges
                // 3. Recalculate costs for affected regions
                // 4. Clear dirty lists after processing

                // Clear dirty lists after processing
                if (state.EntityManager.HasBuffer<DirtyRegion>(hierarchyEntity))
                {
                    var dirtyRegions = state.EntityManager.GetBuffer<DirtyRegion>(hierarchyEntity);
                    dirtyRegions.Clear();
                }

                if (state.EntityManager.HasBuffer<DirtyEdge>(hierarchyEntity))
                {
                    var dirtyEdges = state.EntityManager.GetBuffer<DirtyEdge>(hierarchyEntity);
                    dirtyEdges.Clear();
                }

                // Increment version to signal graph change
                var hierarchyRW = SystemAPI.GetComponentRW<NavGraphHierarchy>(hierarchyEntity);
                hierarchyRW.ValueRW.Version++;
            }
        }
    }
}

