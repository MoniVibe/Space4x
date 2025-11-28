using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures carriers participate in fleet/registry pipelines by attaching Space4XFleet and FleetMovementBroadcast.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XCoreSingletonGuardSystem))]
    public partial struct Space4XCarrierFleetBootstrapSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;

            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<Space4XFleet>(entity))
                {
                    // Derive a stable fleet id from the carrier id so registry bridge can pick it up.
                    var fleetId = carrier.ValueRO.CarrierId;
                    if (fleetId.IsEmpty)
                    {
                        fleetId = default;
                        fleetId.Append('f');
                        fleetId.Append('l');
                        fleetId.Append('e');
                        fleetId.Append('e');
                        fleetId.Append('t');
                        fleetId.Append('-');
                        fleetId.Append('c');
                        fleetId.Append('a');
                        fleetId.Append('r');
                        fleetId.Append('r');
                        fleetId.Append('i');
                        fleetId.Append('e');
                        fleetId.Append('r');
                    }

                    state.EntityManager.AddComponentData(entity, new Space4XFleet
                    {
                        FleetId = fleetId,
                        ShipCount = 1,
                        Posture = Space4XFleetPosture.Patrol,
                        TaskForce = 0
                    });
                }

                if (!state.EntityManager.HasComponent<FleetMovementBroadcast>(entity))
                {
                    float3 position = float3.zero;
                    if (_transformLookup.HasComponent(entity))
                    {
                        position = _transformLookup[entity].Position;
                    }

                    state.EntityManager.AddComponentData(entity, new FleetMovementBroadcast
                    {
                        Position = position,
                        Velocity = float3.zero,
                        LastUpdateTick = tick,
                        AllowsInterception = 1,
                        TechTier = 0
                    });
                }
            }

            // Disable once all carriers are bootstrapped to avoid per-frame cost.
            state.Enabled = false;
        }
    }
}
