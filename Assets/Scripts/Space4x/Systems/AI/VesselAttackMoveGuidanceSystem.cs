using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Steering;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems;
using Unity.Burst;
using Unity.Collections;
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
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<CarrierDepartmentState> _departmentStateLookup;
        private BufferLookup<DepartmentStatsBuffer> _departmentStatsLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<CarrierTag> _carrierTagLookup;
        private BufferLookup<WeaponMount> _weaponLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private ComponentLookup<Space4XOrbitalBandState> _orbitalBandLookup;
        private EntityStorageInfoLookup _entityLookup;
        private FixedString64Bytes _roleNavigationOfficer;
        private FixedString64Bytes _roleWeaponsOfficer;

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
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _departmentStateLookup = state.GetComponentLookup<CarrierDepartmentState>(true);
            _departmentStatsLookup = state.GetBufferLookup<DepartmentStatsBuffer>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _carrierTagLookup = state.GetComponentLookup<CarrierTag>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMount>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(true);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(true);
            _orbitalBandLookup = state.GetComponentLookup<Space4XOrbitalBandState>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
            _roleNavigationOfficer = default;
            _roleNavigationOfficer.Append('s');
            _roleNavigationOfficer.Append('h');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('p');
            _roleNavigationOfficer.Append('.');
            _roleNavigationOfficer.Append('n');
            _roleNavigationOfficer.Append('a');
            _roleNavigationOfficer.Append('v');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('g');
            _roleNavigationOfficer.Append('a');
            _roleNavigationOfficer.Append('t');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('o');
            _roleNavigationOfficer.Append('n');
            _roleNavigationOfficer.Append('_');
            _roleNavigationOfficer.Append('o');
            _roleNavigationOfficer.Append('f');
            _roleNavigationOfficer.Append('f');
            _roleNavigationOfficer.Append('i');
            _roleNavigationOfficer.Append('c');
            _roleNavigationOfficer.Append('e');
            _roleNavigationOfficer.Append('r');

            _roleWeaponsOfficer = default;
            _roleWeaponsOfficer.Append('s');
            _roleWeaponsOfficer.Append('h');
            _roleWeaponsOfficer.Append('i');
            _roleWeaponsOfficer.Append('p');
            _roleWeaponsOfficer.Append('.');
            _roleWeaponsOfficer.Append('w');
            _roleWeaponsOfficer.Append('e');
            _roleWeaponsOfficer.Append('a');
            _roleWeaponsOfficer.Append('p');
            _roleWeaponsOfficer.Append('o');
            _roleWeaponsOfficer.Append('n');
            _roleWeaponsOfficer.Append('s');
            _roleWeaponsOfficer.Append('_');
            _roleWeaponsOfficer.Append('o');
            _roleWeaponsOfficer.Append('f');
            _roleWeaponsOfficer.Append('f');
            _roleWeaponsOfficer.Append('i');
            _roleWeaponsOfficer.Append('c');
            _roleWeaponsOfficer.Append('e');
            _roleWeaponsOfficer.Append('r');
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

            var projectileSpeedMultiplier = 1f;
            if (SystemAPI.TryGetSingleton<Space4XWeaponTuningConfig>(out var weaponTuning))
            {
                projectileSpeedMultiplier = math.max(0f, weaponTuning.ProjectileSpeedMultiplier);
            }

            var movementTuning = Space4XMovementTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMovementTuningConfig>(out var movementTuningSingleton))
            {
                movementTuning = movementTuningSingleton;
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
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _departmentStateLookup.Update(ref state);
            _departmentStatsLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _carrierTagLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _orbitalBandLookup.Update(ref state);
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

                var rangeScale = ResolveRangeScale(entity);
                var maxRange = ResolveMaxWeaponRange(entity) * rangeScale;
                var distanceToAim = hasAim ? math.distance(transform.ValueRO.Position, aimPosition) : 0f;
                var contactRange = ResolveContactRange(maxRange, movementTuning);
                var inContact = hasAim && maxRange > 0f && distanceToAim <= contactRange;

                var facingSkill = inContact ? ResolveFacingSkill(entity) : 0f;
                if (inContact && aimTarget != Entity.Null && _movementLookup.HasComponent(aimTarget) && _weaponLookup.HasBuffer(entity))
                {
                    if (TryResolveLeadDirection(entity, aimTarget, aimPosition, transform.ValueRO.Position, projectileSpeedMultiplier, out var leadDir))
                    {
                        var leadBlend = math.saturate(math.lerp(0.2f, 0.6f, facingSkill));
                        aimDir = math.normalizesafe(math.lerp(aimDir, leadDir, leadBlend), aimDir);
                    }
                }

                var aimWeight = 0f;
                if (inContact && intent.ValueRO.KeepFiringWhileInRange != 0)
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

                if (inContact && _weaponLookup.HasBuffer(entity))
                {
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
                var rangeScale = ResolveRangeScale(entity);
                var maxRange = ResolveMaxWeaponRange(entity) * rangeScale;
                var distanceToAim = math.distance(transform.ValueRO.Position, aimPosition);
                var contactRange = ResolveContactRange(maxRange, movementTuning);
                var inContact = maxRange > 0f && distanceToAim <= contactRange;
                var aimWeight = 0f;

                var facingSkill = inContact ? ResolveFacingSkill(entity) : 0f;
                if (inContact && _movementLookup.HasComponent(aimTarget) && _weaponLookup.HasBuffer(entity))
                {
                    if (TryResolveLeadDirection(entity, aimTarget, aimPosition, transform.ValueRO.Position, projectileSpeedMultiplier, out var leadDir))
                    {
                        var leadBlend = math.saturate(math.lerp(0.2f, 0.6f, facingSkill));
                        aimDir = math.normalizesafe(math.lerp(aimDir, leadDir, leadBlend), aimDir);
                    }
                }

                if (inContact && _weaponLookup.HasBuffer(entity))
                {
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

        private static float ResolveContactRange(float maxRange, in Space4XMovementTuningConfig tuning)
        {
            if (maxRange <= 0f)
            {
                return 0f;
            }

            var scale = math.max(0f, tuning.ContactRangeScale);
            var min = math.max(0f, tuning.ContactRangeMin);
            return math.max(min, maxRange * scale);
        }

        private float ResolveRangeScale(Entity entity)
        {
            if (_orbitalBandLookup.HasComponent(entity))
            {
                var band = _orbitalBandLookup[entity];
                if (band.InBand != 0)
                {
                    return math.max(0.01f, band.RangeScale);
                }
            }

            return 1f;
        }

        private bool TryResolveLeadDirection(
            Entity entity,
            Entity target,
            float3 targetPosition,
            float3 origin,
            float projectileSpeedMultiplier,
            out float3 leadDirection)
        {
            leadDirection = default;
            if (target == Entity.Null || !_movementLookup.HasComponent(target))
            {
                return false;
            }

            var projectileSpeed = ResolveLeadProjectileSpeed(entity, projectileSpeedMultiplier);
            if (projectileSpeed <= 0f)
            {
                return false;
            }

            var targetVelocity = _movementLookup[target].Velocity;
            if (SteeringPrimitives.LeadInterceptPoint(targetPosition, targetVelocity, origin, projectileSpeed, out var interceptPoint, out _))
            {
                leadDirection = math.normalizesafe(interceptPoint - origin, targetPosition - origin);
                return true;
            }

            return false;
        }

        private float ResolveLeadProjectileSpeed(Entity entity, float projectileSpeedMultiplier)
        {
            if (!_weaponLookup.HasBuffer(entity))
            {
                return 0f;
            }

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

            var maxSpeed = 0f;
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

                var projectileSpeed = ResolveProjectileSpeed(mount.Weapon, projectileSpeedMultiplier);
                maxSpeed = math.max(maxSpeed, projectileSpeed);
            }

            return maxSpeed;
        }

        private static float ResolveProjectileSpeed(in Space4XWeapon weapon, float projectileSpeedMultiplier)
        {
            var baseSpeed = weapon.Type switch
            {
                WeaponType.Laser => 400f,
                WeaponType.PointDefense => 450f,
                WeaponType.Plasma => 320f,
                WeaponType.Ion => 280f,
                WeaponType.Kinetic => 220f,
                WeaponType.Flak => 200f,
                WeaponType.Missile => 140f,
                WeaponType.Torpedo => 90f,
                _ => 200f
            };

            var sizeScale = 1f + 0.25f * (int)weapon.Size;
            return baseSpeed * sizeScale * math.max(0f, projectileSpeedMultiplier);
        }

        private float ResolveFacingSkill(Entity entity)
        {
            var navigationSkill = ResolveNavigationSkill(entity);
            var weaponsSkill = ResolveWeaponsOfficerSkill(entity);
            var combatCohesion = ResolveDepartmentCohesion(entity, DepartmentType.Combat);
            var weaponsCoordination = math.saturate(weaponsSkill * 0.55f + combatCohesion * 0.45f);
            var skill = 0.3f + 0.4f * navigationSkill + 0.3f * weaponsCoordination;
            return math.saturate(skill);
        }

        private float ResolveNavigationSkill(Entity entity)
        {
            var navigationOfficer = ResolveSeatOccupant(entity, _roleNavigationOfficer);
            if (navigationOfficer != Entity.Null && _statsLookup.HasComponent(navigationOfficer))
            {
                var stats = _statsLookup[navigationOfficer];
                var command = math.saturate((float)stats.Command / 100f);
                var tactics = math.saturate((float)stats.Tactics / 100f);
                var engineering = math.saturate((float)stats.Engineering / 100f);
                return math.saturate(command * 0.4f + tactics * 0.4f + engineering * 0.2f);
            }

            if (_pilotLookup.HasComponent(entity))
            {
                var pilot = _pilotLookup[entity].Pilot;
                if (pilot != Entity.Null && _statsLookup.HasComponent(pilot))
                {
                    var stats = _statsLookup[pilot];
                    var command = math.saturate((float)stats.Command / 100f);
                    var tactics = math.saturate((float)stats.Tactics / 100f);
                    return math.saturate((command + tactics) * 0.5f);
                }
            }

            return 0.5f;
        }

        private float ResolveWeaponsOfficerSkill(Entity entity)
        {
            var weaponsOfficer = ResolveSeatOccupant(entity, _roleWeaponsOfficer);
            if (weaponsOfficer != Entity.Null && _statsLookup.HasComponent(weaponsOfficer))
            {
                var stats = _statsLookup[weaponsOfficer];
                var command = math.saturate((float)stats.Command / 100f);
                var tactics = math.saturate((float)stats.Tactics / 100f);
                return math.saturate(command * 0.35f + tactics * 0.65f);
            }

            return 0.5f;
        }

        private float ResolveDepartmentCohesion(Entity entity, DepartmentType department)
        {
            if (_departmentStatsLookup.HasBuffer(entity))
            {
                var buffer = _departmentStatsLookup[entity];
                for (int i = 0; i < buffer.Length; i++)
                {
                    var stats = buffer[i].Stats;
                    if (stats.Type == department)
                    {
                        return math.saturate((float)stats.Cohesion);
                    }
                }
            }

            if (_departmentStateLookup.HasComponent(entity))
            {
                return math.saturate((float)_departmentStateLookup[entity].AverageCohesion);
            }

            return 0.5f;
        }

        private Entity ResolveSeatOccupant(Entity shipEntity, FixedString64Bytes roleId)
        {
            if (!_seatRefLookup.HasBuffer(shipEntity))
            {
                return Entity.Null;
            }

            var seats = _seatRefLookup[shipEntity];
            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var seat = _seatLookup[seatEntity];
                if (!seat.RoleId.Equals(roleId))
                {
                    continue;
                }

                if (_seatOccupantLookup.HasComponent(seatEntity))
                {
                    return _seatOccupantLookup[seatEntity].OccupantEntity;
                }

                return Entity.Null;
            }

            return Entity.Null;
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
