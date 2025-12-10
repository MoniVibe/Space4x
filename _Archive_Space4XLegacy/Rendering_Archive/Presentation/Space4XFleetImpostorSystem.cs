using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Render system for fleet impostors following PureDOTS canonical recipe.
    /// Reads LocalTransform, FleetState, FleetRenderSummary, AggregateState, AggregateMemberElement buffer, RenderLODData.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLODSystem))]
    public partial struct Space4XFleetImpostorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FleetImpostorTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Query matches PureDOTS canonical recipe: LocalTransform, FleetState, FleetRenderSummary, AggregateState, AggregateMemberElement buffer, RenderLODData
            foreach (var (transform, fleetState, renderSummary, aggregateState, members, lodData, entity) in 
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<FleetState>, RefRO<FleetRenderSummary>,
                                    RefRO<AggregateState>, DynamicBuffer<AggregateMemberElement>, RefRO<RenderLODData>>()
                     .WithAll<FleetImpostorTag>()
                     .WithEntityAccess())
            {
                // Skip if LOD-culled (per PureDOTS guide: RecommendedLOD >= 4 means cull)
                if (lodData.ValueRO.RecommendedLOD >= 4) continue;

                // Use AveragePosition from FleetState as centroid (per PureDOTS guide)
                float3 position = fleetState.ValueRO.AveragePosition; // Fleet centroid
                float strength = fleetState.ValueRO.TotalStrength;
                int memberCount = fleetState.ValueRO.MemberCount;
                float health = fleetState.ValueRO.TotalHealth;
                float cargoCapacity = fleetState.ValueRO.TotalCargoCapacity;

                // Fleet LOD strategy: LOD 0/1 show ships+marker, LOD 2+ show only marker
                byte recommendedLOD = lodData.ValueRO.RecommendedLOD;
                
                // Fleet LOD strategy implementation:
                // - LOD 0/1: Individual ships render (handled by carrier/craft render systems with RecommendedLOD < 2)
                //   Hide fleet marker at LOD 0/1 (individual ships visible)
                // - LOD 2+: Only fleet marker renders (individual ships are culled by their render systems with RecommendedLOD >= 2)
                //   Show fleet marker at LOD 2+ (individual ships hidden)
                
                // Update LocalTransform to show/hide fleet marker based on LOD
                if (recommendedLOD >= 2)
                {
                    // LOD 2+: Show fleet marker (individual ships hidden)
                    // Position marker at fleet centroid
                    transform.ValueRW.Position = position;
                    transform.ValueRW.Scale = 1f + (memberCount * 0.1f); // Scale with ship count
                }
                else
                {
                    // LOD 0/1: Hide fleet marker (individual ships visible)
                    transform.ValueRW.Scale = 0f; // Hide by scaling to zero
                }
            }
        }
    }
}
