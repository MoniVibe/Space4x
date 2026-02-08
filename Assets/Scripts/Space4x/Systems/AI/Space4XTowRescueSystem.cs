using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Swarms;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Runtime.Breakables;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSwarmDemoSystem))]
    public partial struct Space4XTowRescueSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XShipCapabilityState> _capabilityLookup;
        private ComponentLookup<SwarmThrustState> _swarmThrustLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _capabilityLookup = state.GetComponentLookup<Space4XShipCapabilityState>(true);
            _swarmThrustLookup = state.GetComponentLookup<SwarmThrustState>(false);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _capabilityLookup.Update(ref state);
            _swarmThrustLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            var tick = timeState.Tick;

            foreach (var (request, transform, entity) in SystemAPI.Query<RefRW<Space4XTowRescueRequest>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!IsRequestValid(request.ValueRO, tick, ref state))
                {
                    state.EntityManager.RemoveComponent<Space4XTowRescueRequest>(entity);
                    continue;
                }

                if (!_swarmThrustLookup.HasComponent(entity))
                {
                    continue;
                }

                var thrust = _swarmThrustLookup[entity];
                thrust.Active = true;
                thrust.DesiredDirection = ResolveRescueDirection(transform.ValueRO.Position, ref state);
                _swarmThrustLookup[entity] = thrust;
            }
        }

        private bool IsRequestValid(in Space4XTowRescueRequest request, uint tick, ref SystemState state)
        {
            if (!IsValidEntity(request.Target))
            {
                return false;
            }

            if (request.ExpireTick != 0u && tick > request.ExpireTick + 60u)
            {
                return false;
            }

            if (!_capabilityLookup.HasComponent(request.Target))
            {
                return false;
            }

            var capability = _capabilityLookup[request.Target];
            if (capability.IsAlive == 0 || capability.IsMobile != 0)
            {
                return false;
            }

            return true;
        }

        private float3 ResolveRescueDirection(float3 anchorPos, ref SystemState state)
        {
            Entity nearest = Entity.Null;
            float bestDistance = float.MaxValue;

            foreach (var (asteroid, transform, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                float distance = math.distance(anchorPos, transform.ValueRO.Position);
                if (distance < bestDistance ||
                    (math.abs(distance - bestDistance) < 0.01f && (nearest == Entity.Null || entity.Index < nearest.Index)))
                {
                    bestDistance = distance;
                    nearest = entity;
                }
            }

            if (IsValidEntity(nearest) && _transformLookup.HasComponent(nearest))
            {
                var targetPos = _transformLookup[nearest].Position;
                return math.normalizesafe(anchorPos - targetPos, new float3(1f, 0f, 0f));
            }

            return new float3(1f, 0f, 0f);
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }
    }
}
