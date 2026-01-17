using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using Space4X.Physics;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XHeadlessCollisionPhasingSystem : ISystem
    {
        private const uint OverlapTicksThreshold = 30;
        private const uint CollisionGraceTicks = 5;
        private const float OverlapRatio = 0.95f;
        private const float MinSeparationEpsilon = 0.05f;

        private EntityQuery _missingProbeQuery;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SpaceColliderData> _colliderLookup;
        private BufferLookup<PhysicsCollisionEventElement> _collisionLookup;
        private ComponentLookup<DockedTag> _dockedLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private byte _done;
        private uint _collisionEventCount;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();

            _missingProbeQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MiningState>() },
                None = new[] { ComponentType.ReadOnly<Space4XCollisionProbeState>() }
            });

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _colliderLookup = state.GetComponentLookup<SpaceColliderData>(true);
            _collisionLookup = state.GetBufferLookup<PhysicsCollisionEventElement>(true);
            _dockedLookup = state.GetComponentLookup<DockedTag>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            if (!_missingProbeQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<Space4XCollisionProbeState>(_missingProbeQuery);
            }

            _transformLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _collisionLookup.Update(ref state);
            _dockedLookup.Update(ref state);
            _carrierLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = timeState.Tick;
            AccumulateCollisionEvents(ref state);

            foreach (var (miningState, transform, collider, probe, entity) in SystemAPI
                         .Query<RefRO<MiningState>, RefRO<LocalTransform>, RefRO<SpaceColliderData>, RefRW<Space4XCollisionProbeState>>()
                         .WithEntityAccess())
            {
                var target = miningState.ValueRO.ActiveTarget;
                if (target == Entity.Null)
                {
                    ResetProbe(ref probe.ValueRW);
                    continue;
                }

                var phase = miningState.ValueRO.Phase;
                var isMiningPhase = phase == MiningPhase.ApproachTarget ||
                                    phase == MiningPhase.Latching ||
                                    phase == MiningPhase.Mining;
                if (!isMiningPhase || _dockedLookup.HasComponent(entity))
                {
                    ResetProbe(ref probe.ValueRW);
                    continue;
                }

                if (_carrierLookup.HasComponent(target))
                {
                    ResetProbe(ref probe.ValueRW);
                    continue;
                }

                if (!_transformLookup.HasComponent(target) || !_colliderLookup.HasComponent(target))
                {
                    ResetProbe(ref probe.ValueRW);
                    continue;
                }

                var targetTransform = _transformLookup[target];
                var targetCollider = _colliderLookup[target];
                var radiusSum = math.max(0.1f, collider.ValueRO.Radius + targetCollider.Radius);
                var distance = math.distance(transform.ValueRO.Position, targetTransform.Position);
                var overlap = distance <= radiusSum * OverlapRatio;
                var penetration = radiusSum - distance;

                var probeState = probe.ValueRW;
                var lastCollisionTick = ResolveLastCollisionTick(entity, target, probeState.LastCollisionTick);
                probeState.LastCollisionTick = lastCollisionTick;

                if (!overlap)
                {
                    ResetProbe(ref probeState);
                    probe.ValueRW = probeState;
                    continue;
                }

                if (probeState.OverlapTicks == 0)
                {
                    probeState.OverlapStartTick = tick;
                }

                if (tick > lastCollisionTick + CollisionGraceTicks)
                {
                    probeState.OverlapTicks++;
                }
                else
                {
                    probeState.OverlapTicks = 0;
                }

                if (probeState.Reported == 0 &&
                    probeState.OverlapTicks >= OverlapTicksThreshold &&
                    tick > lastCollisionTick + CollisionGraceTicks &&
                    penetration > MinSeparationEpsilon)
                {
                    AppendBlackCat(ref state, entity, target, probeState.OverlapStartTick, tick, probeState.OverlapTicks, penetration, lastCollisionTick);
                    probeState.Reported = 1;
                }

                probe.ValueRW = probeState;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (tick >= runtime.EndTick)
            {
                EmitCollisionSummary(ref state);
                _done = 1;
            }
        }

        private uint ResolveLastCollisionTick(Entity entity, Entity target, uint fallback)
        {
            if (!_collisionLookup.HasBuffer(entity))
            {
                return fallback;
            }

            var buffer = _collisionLookup[entity];
            var lastTick = fallback;
            for (var i = 0; i < buffer.Length; i++)
            {
                var evt = buffer[i];
                if (evt.OtherEntity != target)
                {
                    continue;
                }

                if (evt.EventType == PhysicsCollisionEventType.TriggerEnter || evt.EventType == PhysicsCollisionEventType.TriggerExit)
                {
                    continue;
                }

                if (evt.Tick > lastTick)
                {
                    lastTick = evt.Tick;
                }
            }

            return lastTick;
        }

        private void AppendBlackCat(
            ref SystemState state,
            Entity vessel,
            Entity target,
            uint startTick,
            uint endTick,
            uint overlapTicks,
            float penetration,
            uint lastCollisionTick)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("COLLISION_PHASING"),
                Primary = vessel,
                Secondary = target,
                StartTick = startTick,
                EndTick = endTick,
                MetricA = overlapTicks,
                MetricB = penetration,
                MetricC = lastCollisionTick,
                MetricD = 0f,
                Classification = 1
            });
        }

        private static void ResetProbe(ref Space4XCollisionProbeState probe)
        {
            probe.OverlapTicks = 0;
            probe.OverlapStartTick = 0;
            probe.LastCollisionTick = 0;
            probe.Reported = 0;
        }

        private void AccumulateCollisionEvents(ref SystemState state)
        {
            using var query = SystemAPI.QueryBuilder()
                .WithAll<PhysicsCollisionEventElement>()
                .Build();
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var count = 0u;
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<PhysicsCollisionEventElement>>())
            {
                if (buffer.Length > 0)
                {
                    count += (uint)buffer.Length;
                }
            }

            if (count > 0)
            {
                _collisionEventCount += count;
            }
        }

        private void EmitCollisionSummary(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.collision.event_count"), _collisionEventCount);
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }
    }

    public struct Space4XCollisionProbeState : IComponentData
    {
        public uint OverlapTicks;
        public uint OverlapStartTick;
        public uint LastCollisionTick;
        public byte Reported;
    }
}
