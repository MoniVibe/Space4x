using PureDOTS.Runtime.ComplexEntities;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.ComplexEntities
{
    /// <summary>
    /// Handles narrative detail expansion/contraction for complex entities.
    /// Activates when InspectionRequest is present, deactivates when removed.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct ComplexEntityNarrativeDetailSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check feature flag
            var featureFlags = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((featureFlags.Flags & SimulationFeatureFlags.ComplexEntitiesEnabled) == 0)
                return;

            if ((featureFlags.Flags & SimulationFeatureFlags.ComplexEntityNarrativeExpansionEnabled) == 0)
                return;

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickState))
                return;
            var currentTick = tickState.Tick;

            // Enable narrative detail for entities with inspection request
            foreach (var (coreAxes, entity) in SystemAPI.Query<RefRW<ComplexEntityCoreAxes>>()
                .WithAll<InspectionRequest>()
                .WithNone<ComplexEntityNarrativeDetail>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new ComplexEntityNarrativeDetail
                {
                    NarrativeBlob = default,
                    NarrativeFlags = 0,
                    LastNarrativeTick = currentTick
                });
                ecb.SetComponentEnabled<ComplexEntityNarrativeDetail>(entity, true);

                // Update flags
                var axes = coreAxes.ValueRW;
                axes.Flags |= ComplexEntityFlags.NarrativeActive;
                ecb.SetComponent(entity, axes);
            }

            // Ensure narrative detail is enabled for already-present components (component may be disabled after contraction)
            foreach (var (coreAxes, entity) in SystemAPI.Query<RefRW<ComplexEntityCoreAxes>>()
                .WithAll<InspectionRequest, ComplexEntityNarrativeDetail>()
                .WithEntityAccess())
            {
                ecb.SetComponentEnabled<ComplexEntityNarrativeDetail>(entity, true);
                var axes = coreAxes.ValueRW;
                axes.Flags |= ComplexEntityFlags.NarrativeActive;
                ecb.SetComponent(entity, axes);
            }

            // Disable narrative detail for entities without inspection request
            foreach (var (coreAxes, entity) in SystemAPI.Query<RefRW<ComplexEntityCoreAxes>>()
                .WithAll<ComplexEntityNarrativeDetail>()
                .WithNone<InspectionRequest>()
                .WithEntityAccess())
            {
                ecb.SetComponentEnabled<ComplexEntityNarrativeDetail>(entity, false);

                // Update flags
                var axes = coreAxes.ValueRW;
                axes.Flags &= ~ComplexEntityFlags.NarrativeActive;
                ecb.SetComponent(entity, axes);
            }
        }
    }
}
