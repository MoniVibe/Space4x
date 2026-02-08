using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(Space4XEngagementIntentSystem))]
    public partial struct Space4XTargetingPlannerSystem : ISystem
    {
        private ComponentLookup<TargetSelectionProfile> _profileLookup;
        private ComponentLookup<TargetPriority> _priorityLookup;
        private ComponentLookup<ModuleTargetPolicy> _modulePolicyLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GroupTag>();

            _profileLookup = state.GetComponentLookup<TargetSelectionProfile>(false);
            _priorityLookup = state.GetComponentLookup<TargetPriority>(false);
            _modulePolicyLookup = state.GetComponentLookup<ModuleTargetPolicy>(true);
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

            _profileLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _modulePolicyLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (meta, intent, planner, members, entity) in SystemAPI
                         .Query<RefRO<GroupMeta>, RefRO<EngagementIntent>, RefRW<EngagementPlannerState>, DynamicBuffer<GroupMember>>()
                         .WithAll<GroupTag>()
                         .WithEntityAccess())
            {
                if (meta.ValueRO.Kind != GroupKind.StrikeWing && meta.ValueRO.Kind != GroupKind.FleetTaskUnit)
                {
                    continue;
                }

                var intentUpdated = intent.ValueRO.LastUpdateTick > planner.ValueRO.LastTargetingTick;
                var shouldUpdate = intentUpdated
                                   || timeState.Tick - planner.ValueRO.LastTargetingTick >= config.TacticUpdateIntervalTicks;
                if (!shouldUpdate)
                {
                    continue;
                }

                var template = ResolveProfileTemplate(intent.ValueRO.Kind);
                var policy = ResolveModulePolicy(intent.ValueRO.Kind);

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if ((member.Flags & GroupMemberFlags.Active) == 0)
                    {
                        continue;
                    }

                    var memberEntity = member.MemberEntity;
                    if (!IsValidEntity(memberEntity))
                    {
                        continue;
                    }

                    if (_profileLookup.HasComponent(memberEntity))
                    {
                        var current = _profileLookup[memberEntity];
                        var next = template;
                        next.MaxEngagementRange = current.MaxEngagementRange;

                        if (intent.ValueRO.Kind == EngagementIntentKind.Retreat && next.MaxEngagementRange > 0f)
                        {
                            next.MaxEngagementRange = math.max(0f, current.MaxEngagementRange * config.RetreatRangeScale);
                        }

                        _profileLookup[memberEntity] = next;

                        if (_priorityLookup.HasComponent(memberEntity))
                        {
                            var priority = _priorityLookup[memberEntity];
                            priority.ForceReevaluate = 1;
                            _priorityLookup[memberEntity] = priority;
                        }
                    }

                    if (_modulePolicyLookup.HasComponent(memberEntity))
                    {
                        ecb.SetComponent(memberEntity, new ModuleTargetPolicy { Kind = policy });
                    }
                    else
                    {
                        ecb.AddComponent(memberEntity, new ModuleTargetPolicy { Kind = policy });
                    }
                }

                planner.ValueRW.LastTargetingTick = timeState.Tick;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static TargetSelectionProfile ResolveProfileTemplate(EngagementIntentKind intent)
        {
            return intent switch
            {
                EngagementIntentKind.Fight => TargetSelectionProfile.NeutralizeThreats,
                EngagementIntentKind.BreakThrough => TargetSelectionProfile.NeutralizeThreats,
                EngagementIntentKind.Harass => TargetSelectionProfile.Opportunistic,
                EngagementIntentKind.Retreat => TargetSelectionProfile.NeutralizeThreats,
                EngagementIntentKind.Pursue => TargetSelectionProfile.Opportunistic,
                EngagementIntentKind.Screen => TargetSelectionProfile.DefendAllies,
                EngagementIntentKind.Rescue => TargetSelectionProfile.DefendAllies,
                EngagementIntentKind.Hold => TargetSelectionProfile.DefendAllies,
                _ => TargetSelectionProfile.Balanced
            };
        }

        private static ModuleTargetPolicyKind ResolveModulePolicy(EngagementIntentKind intent)
        {
            return intent switch
            {
                EngagementIntentKind.Fight => ModuleTargetPolicyKind.DisableFighting,
                EngagementIntentKind.BreakThrough => ModuleTargetPolicyKind.DisableFighting,
                EngagementIntentKind.Harass => ModuleTargetPolicyKind.DisableMobility,
                EngagementIntentKind.Retreat => ModuleTargetPolicyKind.DisableMobility,
                EngagementIntentKind.Pursue => ModuleTargetPolicyKind.DisableMobility,
                EngagementIntentKind.Screen => ModuleTargetPolicyKind.DisableFighting,
                EngagementIntentKind.Rescue => ModuleTargetPolicyKind.DisableFighting,
                _ => ModuleTargetPolicyKind.Default
            };
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }
    }
}
