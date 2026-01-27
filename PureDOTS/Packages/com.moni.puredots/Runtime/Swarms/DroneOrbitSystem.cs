using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Swarms;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Swarms
{
    /// <summary>
    /// Updates positions for drones with DroneOrbit using analytic orbit calculations.
    /// Includes cloud distribution effects for volumetric swarm appearance.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct DroneOrbitSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            _transformLookup.Update(ref state);

            var worldSeconds = tickTime.WorldSeconds;

            foreach (var (droneOrbit, transform, entity) in SystemAPI.Query<RefRO<DroneOrbit>, RefRW<LocalTransform>>()
                .WithAll<DroneTag>()
                .WithEntityAccess())
            {
                var orbit = droneOrbit.ValueRO;

                // Skip if anchor is invalid
                if (orbit.AnchorShip == Entity.Null || !state.EntityManager.Exists(orbit.AnchorShip))
                {
                    continue;
                }

                // Get anchor world position
                float3 anchorPos = float3.zero;
                if (_transformLookup.TryGetComponent(orbit.AnchorShip, out var anchorTransform))
                {
                    anchorPos = anchorTransform.Position;
                }

                // Compute angle: angle = PhaseOffset + AngularSpeed * WorldSeconds
                float angle = orbit.PhaseOffset + orbit.AngularSpeed * worldSeconds;

                // Compute position in local orbit plane: p = (cos(angle), Elevation, sin(angle)) * Radius
                // Use Elevation for vertical offset to create cloud effect
                float3 p = new float3(
                    math.cos(angle) * orbit.Radius,
                    orbit.Elevation, // Vertical offset for cloud distribution
                    math.sin(angle) * orbit.Radius
                );

                // Add small random jitter for cloud effect (deterministic based on entity index)
                // This creates a more volumetric swarm appearance
                uint entityIndex = (uint)entity.Index;
                float jitterScale = 0.1f; // Small jitter amount
                float3 jitter = new float3(
                    (entityIndex % 100) / 100f - 0.5f,
                    ((entityIndex >> 8) % 100) / 100f - 0.5f,
                    ((entityIndex >> 16) % 100) / 100f - 0.5f
                ) * jitterScale;
                p += jitter;

                // Final position = anchor position + orbit position
                float3 finalPos = anchorPos + p;

                // Update transform
                var currentTransform = transform.ValueRO;
                currentTransform.Position = finalPos;
                transform.ValueRW = currentTransform;
            }
        }
    }
}

