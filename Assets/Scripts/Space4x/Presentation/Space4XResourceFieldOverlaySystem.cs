using Space4X.Registry;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that draws resource field visualization as debug overlays when enabled.
    /// Draws "cloud" effects around resource-rich asteroids.
    /// Uses Gizmos/Handles (requires BurstDiscard).
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct Space4XResourceFieldOverlaySystem : ISystem
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

            if (!debugConfig.ShowResourceFields)
            {
                return;
            }

            // Draw resource clouds for each asteroid
            foreach (var (transform, asteroid, sourceState, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<Asteroid>,
                                    RefRO<PureDOTS.Runtime.Components.ResourceSourceState>>()
                     .WithAll<AsteroidPresentationTag>()
                     .WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;
                float resourceAmount = asteroid.ValueRO.ResourceAmount;
                float maxResourceAmount = asteroid.ValueRO.MaxResourceAmount;
                float resourceRatio = resourceAmount / math.max(0.0001f, maxResourceAmount);

                // Skip depleted asteroids
                if (resourceRatio <= 0f)
                {
                    continue;
                }

                // Get resource type color
                float4 resourceColor = new float4(0.6f, 0.6f, 0.6f, 1f);
                if (SystemAPI.HasComponent<ResourceTypeColor>(entity))
                {
                    resourceColor = SystemAPI.GetComponentRO<ResourceTypeColor>(entity).ValueRO.Value;
                }

                // Calculate cloud radius based on resource amount
                float baseRadius = 5f;
                float radius = baseRadius + resourceRatio * 10f; // Scale from 5 to 15 based on richness

                // Draw semi-transparent sphere (cloud effect)
                Color gizmoColor = new Color(resourceColor.x, resourceColor.y, resourceColor.z, 0.2f * resourceRatio);
                Gizmos.color = gizmoColor;
                Gizmos.DrawSphere(position, radius);

                // Draw wireframe outline
                gizmoColor.a = 0.5f * resourceRatio;
                Gizmos.color = gizmoColor;
                Gizmos.DrawWireSphere(position, radius);
            }

            // Optional: Cluster nearby asteroids and draw larger cloud
            // This would require spatial queries, which is more complex
            // For now, individual asteroid clouds are sufficient
        }
    }
}

