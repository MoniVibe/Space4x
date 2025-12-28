using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4XVesselIntentBridgeSystem))]
    [UpdateBefore(typeof(VesselMovementSystem))]
    public partial struct VesselAttackMoveGuidanceSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselAimDirective> _aimLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private ComponentLookup<TargetPriority> _priorityLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private BufferLookup<WeaponMount> _weaponLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _aimLookup = state.GetComponentLookup<VesselAimDirective>(false);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            _priorityLookup = state.GetComponentLookup<TargetPriority>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMount>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(true);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
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

            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }

            var missingAim = SystemAPI.QueryBuilder()
                .WithAll<AttackMoveIntent>()
                .WithNone<VesselAimDirective>()
                .Build();
            if (!missingAim.IsEmptyIgnoreFilter)
            {
                state.CompleteDependency();
                state.EntityManager.AddComponent<VesselAimDirective>(missingAim);
            }

            _transformLookup.Update(ref state);
            _aimLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _entityLookup.Update(ref state);

            foreach (var (aim, entity) in SystemAPI.Query<RefRW<VesselAimDirective>>().WithNone<AttackMoveIntent>().WithEntityAccess())
            {
                var directive = aim.ValueRW;
                directive.AimWeight = 0f;
                directive.AimTarget = Entity.Null;
                directive.AimDirection = float3.zero;
                aim.ValueRW = directive;
            }

            foreach (var (intent, aiState, transform, entity) in SystemAPI
                         .Query<RefRW<AttackMoveIntent>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (intent.ValueRO.StartTick == 0)
                {
                    intent.ValueRW.StartTick = timeState.Tick;
                }

                aiState.ValueRW.TargetPosition = intent.ValueRO.Destination;

                var moveDir = ResolveMoveDirection(intent.ValueRO.Destination, transform.ValueRO.Position);
                var aimTarget = ResolveAimTarget(intent.ValueRO, entity);
                var hasAim = TryResolveTarget(aimTarget, out var aimPosition);

                var aimDir = hasAim
                    ? math.normalizesafe(aimPosition - transform.ValueRO.Position, moveDir)
                    : moveDir;

                var maxRange = ResolveMaxWeaponRange(entity);
                var aimWeight = 0f;
                if (hasAim && maxRange > 0f && intent.ValueRO.KeepFiringWhileInRange != 0)
                {
                    var distance = math.distance(transform.ValueRO.Position, aimPosition);
                    if (distance <= maxRange)
                    {
                        var tuning = ResolveStanceTuning(entity, stanceConfig);
                        var bearingWeight = math.max(0f, tuning.AttackMoveBearingWeight);
                        var destinationWeight = math.max(0f, tuning.AttackMoveDestinationWeight);
                        var weightSum = bearingWeight + destinationWeight;
                        var stanceWeight = weightSum > 1e-4f ? bearingWeight / weightSum : 0f;
                        var bearingDot = math.dot(moveDir, aimDir);
                        var coneError = math.saturate((0.7f - bearingDot) / 0.3f);
                        var bias = 0.4f + 0.6f * coneError;
                        aimWeight = math.saturate(stanceWeight * bias);
                    }
                }

                if (!hasAim)
                {
                    aimTarget = Entity.Null;
                }

                if (_aimLookup.HasComponent(entity))
                {
                    var directive = _aimLookup[entity];
                    directive.AimDirection = aimDir;
                    directive.AimWeight = aimWeight;
                    directive.AimTarget = aimTarget;
                    _aimLookup[entity] = directive;
                }

                aiState.ValueRW.TargetEntity = aimTarget;
            }
        }

        private StanceTuningEntry ResolveStanceTuning(Entity entity, in Space4XStanceTuningConfig config)
        {
            var stance = _stanceLookup.HasComponent(entity)
                ? _stanceLookup[entity].CurrentStance
                : VesselStanceMode.Balanced;
            return config.Resolve(stance);
        }

        private Entity ResolveAimTarget(in AttackMoveIntent intent, Entity entity)
        {
            var target = intent.EngageTarget;
            if (IsValidTarget(target))
            {
                return target;
            }

            if (intent.AcquireTargetsAlongRoute != 0 && _priorityLookup.HasComponent(entity))
            {
                var priority = _priorityLookup[entity];
                if (IsValidTarget(priority.CurrentTarget))
                {
                    return priority.CurrentTarget;
                }
            }

            if (intent.KeepFiringWhileInRange != 0 && _engagementLookup.HasComponent(entity))
            {
                var engagement = _engagementLookup[entity];
                if (IsValidTarget(engagement.PrimaryTarget))
                {
                    return engagement.PrimaryTarget;
                }
            }

            return Entity.Null;
        }

        private float ResolveMaxWeaponRange(Entity entity)
        {
            if (!_weaponLookup.HasBuffer(entity))
            {
                return 0f;
            }

            var maxRange = 0f;
            var weapons = _weaponLookup[entity];
            var hasSubsystems = _subsystemLookup.HasBuffer(entity);
            var hasDisabled = _subsystemDisabledLookup.HasBuffer(entity);
            DynamicBuffer<SubsystemHealth> subsystems = default;
            DynamicBuffer<SubsystemDisabled> disabled = default;
            var weaponsDisabled = false;
            if (hasSubsystems)
            {
                subsystems = _subsystemLookup[entity];
                if (hasDisabled)
                {
                    disabled = _subsystemDisabledLookup[entity];
                    weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, disabled, SubsystemType.Weapons);
                }
                else
                {
                    weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, SubsystemType.Weapons);
                }
            }
            for (int i = 0; i < weapons.Length; i++)
            {
                var mount = weapons[i];
                if (mount.IsEnabled == 0)
                {
                    continue;
                }
                if (weaponsDisabled)
                {
                    if (hasDisabled)
                    {
                        if (Space4XSubsystemUtility.IsWeaponMountDisabled(entity, i, subsystems, disabled))
                        {
                            continue;
                        }
                    }
                    else if (Space4XSubsystemUtility.ShouldDisableMount(entity, i))
                    {
                        continue;
                    }
                }
                maxRange = math.max(maxRange, mount.Weapon.MaxRange);
            }

            return maxRange;
        }

        private float3 ResolveMoveDirection(float3 destination, float3 position)
        {
            var toTarget = destination - position;
            return math.normalizesafe(toTarget, new float3(0f, 0f, 1f));
        }

        private bool TryResolveTarget(Entity target, out float3 position)
        {
            position = default;
            if (!IsValidTarget(target))
            {
                return false;
            }

            if (!_transformLookup.HasComponent(target))
            {
                return false;
            }

            position = _transformLookup[target].Position;
            return true;
        }

        private bool IsValidTarget(Entity target)
        {
            return target != Entity.Null && _entityLookup.Exists(target);
        }
    }
}
