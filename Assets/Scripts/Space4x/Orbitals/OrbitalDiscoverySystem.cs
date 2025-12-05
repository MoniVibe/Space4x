using Space4X.Orbitals;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Orbitals
{
    /// <summary>
    /// Reveals hidden orbital objects when player ships come within discovery range.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct OrbitalDiscoverySystem : ISystem
    {
        private const float DISCOVERY_RANGE = 50f; // Distance threshold for discovery

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OrbitalObjectTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Query all hidden orbitals
            foreach (var (orbitalState, orbitalTransform, orbitalEntity) in SystemAPI.Query<RefRW<OrbitalObjectState>, RefRO<LocalTransform>>()
                .WithAll<OrbitalObjectTag>()
                .WithEntityAccess())
            {
                var stateRef = orbitalState.ValueRO;

                // Skip if already discovered
                if (!stateRef.Hidden)
                {
                    continue;
                }

                float3 orbitalPos = orbitalTransform.ValueRO.Position;

                // Check distance to player ships
                // TODO: Query actual player ship entities - for now, use a simple check
                // In real implementation, query ships with player faction tag
                bool discovered = false;

                // Simple discovery check: if orbital is near origin (where player might be)
                // Real implementation would check distance to nearest player ship
                float distanceToOrigin = math.length(orbitalPos);
                if (distanceToOrigin < DISCOVERY_RANGE * 2f) // Temporary: discover if near origin
                {
                    discovered = true;
                }

                // TODO: Check actual player ship positions
                // foreach (var (shipTransform, shipEntity) in SystemAPI.Query<RefRO<LocalTransform>>()
                //     .WithAll<PlayerShipTag>() // Would need this component
                //     .WithEntityAccess())
                // {
                //     float3 shipPos = shipTransform.ValueRO.Position;
                //     float distance = math.distance(orbitalPos, shipPos);
                //     if (distance < DISCOVERY_RANGE)
                //     {
                //         discovered = true;
                //         break;
                //     }
                // }

                if (discovered)
                {
                    // Reveal orbital
                    stateRef.Hidden = false;
                    orbitalState.ValueRW = stateRef;

                    // TODO: Trigger discovery event/notification
                    // TODO: Add mission/interaction availability if OffersMission is true
                }
            }
        }
    }
}

