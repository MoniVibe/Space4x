using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Orbits
{
    /// <summary>
    /// Updates positions for entities with OrbitParams using analytic orbit calculations.
    /// Position = f(time, params) - no numerical integration, fully deterministic.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct OrbitSystem : ISystem
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

            foreach (var (orbitParams, transform) in SystemAPI.Query<RefRO<OrbitParams>, RefRW<LocalTransform>>())
            {
                var orbit = orbitParams.ValueRO;

                // Skip if anchor is invalid
                if (orbit.Anchor == Entity.Null || !state.EntityManager.Exists(orbit.Anchor))
                {
                    continue;
                }

                // Get anchor world position
                float3 anchorPos = float3.zero;
                if (_transformLookup.TryGetComponent(orbit.Anchor, out var anchorTransform))
                {
                    anchorPos = anchorTransform.Position;
                }
                anchorPos += orbit.LocalCenter;

                // Compute angle: angle = InitialPhase + AngularSpeed * WorldSeconds
                float angle = orbit.InitialPhase + orbit.AngularSpeed * worldSeconds;

                // Compute position in local orbit plane: p = (cos(angle), 0, sin(angle)) * Radius
                float3 p = new float3(math.cos(angle), 0f, math.sin(angle)) * orbit.Radius;

                // Apply inclination rotation around X axis
                float cosInc = math.cos(orbit.Inclination);
                float sinInc = math.sin(orbit.Inclination);
                float3 rotated = new float3(
                    p.x,
                    p.y * cosInc - p.z * sinInc,
                    p.y * sinInc + p.z * cosInc
                );

                // Final position = anchor position + rotated orbit position
                float3 finalPos = anchorPos + rotated;

                // Update transform
                var currentTransform = transform.ValueRO;
                currentTransform.Position = finalPos;
                transform.ValueRW = currentTransform;
            }
        }
    }
}

