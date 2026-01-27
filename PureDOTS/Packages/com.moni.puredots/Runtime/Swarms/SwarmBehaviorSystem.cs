using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Swarms
{
    /// <summary>
    /// Manages swarm behavior modes (Screen, Attack, Return, Tug).
    /// Overrides orbit behavior when in Attack or Tug modes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(DroneOrbitSystem))]
    public partial struct SwarmBehaviorSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<DroneOrbit> _droneOrbitLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _droneOrbitLookup = state.GetComponentLookup<DroneOrbit>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            _transformLookup.Update(ref state);
            _droneOrbitLookup.Update(ref state);

            var deltaTime = tickTime.FixedDeltaTime;

            foreach (var (swarmBehavior, transform, droneOrbit, entity) in SystemAPI.Query<RefRO<SwarmBehavior>, RefRW<LocalTransform>, RefRO<DroneOrbit>>()
                .WithAll<DroneTag>()
                .WithEntityAccess())
            {
                var behavior = swarmBehavior.ValueRO;
                var orbit = droneOrbit.ValueRO;

                // Screen mode: follow orbit (handled by DroneOrbitSystem, no override needed)
                if (behavior.Mode == SwarmMode.Screen)
                {
                    continue;
                }

                // Get current position
                float3 currentPos = transform.ValueRO.Position;

                // Get anchor position
                float3 anchorPos = float3.zero;
                if (_transformLookup.TryGetComponent(orbit.AnchorShip, out var anchorTransform))
                {
                    anchorPos = anchorTransform.Position;
                }

                float3 targetPos = float3.zero;
                bool hasTarget = false;

                switch (behavior.Mode)
                {
                    case SwarmMode.Attack:
                        // Attack mode: move toward target along spiral/intercept path
                        if (behavior.Target != Entity.Null && state.EntityManager.Exists(behavior.Target))
                        {
                            if (_transformLookup.TryGetComponent(behavior.Target, out var targetTransform))
                            {
                                targetPos = targetTransform.Position;
                                hasTarget = true;
                            }
                        }
                        break;

                    case SwarmMode.Return:
                        // Return mode: move back to orbit radius
                        // Compute desired position on orbit
                        if (_droneOrbitLookup.HasComponent(entity))
                        {
                            var orbitRef = _droneOrbitLookup[entity];
                            float angle = orbitRef.PhaseOffset + orbitRef.AngularSpeed * tickTime.WorldSeconds;
                            targetPos = anchorPos + new float3(
                                math.cos(angle) * orbitRef.Radius,
                                orbitRef.Elevation,
                                math.sin(angle) * orbitRef.Radius
                            );
                            hasTarget = true;
                        }
                        break;

                    case SwarmMode.Tug:
                        // Tug mode: position behind desired direction (handled by thrust system)
                        // For now, just maintain orbit position
                        continue;
                }

                if (hasTarget)
                {
                    // Move toward target position
                    float3 direction = math.normalizesafe(targetPos - currentPos);
                    float moveSpeed = 10f; // Configurable move speed
                    float3 newPos = currentPos + direction * moveSpeed * deltaTime;

                    // Update transform
                    var currentTransform = transform.ValueRO;
                    currentTransform.Position = newPos;
                    transform.ValueRW = currentTransform;
                }
            }
        }
    }
}

