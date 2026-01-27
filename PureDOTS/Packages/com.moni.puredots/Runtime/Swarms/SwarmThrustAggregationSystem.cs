using PureDOTS.Runtime.Swarms;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Swarms
{
    /// <summary>
    /// Aggregates thrust contributions from drones in Tug mode.
    /// Sums contributions for each ship/object with SwarmThrustState.Active.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(SwarmBehaviorSystem))]
    public partial struct SwarmThrustAggregationSystem : ISystem
    {
        private const float THRUST_CONTRIBUTION_PER_DRONE = 5f; // Thrust per drone
        private const float TUG_RANGE = 20f; // Range for tug detection

        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SwarmBehavior> _swarmBehaviorLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SwarmThrustState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _swarmBehaviorLookup = state.GetComponentLookup<SwarmBehavior>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _swarmBehaviorLookup.Update(ref state);

            // Reset thrust for all active swarm thrust entities
            foreach (var (swarmThrust, entity) in SystemAPI.Query<RefRW<SwarmThrustState>>()
                .WithEntityAccess())
            {
                if (!swarmThrust.ValueRO.Active)
                {
                    continue;
                }

                swarmThrust.ValueRW.CurrentThrust = 0f;
            }

            // Aggregate contributions from drones in Tug mode
            foreach (var (swarmBehavior, droneTransform, droneOrbit, droneEntity) in SystemAPI.Query<RefRO<SwarmBehavior>, RefRO<LocalTransform>, RefRO<DroneOrbit>>()
                .WithAll<DroneTag>()
                .WithEntityAccess())
            {
                var behavior = swarmBehavior.ValueRO;

                // Only consider drones in Tug mode
                if (behavior.Mode != SwarmMode.Tug)
                {
                    continue;
                }

                // Get anchor ship (the entity being tugged)
                Entity anchorShip = droneOrbit.ValueRO.AnchorShip;
                if (anchorShip == Entity.Null || !state.EntityManager.Exists(anchorShip))
                {
                    continue;
                }

                // Check if anchor has SwarmThrustState
                if (!state.EntityManager.HasComponent<SwarmThrustState>(anchorShip))
                {
                    continue;
                }

                var swarmThrust = state.EntityManager.GetComponentData<SwarmThrustState>(anchorShip);
                if (!swarmThrust.Active)
                {
                    continue;
                }

                // Check if drone is in range
                float3 dronePos = droneTransform.ValueRO.Position;
                float3 anchorPos = float3.zero;
                if (_transformLookup.TryGetComponent(anchorShip, out var anchorTransform))
                {
                    anchorPos = anchorTransform.Position;
                }

                float distance = math.distance(dronePos, anchorPos);
                if (distance > TUG_RANGE)
                {
                    continue;
                }

                // Compute thrust contribution direction (from drone to anchor, along desired direction)
                float3 toAnchor = math.normalizesafe(anchorPos - dronePos);
                float3 desiredDir = math.normalizesafe(swarmThrust.DesiredDirection);
                
                // Contribution is proportional to alignment with desired direction
                float alignment = math.max(0f, math.dot(toAnchor, desiredDir));
                float contribution = THRUST_CONTRIBUTION_PER_DRONE * alignment;

                // Add to aggregated thrust
                swarmThrust.CurrentThrust += contribution;

                // Update component
                state.EntityManager.SetComponentData(anchorShip, swarmThrust);
            }
        }
    }
}

