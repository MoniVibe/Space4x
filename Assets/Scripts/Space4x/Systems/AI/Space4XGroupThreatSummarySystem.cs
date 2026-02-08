using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeWingGroupSyncSystem))]
    public partial struct Space4XGroupThreatSummarySystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private ComponentLookup<TargetPriority> _priorityLookup;
        private BufferLookup<TargetCandidate> _candidateLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GroupTag>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _priorityLookup = state.GetComponentLookup<TargetPriority>(true);
            _candidateLookup = state.GetBufferLookup<TargetCandidate>(true);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var config = EngagementDoctrineConfig.Default;
            if (SystemAPI.TryGetSingleton<EngagementDoctrineConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            _transformLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _candidateLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            foreach (var (summary, meta, members, transform, entity) in SystemAPI
                         .Query<RefRW<EngagementThreatSummary>, RefRO<GroupMeta>, DynamicBuffer<GroupMember>, RefRO<LocalTransform>>()
                         .WithAll<GroupTag>()
                         .WithEntityAccess())
            {
                if (meta.ValueRO.Kind != GroupKind.StrikeWing
                    && meta.ValueRO.Kind != GroupKind.MiningWing
                    && meta.ValueRO.Kind != GroupKind.FleetTaskUnit)
                {
                    continue;
                }

                if (timeState.Tick - summary.ValueRO.LastUpdateTick < config.ThreatUpdateIntervalTicks)
                {
                    continue;
                }

                int friendlyCount = 0;
                float friendlyHullSum = 0f;
                float friendlyStrength = 0f;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if ((member.Flags & GroupMemberFlags.Active) == 0)
                    {
                        continue;
                    }

                    var memberEntity = member.MemberEntity;
                    if (memberEntity == Entity.Null || !_entityInfoLookup.Exists(memberEntity))
                    {
                        continue;
                    }

                    friendlyCount++;
                    if (_hullLookup.HasComponent(memberEntity))
                    {
                        var hull = _hullLookup[memberEntity];
                        friendlyStrength += hull.Current;
                        friendlyHullSum += hull.Ratio;
                    }
                    else
                    {
                        friendlyStrength += 1f;
                        friendlyHullSum += 1f;
                    }
                }

                float friendlyAverageHull = friendlyCount > 0 ? friendlyHullSum / friendlyCount : 0f;

                Entity primaryThreat = Entity.Null;
                if (_priorityLookup.HasComponent(entity))
                {
                    var priority = _priorityLookup[entity];
                    if (IsValidEntity(priority.CurrentTarget))
                    {
                        primaryThreat = priority.CurrentTarget;
                    }
                }

                float primaryThreatDistance = float.MaxValue;
                float primaryThreatHullRatio = 0f;
                if (IsValidEntity(primaryThreat) && _transformLookup.HasComponent(primaryThreat))
                {
                    primaryThreatDistance = math.distance(transform.ValueRO.Position, _transformLookup[primaryThreat].Position);
                    if (_hullLookup.HasComponent(primaryThreat))
                    {
                        primaryThreatHullRatio = _hullLookup[primaryThreat].Ratio;
                    }
                }

                int threatCount = 0;
                float threatStrength = 0f;
                float threatHullSum = 0f;

                if (_candidateLookup.HasBuffer(entity))
                {
                    var candidates = _candidateLookup[entity];
                    var limit = math.max(1, config.ThreatSampleLimit);
                    for (int i = 0; i < candidates.Length && threatCount < limit; i++)
                    {
                        var candidate = candidates[i];
                        if (candidate.Entity == Entity.Null || !_entityInfoLookup.Exists(candidate.Entity))
                        {
                            continue;
                        }

                        threatCount++;
                        if (_hullLookup.HasComponent(candidate.Entity))
                        {
                            var hull = _hullLookup[candidate.Entity];
                            threatStrength += hull.Current;
                            threatHullSum += hull.Ratio;
                        }
                        else
                        {
                            threatStrength += 1f;
                            threatHullSum += 1f;
                        }
                    }
                }

                if (threatCount == 0 && IsValidEntity(primaryThreat))
                {
                    threatCount = 1;
                    if (_hullLookup.HasComponent(primaryThreat))
                    {
                        var hull = _hullLookup[primaryThreat];
                        threatStrength = hull.Current;
                        threatHullSum = hull.Ratio;
                    }
                    else
                    {
                        threatStrength = 1f;
                        threatHullSum = 1f;
                    }
                }

                float threatAverageHull = threatCount > 0 ? threatHullSum / threatCount : 0f;
                float totalStrength = math.max(1f, friendlyStrength + threatStrength);
                float threatPressure = threatStrength / totalStrength;
                float advantageRatio = threatStrength > 0f ? friendlyStrength / threatStrength : 1f;

                float escapeProbability = 1f;
                if (IsValidEntity(primaryThreat) && primaryThreatDistance < float.MaxValue)
                {
                    escapeProbability = math.saturate(primaryThreatDistance / math.max(1f, config.EscapeDistance));
                }

                summary.ValueRW.PrimaryThreat = primaryThreat;
                summary.ValueRW.PrimaryThreatDistance = primaryThreatDistance;
                summary.ValueRW.PrimaryThreatHullRatio = primaryThreatHullRatio;
                summary.ValueRW.FriendlyAverageHull = friendlyAverageHull;
                summary.ValueRW.ThreatAverageHull = threatAverageHull;
                summary.ValueRW.FriendlyStrength = friendlyStrength;
                summary.ValueRW.ThreatStrength = threatStrength;
                summary.ValueRW.ThreatPressure = threatPressure;
                summary.ValueRW.AdvantageRatio = advantageRatio;
                summary.ValueRW.FriendlyCount = friendlyCount;
                summary.ValueRW.ThreatCount = threatCount;
                summary.ValueRW.EscapeProbability = escapeProbability;
                summary.ValueRW.LastUpdateTick = timeState.Tick;
            }
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }
    }
}
