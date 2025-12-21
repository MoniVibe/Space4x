using PureDOTS.Runtime.Components;
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
    /// Assigns targets and launches strike craft during headless scenarios.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XStrikeCraftSystem))]
    public partial struct Space4XStrikeCraftLaunchSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<PatrolStance> _patrolStanceLookup;
        private ComponentLookup<StrikeCraftProfile> _strikeCraftLookup;
        private ComponentLookup<ScenarioSide> _sideLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _patrolStanceLookup = state.GetComponentLookup<PatrolStance>(true);
            _strikeCraftLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _sideLookup = state.GetComponentLookup<ScenarioSide>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _alignmentLookup.Update(ref state);
            _patrolStanceLookup.Update(ref state);
            _strikeCraftLookup.Update(ref state);
            _sideLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var carriers = new NativeList<CarrierTarget>(Allocator.Temp);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<Carrier>()
                         .WithEntityAccess())
            {
                carriers.Add(new CarrierTarget
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    Side = _sideLookup.HasComponent(entity) ? _sideLookup[entity].Side : (byte)0
                });
            }

            if (carriers.Length == 0)
            {
                carriers.Dispose();
                return;
            }

            var assignedCarriers = new NativeHashSet<Entity>(math.max(1, carriers.Length), Allocator.Temp);
            foreach (var profile in SystemAPI.Query<RefRO<StrikeCraftProfile>>())
            {
                if (profile.ValueRO.Carrier == Entity.Null)
                {
                    continue;
                }

                if (profile.ValueRO.WingLeader != Entity.Null || profile.ValueRO.WingPosition != 0)
                {
                    assignedCarriers.Add(profile.ValueRO.Carrier);
                }
            }

            var wingLeaders = new NativeHashMap<Entity, Entity>(math.max(1, carriers.Length), Allocator.Temp);
            var wingCounts = new NativeHashMap<Entity, byte>(math.max(1, carriers.Length), Allocator.Temp);

            foreach (var (profile, entity) in SystemAPI.Query<RefRW<StrikeCraftProfile>>().WithEntityAccess())
            {
                if (profile.ValueRO.Carrier == Entity.Null)
                {
                    continue;
                }

                if (!assignedCarriers.Contains(profile.ValueRO.Carrier) &&
                    profile.ValueRO.WingLeader == Entity.Null &&
                    profile.ValueRO.WingPosition == 0)
                {
                    if (!wingLeaders.TryGetValue(profile.ValueRO.Carrier, out var leader))
                    {
                        wingLeaders[profile.ValueRO.Carrier] = entity;
                        wingCounts[profile.ValueRO.Carrier] = 1;
                        profile.ValueRW.WingLeader = Entity.Null;
                        profile.ValueRW.WingPosition = 0;
                    }
                    else
                    {
                        var position = wingCounts[profile.ValueRO.Carrier];
                        profile.ValueRW.WingLeader = leader;
                        profile.ValueRW.WingPosition = position;
                        wingCounts[profile.ValueRO.Carrier] = (byte)math.min(byte.MaxValue - 1, position + 1);
                    }
                }

                if (profile.ValueRO.Target != Entity.Null ||
                    (profile.ValueRO.Phase != AttackRunPhase.Docked && profile.ValueRO.Phase != AttackRunPhase.CombatAirPatrol))
                {
                    continue;
                }

                var origin = float3.zero;
                if (_transformLookup.HasComponent(entity))
                {
                    origin = _transformLookup[entity].Position;
                }
                else if (_transformLookup.HasComponent(profile.ValueRO.Carrier))
                {
                    origin = _transformLookup[profile.ValueRO.Carrier].Position;
                }

                var ownSide = _sideLookup.HasComponent(profile.ValueRO.Carrier) ? _sideLookup[profile.ValueRO.Carrier].Side : (byte)0;
                var hasSide = _sideLookup.HasComponent(profile.ValueRO.Carrier);

                var lawfulness = 0.5f;
                var chaos = 0.5f;
                if (_alignmentLookup.HasComponent(entity))
                {
                    var alignment = _alignmentLookup[entity];
                    lawfulness = AlignmentMath.Lawfulness(alignment);
                    chaos = AlignmentMath.Chaos(alignment);
                }

                var focusFireStrict = false;
                var leaderTarget = Entity.Null;
                if (profile.ValueRO.WingLeader != Entity.Null && _strikeCraftLookup.HasComponent(profile.ValueRO.WingLeader))
                {
                    var leaderProfile = _strikeCraftLookup[profile.ValueRO.WingLeader];
                    leaderTarget = leaderProfile.Target;
                    focusFireStrict = leaderTarget != Entity.Null;
                }

                if (_patrolStanceLookup.HasComponent(profile.ValueRO.Carrier))
                {
                    var stance = _patrolStanceLookup[profile.ValueRO.Carrier];
                    if (stance.Stance == VesselStanceMode.Aggressive)
                    {
                        focusFireStrict = true;
                    }
                }

                if (lawfulness >= 0.65f)
                {
                    focusFireStrict = true;
                }

                Entity bestTarget = Entity.Null;
                if (focusFireStrict && leaderTarget != Entity.Null)
                {
                    bestTarget = leaderTarget;
                }
                else if (chaos >= 0.6f && profile.ValueRO.Role == StrikeCraftRole.Bomber && carriers.Length > 1)
                {
                    bestTarget = SelectChaoticTarget(carriers, profile.ValueRO.Carrier, hasSide, ownSide, entity.Index);
                }
                else
                {
                    bestTarget = SelectNearestTarget(carriers, profile.ValueRO.Carrier, hasSide, ownSide, origin);
                }

                if (bestTarget != Entity.Null)
                {
                    profile.ValueRW.Target = bestTarget;
                    profile.ValueRW.Phase = AttackRunPhase.Launching;
                    profile.ValueRW.PhaseTimer = 0;
                }
                else
                {
                    profile.ValueRW.Phase = AttackRunPhase.CombatAirPatrol;
                }
            }

            assignedCarriers.Dispose();
            wingLeaders.Dispose();
            wingCounts.Dispose();
            carriers.Dispose();
        }

        private struct CarrierTarget
        {
            public Entity Entity;
            public float3 Position;
            public byte Side;
        }

        private static bool IsCombatRole(StrikeCraftRole role)
        {
            return role == StrikeCraftRole.Fighter ||
                   role == StrikeCraftRole.Interceptor ||
                   role == StrikeCraftRole.Bomber ||
                   role == StrikeCraftRole.Suppression;
        }

        private static Entity SelectNearestTarget(
            NativeList<CarrierTarget> carriers,
            Entity carrierEntity,
            bool hasSide,
            byte ownSide,
            float3 origin)
        {
            Entity bestTarget = Entity.Null;
            var bestDistSq = float.MaxValue;

            for (int i = 0; i < carriers.Length; i++)
            {
                var candidate = carriers[i];
                if (candidate.Entity == carrierEntity)
                {
                    continue;
                }

                if (hasSide && candidate.Side == ownSide)
                {
                    continue;
                }

                var distSq = math.lengthsq(candidate.Position - origin);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTarget = candidate.Entity;
                }
            }

            return bestTarget;
        }

        private static Entity SelectChaoticTarget(
            NativeList<CarrierTarget> carriers,
            Entity carrierEntity,
            bool hasSide,
            byte ownSide,
            int entityIndex)
        {
            var offset = (uint)math.abs(entityIndex) + 1u;
            var startIndex = (int)(offset % (uint)carriers.Length);

            for (int i = 0; i < carriers.Length; i++)
            {
                var candidate = carriers[(startIndex + i) % carriers.Length];
                if (candidate.Entity == carrierEntity)
                {
                    continue;
                }

                if (hasSide && candidate.Side == ownSide)
                {
                    continue;
                }

                return candidate.Entity;
            }

            return Entity.Null;
        }
    }
}
