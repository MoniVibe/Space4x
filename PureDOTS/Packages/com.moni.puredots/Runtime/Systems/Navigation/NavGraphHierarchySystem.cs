using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Maintains navigation graph hierarchy and versioning.
    /// Links LocalGrid, RegionGraph, and TransitGraph entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateAfter(typeof(RegionGraphBuildSystem))]
    [UpdateAfter(typeof(TransitGraphBuildSystem))]
    public partial struct NavGraphHierarchySystem : ISystem
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

            // Ensure hierarchy singleton exists
            Entity hierarchyEntity;
            if (!SystemAPI.TryGetSingletonEntity<NavGraphHierarchy>(out hierarchyEntity))
            {
                hierarchyEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<NavGraphHierarchy>(hierarchyEntity);
            }

            var hierarchy = SystemAPI.GetComponentRW<NavGraphHierarchy>(hierarchyEntity);

            // Find or create local grid entity (NavGraph singleton)
            if (hierarchy.ValueRO.LocalGridEntity == Entity.Null)
            {
                if (SystemAPI.TryGetSingletonEntity<NavGraph>(out var navGraphEntity))
                {
                    hierarchy.ValueRW.LocalGridEntity = navGraphEntity;
                }
            }

            // Find or create region graph entity
            if (hierarchy.ValueRO.RegionGraphEntity == Entity.Null)
            {
                foreach (var (regionNode, entity) in SystemAPI.Query<RefRO<RegionNode>>().WithEntityAccess())
                {
                    hierarchy.ValueRW.RegionGraphEntity = entity;
                    break;
                }
            }

            // Count region nodes
            int regionNodeCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<RegionNode>>())
            {
                regionNodeCount++;
            }
            hierarchy.ValueRW.RegionNodeCount = regionNodeCount;

            // Find or create transit graph entity
            if (hierarchy.ValueRO.TransitGraphEntity == Entity.Null)
            {
                foreach (var (transitNode, entity) in SystemAPI.Query<RefRO<TransitNode>>().WithEntityAccess())
                {
                    hierarchy.ValueRW.TransitGraphEntity = entity;
                    break;
                }
            }

            // Count transit nodes
            int transitNodeCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<TransitNode>>())
            {
                transitNodeCount++;
            }
            hierarchy.ValueRW.TransitNodeCount = transitNodeCount;

            // Increment version if graph structure changed
            if (hierarchy.ValueRO.Version == 0 || 
                regionNodeCount != hierarchy.ValueRO.RegionNodeCount ||
                transitNodeCount != hierarchy.ValueRO.TransitNodeCount)
            {
                hierarchy.ValueRW.Version++;
            }
        }
    }
}






















