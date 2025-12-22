using Space4X.Registry;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Assigns missing mining orders by choosing a viable resource source near the vessel.
    /// Keeps headless mining loops moving without explicit player-issued directives.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateBefore(typeof(VesselAISystem))]
    public partial struct Space4XMiningOrderAutoAssignSystem : ISystem
    {
        private ComponentLookup<ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<ResourceTypeId> _resourceTypeLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<MiningYield> _yieldLookup;
        private ComponentLookup<CarrierMiningTarget> _carrierTargetLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PureDOTS.Runtime.Components.TimeState>();
            state.RequireForUpdate<PureDOTS.Runtime.Components.RewindState>();
            state.RequireForUpdate<MiningOrder>();

            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(true);
            _resourceTypeLookup = state.GetComponentLookup<ResourceTypeId>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _yieldLookup = state.GetComponentLookup<MiningYield>(false);
            _carrierTargetLookup = state.GetComponentLookup<CarrierMiningTarget>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<PureDOTS.Runtime.Components.TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<PureDOTS.Runtime.Components.RewindState>();
            if (rewindState.Mode != PureDOTS.Runtime.Components.RewindMode.Record)
            {
                return;
            }

            _resourceStateLookup.Update(ref state);
            _resourceTypeLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _yieldLookup.Update(ref state);
            _carrierTargetLookup.Update(ref state);

            var resources = new NativeList<ResourceCandidate>(Allocator.Temp);
            foreach (var (resourceState, resourceTypeId, transform, entity) in SystemAPI
                         .Query<RefRO<ResourceSourceState>, RefRO<ResourceTypeId>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (resourceState.ValueRO.UnitsRemaining <= 0f)
                {
                    continue;
                }

                resources.Add(new ResourceCandidate
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    ResourceId = resourceTypeId.ValueRO.Value
                });
            }

            if (resources.Length == 0)
            {
                resources.Dispose();
                return;
            }

            var currentTick = timeState.Tick;

            foreach (var (order, vessel, transform, entity) in SystemAPI
                         .Query<RefRW<MiningOrder>, RefRO<MiningVessel>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!NeedsAssignment(order.ValueRO, vessel.ValueRO))
                {
                    continue;
                }

                var resourceId = order.ValueRO.ResourceId;
                var preferredTarget = Entity.Null;
                var hasPreferredTarget = false;

                var carrierEntity = vessel.ValueRO.CarrierEntity;
                if (carrierEntity != Entity.Null && _carrierTargetLookup.HasComponent(carrierEntity))
                {
                    var carrierTarget = _carrierTargetLookup[carrierEntity];
                    var candidateEntity = carrierTarget.TargetEntity;
                    if (candidateEntity != Entity.Null &&
                        _resourceStateLookup.HasComponent(candidateEntity) &&
                        _resourceStateLookup[candidateEntity].UnitsRemaining > 0f &&
                        _resourceTypeLookup.HasComponent(candidateEntity))
                    {
                        var targetResourceId = _resourceTypeLookup[candidateEntity].Value;
                        if (resourceId.IsEmpty || resourceId.Equals(targetResourceId))
                        {
                            resourceId = resourceId.IsEmpty ? targetResourceId : resourceId;
                            preferredTarget = candidateEntity;
                            hasPreferredTarget = true;
                        }
                    }
                }

                ResourceCandidate selection = default;
                var hasSelection = false;

                if (!hasPreferredTarget && resourceId.IsEmpty)
                {
                    hasSelection = TrySelectResource(vessel.ValueRO.CargoResourceType, transform.ValueRO.Position, resources, out selection);
                    if (!hasSelection)
                    {
                        continue;
                    }

                    resourceId = selection.ResourceId;
                }

                order.ValueRW.ResourceId = resourceId;
                order.ValueRW.Status = MiningOrderStatus.Pending;
                order.ValueRW.TargetEntity = Entity.Null;
                order.ValueRW.PreferredTarget = hasPreferredTarget
                    ? preferredTarget
                    : (hasSelection ? selection.Entity : Entity.Null);
                order.ValueRW.IssuedTick = currentTick;

                if (_yieldLookup.HasComponent(entity))
                {
                    var yield = _yieldLookup[entity];
                    if (yield.ResourceId.IsEmpty)
                    {
                        yield.ResourceId = resourceId;
                        _yieldLookup[entity] = yield;
                    }
                }
            }

            resources.Dispose();
        }

        private static bool NeedsAssignment(in MiningOrder order, in MiningVessel vessel)
        {
            if (vessel.CurrentCargo > 0.01f)
            {
                return false;
            }

            if (order.ResourceId.IsEmpty)
            {
                return true;
            }

            return order.Status == MiningOrderStatus.None || order.Status == MiningOrderStatus.Completed;
        }

        private static bool TrySelectResource(ResourceType desiredType, float3 position, NativeList<ResourceCandidate> resources, out ResourceCandidate selection)
        {
            selection = default;
            var bestDistance = float.MaxValue;
            var found = false;

            for (var i = 0; i < resources.Length; i++)
            {
                var candidate = resources[i];
                if (Space4XMiningResourceUtility.TryMapToResourceType(candidate.ResourceId, out var candidateType) &&
                    candidateType != desiredType)
                {
                    continue;
                }

                var distance = math.distancesq(position, candidate.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    selection = candidate;
                    found = true;
                }
            }

            if (found)
            {
                return true;
            }

            bestDistance = float.MaxValue;
            for (var i = 0; i < resources.Length; i++)
            {
                var candidate = resources[i];
                var distance = math.distancesq(position, candidate.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    selection = candidate;
                    found = true;
                }
            }

            return found;
        }

        private struct ResourceCandidate
        {
            public Entity Entity;
            public float3 Position;
            public FixedString64Bytes ResourceId;
        }
    }
}
