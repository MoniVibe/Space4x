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
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<CarrierDepartmentState> _departmentStateLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<CarrierTag> _carrierTagLookup;
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
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _departmentStateLookup = state.GetComponentLookup<CarrierDepartmentState>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _carrierTagLookup = state.GetComponentLookup<CarrierTag>(true);
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

            var missingAimAttackMove = SystemAPI.QueryBuilder()
                .WithAll<AttackMoveIntent>()
                .WithNone<VesselAimDirective>()
                .Build();
            if (!missingAimAttackMove.IsEmptyIgnoreFilter)
            {
                state.CompleteDependency();
                state.EntityManager.AddComponent<VesselAimDirective>(missingAimAttackMove);
            }

            var missingAimEngaged = SystemAPI.QueryBuilder()
                .WithAll<Space4XEngagement>()
                .WithNone<VesselAimDirective>()
                .Build();
            if (!missingAimEngaged.IsEmptyIgnoreFilter)
            {
                state.CompleteDependency();
                state.EntityManager.AddComponent<VesselAimDirective>(missingAimEngaged);
            }

            _transformLookup.Update(ref state);
            _aimLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _departmentStateLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _carrierTagLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _entityLookup.Update(ref state);

            foreach (var (aim, entity) in SystemAPI.Query<RefRW<VesselAimDirective>>()
                         .WithNone<AttackMoveIntent, Space4XEngagement>()
                         .WithEntityAccess())
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
                var distanceToAim = hasAim ? math.distance(transform.ValueRO.Position, aimPosition) : 0f;
                if (hasAim && maxRange > 0f && intent.ValueRO.KeepFiringWhileInRange != 0)
                {
                    if (distanceToAim <= maxRange)
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

                if (hasAim && maxRange > 0f && distanceToAim <= maxRange * 1.1f && _weaponLookup.HasBuffer(entity))
                {
                    var facingSkill = ResolveFacingSkill(entity);
                    var weapons = _weaponLookup[entity];
                    var facingDir = ResolveFacingDirection(entity, aimDir, transform.ValueRO.Rotation, distanceToAim, weapons, facingSkill, out var coverage);
                    if (coverage > 0.001f)
                    {
                        var facingBlend = math.saturate(math.lerp(0.25f, 0.85f, facingSkill));
                        aimDir = math.normalizesafe(math.lerp(aimDir, facingDir, facingBlend), aimDir);
                        aimWeight = math.max(aimWeight, facingBlend * math.lerp(0.5f, 1f, coverage));
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

            foreach (var (engagement, transform, entity) in SystemAPI
                         .Query<RefRO<Space4XEngagement>, RefRO<LocalTransform>>()
                         .WithNone<AttackMoveIntent>()
                         .WithEntityAccess())
            {
                if (engagement.ValueRO.Phase != EngagementPhase.Engaged)
                {
                    continue;
                }

                var aimTarget = engagement.ValueRO.PrimaryTarget;
                if (!TryResolveTarget(aimTarget, out var aimPosition))
                {
                    continue;
                }

                var fallbackDir = math.forward(transform.ValueRO.Rotation);
                var aimDir = math.normalizesafe(aimPosition - transform.ValueRO.Position, fallbackDir);
                var maxRange = ResolveMaxWeaponRange(entity);
                var distanceToAim = math.distance(transform.ValueRO.Position, aimPosition);
                var aimWeight = 0f;

                if (maxRange > 0f && distanceToAim <= maxRange * 1.2f && _weaponLookup.HasBuffer(entity))
                {
                    var facingSkill = ResolveFacingSkill(entity);
                    var weapons = _weaponLookup[entity];
                    var facingDir = ResolveFacingDirection(entity, aimDir, transform.ValueRO.Rotation, distanceToAim, weapons, facingSkill, out var coverage);
                    if (coverage > 0.001f)
                    {
                        var facingBlend = math.saturate(math.lerp(0.3f, 0.9f, facingSkill));
                        aimDir = math.normalizesafe(math.lerp(aimDir, facingDir, facingBlend), aimDir);
                        aimWeight = math.max(aimWeight, facingBlend * math.lerp(0.6f, 1f, coverage));
                    }
                }

                if (_aimLookup.HasComponent(entity))
                {
                    var directive = _aimLookup[entity];
                    directive.AimDirection = aimDir;
                    directive.AimWeight = math.saturate(aimWeight);
                    directive.AimTarget = aimTarget;
                    _aimLookup[entity] = directive;
                }
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

        private float ResolveFacingSkill(Entity entity)
        {
            float intelligence = 0.5f;
            if (_pilotLookup.HasComponent(entity))
            {
                var pilot = _pilotLookup[entity].Pilot;
                if (pilot != Entity.Null && _statsLookup.HasComponent(pilot))
                {
                    var stats = _statsLookup[pilot];
                    var command = math.saturate((float)stats.Command / 100f);
                    var tactics = math.saturate((float)stats.Tactics / 100f);
                    intelligence = math.saturate((command + tactics) * 0.5f);
                }
            }

            float cohesion = 0.5f;
            if (_departmentStateLookup.HasComponent(entity))
            {
                cohesion = math.saturate((float)_departmentStateLookup[entity].AverageCohesion);
            }

            var skill = 0.35f + 0.35f * intelligence + 0.3f * cohesion;
            return math.saturate(skill);
        }

        private float3 ResolveFacingDirection(
            Entity entity,
            float3 targetDirection,
            quaternion rotation,
            float distance,
            DynamicBuffer<WeaponMount> weapons,
            float skill,
            out float coverage)
        {
            coverage = 0f;
            if (weapons.Length == 0)
            {
                return targetDirection;
            }

            var forward = math.forward(rotation);
            var broadsidePreferred = IsBroadsidePreferred(entity);

            var bestForward = targetDirection;
            var bestScore = float.MinValue;

            EvaluateCandidate(entity, targetDirection, forward, targetDirection, distance, weapons, broadsidePreferred, skill, ref bestForward, ref bestScore, ref coverage);
            if (broadsidePreferred)
            {
                var left = RotateYaw(targetDirection, -90f);
                var right = RotateYaw(targetDirection, 90f);
                EvaluateCandidate(entity, left, forward, targetDirection, distance, weapons, broadsidePreferred, skill, ref bestForward, ref bestScore, ref coverage);
                EvaluateCandidate(entity, right, forward, targetDirection, distance, weapons, broadsidePreferred, skill, ref bestForward, ref bestScore, ref coverage);
            }

            if (skill > 0.6f)
            {
                var rear = RotateYaw(targetDirection, 180f);
                EvaluateCandidate(entity, rear, forward, targetDirection, distance, weapons, broadsidePreferred, skill, ref bestForward, ref bestScore, ref coverage);
            }

            return bestForward;
        }

        private void EvaluateCandidate(
            Entity entity,
            float3 candidateForward,
            float3 currentForward,
            float3 targetDirection,
            float distance,
            DynamicBuffer<WeaponMount> weapons,
            bool broadsidePreferred,
            float skill,
            ref float3 bestForward,
            ref float bestScore,
            ref float bestCoverage)
        {
            var coverage = ComputeArcCoverage(entity, candidateForward, targetDirection, distance, weapons, broadsidePreferred);
            var turnPenalty = 0.5f * (1f - math.clamp(math.dot(currentForward, candidateForward), -1f, 1f));
            var coverageWeight = math.lerp(0.65f, 0.85f, skill);
            var turnWeight = math.lerp(0.65f, 0.35f, skill);
            var score = coverage * coverageWeight - turnPenalty * turnWeight;

            if (broadsidePreferred && math.abs(math.dot(candidateForward, targetDirection)) < 0.25f)
            {
                score += 0.05f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestForward = candidateForward;
                bestCoverage = coverage;
            }
        }

        private float ComputeArcCoverage(
            Entity entity,
            float3 candidateForward,
            float3 targetDirection,
            float distance,
            DynamicBuffer<WeaponMount> weapons,
            bool broadsidePreferred)
        {
            var weaponsDisabled = false;
            DynamicBuffer<SubsystemHealth> subsystems = default;
            DynamicBuffer<SubsystemDisabled> disabled = default;

            var hasSubsystems = _subsystemLookup.HasBuffer(entity);
            var hasDisabled = _subsystemDisabledLookup.HasBuffer(entity);
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

            var totalWeight = 0f;
            var activeWeight = 0f;

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

                if (distance > mount.Weapon.MaxRange && mount.Weapon.MaxRange > 0f)
                {
                    continue;
                }

                var weight = ResolveWeaponWeight(mount.Weapon);
                totalWeight += weight;

                var arcDegrees = ResolveWeaponFireArcDegrees(mount.Weapon);
                var arcOffset = (float)mount.FireArcCenterOffsetDeg;
                if (broadsidePreferred && math.abs(arcOffset) < 0.01f)
                {
                    arcOffset = (i & 1) == 0 ? 90f : -90f;
                }

                if (IsWithinArc(candidateForward, targetDirection, arcDegrees, arcOffset))
                {
                    activeWeight += weight;
                }
            }

            return totalWeight > 0f ? activeWeight / totalWeight : 0f;
        }

        private bool IsBroadsidePreferred(Entity entity)
        {
            return _carrierLookup.HasComponent(entity) || _carrierTagLookup.HasComponent(entity);
        }

        private static float ResolveWeaponFireArcDegrees(in Space4XWeapon weapon)
        {
            if (weapon.FireArcDegrees > 0f)
            {
                return math.clamp(weapon.FireArcDegrees, 0f, 360f);
            }

            return weapon.Type switch
            {
                WeaponType.PointDefense => 320f,
                WeaponType.Flak => 260f,
                WeaponType.Missile => 200f,
                WeaponType.Torpedo => 140f,
                WeaponType.Kinetic => 140f,
                _ => 160f
            };
        }

        private static float ResolveWeaponWeight(in Space4XWeapon weapon)
        {
            var sizeWeight = 1f + 0.35f * (int)weapon.Size;
            var damageWeight = math.max(0f, weapon.BaseDamage) * 0.02f;
            return math.max(0.5f, sizeWeight + damageWeight);
        }

        private static bool IsWithinArc(float3 forward, float3 targetDirection, float arcDegrees, float arcOffsetDegrees)
        {
            if (arcDegrees <= 0f || arcDegrees >= 360f)
            {
                return true;
            }

            var center = RotateYaw(forward, arcOffsetDegrees);
            var halfArcRad = math.radians(arcDegrees * 0.5f);
            var minDot = math.cos(halfArcRad);
            return math.dot(center, targetDirection) >= minDot;
        }

        private static float3 RotateYaw(float3 direction, float degrees)
        {
            if (math.abs(degrees) < 0.001f)
            {
                return direction;
            }

            var rot = quaternion.AxisAngle(math.up(), math.radians(degrees));
            return math.normalizesafe(math.mul(rot, direction), direction);
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
