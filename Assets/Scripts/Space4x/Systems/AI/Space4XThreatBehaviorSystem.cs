using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Implements threat behavior profiles per ThreatBehaviorProfiles.md: pirates, space fauna, and
    /// environmental phenomena act according to alignments, outlooks, or intrinsic needs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAIMissionBoardSystem))]
    public partial struct Space4XThreatBehaviorSystem : ISystem
    {
        private ComponentLookup<ThreatProfile> _threatLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<FleetMovementBroadcast> _fleetBroadcastLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private ComponentLookup<Reputation> _reputationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _threatLookup = state.GetComponentLookup<ThreatProfile>(false);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(false);
            _fleetBroadcastLookup = state.GetComponentLookup<FleetMovementBroadcast>(false);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _reputationLookup = state.GetComponentLookup<Reputation>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _threatLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _fleetBroadcastLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _reputationLookup.Update(ref state);

            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime;

            foreach (var (threatRef, transformRef, entity) in
                     SystemAPI.Query<RefRW<ThreatProfile>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                var threat = threatRef.ValueRW;
                var transform = transformRef.ValueRW;

                switch (threat.Type)
                {
                    case ThreatProfileType.Pirate:
                        ProcessPirateBehavior(ref state, ref threat, ref transform, entity, deltaTime, currentTick);
                        break;

                    case ThreatProfileType.SpaceFauna:
                        ProcessFaunaBehavior(ref state, ref threat, ref transform, entity, deltaTime, currentTick);
                        break;

                    case ThreatProfileType.Environmental:
                        ProcessEnvironmentalBehavior(ref threat, entity);
                        break;
                }

                threatRef.ValueRW = threat;
                transformRef.ValueRW = transform;
            }
        }

        private void ProcessPirateBehavior(ref SystemState state, ref ThreatProfile threat, ref LocalTransform transform, Entity entity, float deltaTime, uint currentTick)
        {
                _ = state; // state required for SystemAPI queries (source generation)

                // Pirates evaluate risk vs reward; avoid fortified targets unless desperate
                // Alignment/outlook-based: range from opportunistic raiders to fanatics
                if (_alignmentLookup.HasComponent(entity))
                {
                    var alignment = _alignmentLookup[entity];
                    var chaos = AlignmentMath.Chaos(alignment);
                    var integrity = AlignmentMath.IntegrityNormalized(alignment);

                    // Chaotic pirates take more risks
                    threat.AggressionLevel = (half)math.lerp(0.3f, 0.9f, chaos);
                    
                    // Low integrity pirates may break deals immediately
                    threat.CanNegotiate = (byte)(integrity > 0.3f ? 1 : 0);
                }

                // Find targets: mining vessels and carriers
                if ((float)threat.AggressionLevel > 0.5f)
                {
                    Entity bestTarget = Entity.Null;
                    float bestDistance = float.MaxValue;
                    float3 targetPosition = float3.zero;
                    float searchRadius = 150f;
                    float searchRadiusSq = searchRadius * searchRadius;

                    // Find mining vessels (high value targets)
                    foreach (var (vessel, vesselTransform, vesselEntity) in SystemAPI.Query<RefRO<MiningVessel>, RefRO<LocalTransform>>()
                        .WithEntityAccess())
                    {
                        var distSq = math.distancesq(transform.Position, vesselTransform.ValueRO.Position);
                        if (distSq < searchRadiusSq && distSq < bestDistance && vessel.ValueRO.CurrentCargo > 0.1f)
                        {
                            bestTarget = vesselEntity;
                            bestDistance = distSq;
                            targetPosition = vesselTransform.ValueRO.Position;
                        }
                    }

                    // Find carriers (if no mining vessels nearby)
                    if (bestTarget == Entity.Null)
                    {
                        foreach (var (carrier, carrierTransform, carrierEntity) in SystemAPI.Query<RefRO<Carrier>, RefRO<LocalTransform>>()
                            .WithEntityAccess())
                        {
                            var distSq = math.distancesq(transform.Position, carrierTransform.ValueRO.Position);
                            if (distSq < searchRadiusSq && distSq < bestDistance)
                            {
                                bestTarget = carrierEntity;
                                bestDistance = distSq;
                                targetPosition = carrierTransform.ValueRO.Position;
                            }
                        }
                    }

                    // Move toward target if found
                    if (bestTarget != Entity.Null)
                    {
                        threat.TargetEntity = bestTarget;
                        MoveTowardTarget(ref transform, targetPosition, entity, 8f, deltaTime, currentTick);
                    }
                    else
                    {
                        threat.TargetEntity = Entity.Null;
                    }
                }
                else
                {
                    // Low aggression - patrol or hold position
                    threat.TargetEntity = Entity.Null;
                }

                // Success breeds aggression; repeated defeats push relocation
                // In full implementation, would track success/failure history
        }

        private void ProcessFaunaBehavior(ref SystemState state, ref ThreatProfile threat, ref LocalTransform transform, Entity entity, float deltaTime, uint currentTick)
        {
                _ = state; // state required for SystemAPI queries (source generation)

                // Fauna behaviors revolve around feeding, breeding, or defending territory
                // Generally neutral until provoked or when players encroach on habitats
                threat.AggressionLevel = (half)0.2f; // Low base aggression
                threat.CanNegotiate = 0; // No diplomacy with fauna

                // Defend territory: attack vessels that get too close
                float territoryRadius = 50f;
                float territoryRadiusSq = territoryRadius * territoryRadius;
                Entity nearestIntruder = Entity.Null;
                float nearestDistanceSq = float.MaxValue;
                float3 intruderPosition = float3.zero;

                // Check for nearby mining vessels (encroaching on habitat)
                foreach (var (vessel, vesselTransform, vesselEntity) in SystemAPI.Query<RefRO<MiningVessel>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
                {
                    var distSq = math.distancesq(transform.Position, vesselTransform.ValueRO.Position);
                    if (distSq < territoryRadiusSq && distSq < nearestDistanceSq)
                    {
                        nearestIntruder = vesselEntity;
                        nearestDistanceSq = distSq;
                        intruderPosition = vesselTransform.ValueRO.Position;
                    }
                }

                // Attack if intruder in territory
                if (nearestIntruder != Entity.Null)
                {
                    threat.TargetEntity = nearestIntruder;
                    threat.AggressionLevel = (half)0.8f; // Increase aggression when defending
                    MoveTowardTarget(ref transform, intruderPosition, entity, 6f, deltaTime, currentTick); // Fauna speed
                }
                else
                {
                    threat.TargetEntity = Entity.Null;
                    // Patrol territory (simple circular patrol)
                    // In full implementation, would use habitat boundaries
                }

                // In full implementation, would:
                // - Track habitat boundaries
                // - Respond to resource extraction (mining noise)
                // - Migrate if habitats collapse
        }

        private void MoveTowardTarget(ref LocalTransform transform, float3 targetPosition, Entity entity, float speed, float deltaTime, uint currentTick)
        {
                var toTarget = targetPosition - transform.Position;
                var distance = math.length(toTarget);

                if (distance < 0.1f)
                {
                    return; // Already at target
                }

                var direction = math.normalize(toTarget);
                transform.Position += direction * speed * deltaTime;

                // Update movement component if present
                if (_movementLookup.HasComponent(entity))
                {
                    var movement = _movementLookup.GetRefRW(entity).ValueRW;
                    movement.Velocity = direction * speed;
                    movement.IsMoving = 1;
                    movement.LastMoveTick = currentTick;
                }

                // Update fleet broadcast if present
                if (_fleetBroadcastLookup.HasComponent(entity))
                {
                    var broadcast = _fleetBroadcastLookup.GetRefRW(entity).ValueRW;
                    broadcast.Position = transform.Position;
                    broadcast.Velocity = direction * speed;
                    broadcast.LastUpdateTick = currentTick;
                }
            }

        private static void ProcessEnvironmentalBehavior(ref ThreatProfile threat, Entity entity)
        {
            // Environmental phenomena: semi-predictable patterns
            // Affects navigation and logistics
            threat.AggressionLevel = (half)0f; // Not aggressive, just hazardous
            threat.CanNegotiate = 0; // No negotiation with storms
        }
    }
}
