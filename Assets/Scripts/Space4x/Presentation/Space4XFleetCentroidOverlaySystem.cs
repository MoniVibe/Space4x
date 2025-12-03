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
    /// System that draws fleet centroids and bounds as debug overlays when enabled.
    /// Uses Gizmos/Handles (requires BurstDiscard).
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct Space4XFleetCentroidOverlaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DebugOverlayConfig>();
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<DebugOverlayConfig>(out var debugConfig))
            {
                return;
            }

            if (!debugConfig.ShowFactionZones)
            {
                return;
            }

            // Draw fleet centroids and bounds
            foreach (var (transform, fleetState, aggregateState, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<Space4X.Presentation.FleetState>, RefRO<Space4X.Presentation.AggregateState>>()
                     .WithAll<FleetImpostorTag>()
                     .WithEntityAccess())
            {
                float3 centroid = fleetState.ValueRO.AveragePosition;
                int memberCount = fleetState.ValueRO.MemberCount;

                // Get faction color
                float4 factionColor = new float4(0.5f, 0.5f, 0.5f, 1f);
                if (SystemAPI.HasComponent<Space4X.Presentation.FactionColor>(entity))
                {
                    factionColor = SystemAPI.GetComponentRO<Space4X.Presentation.FactionColor>(entity).ValueRO.Value;
                }

                // Draw wireframe sphere at centroid
                Color gizmoColor = new Color(factionColor.x, factionColor.y, factionColor.z, 0.3f);
                Gizmos.color = gizmoColor;
                Gizmos.DrawWireSphere(centroid, 5f + memberCount * 0.5f); // Scale radius with member count

                // Draw bounds if available (from AggregateState)
                float3 boundsCenter = (aggregateState.ValueRO.BoundsMin + aggregateState.ValueRO.BoundsMax) * 0.5f;
                float3 boundsSize = aggregateState.ValueRO.BoundsMax - aggregateState.ValueRO.BoundsMin;
                
                Gizmos.color = new Color(factionColor.x, factionColor.y, factionColor.z, 0.2f);
                Gizmos.DrawWireCube(boundsCenter, boundsSize);
            }
        }
    }
}

