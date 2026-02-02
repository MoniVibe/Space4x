using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Steering;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;
    using PDCarrierModuleSlot = PureDOTS.Runtime.Ships.CarrierModuleSlot;
    using PDShipModule = PureDOTS.Runtime.Ships.ShipModule;
    using PDModuleHealth = PureDOTS.Runtime.Ships.ModuleHealth;
    using PDModuleClass = PureDOTS.Runtime.Ships.ModuleClass;

    
    /// <summary>
    /// Manages weapon cooldowns and firing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XWeaponSystem : ISystem
    {
        private ComponentLookup<CapabilityState> _capabilityStateLookup;
        private ComponentLookup<CapabilityEffectiveness> _effectivenessLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<StrikeCraftState> _strikeCraftStateLookup;
        private ComponentLookup<StrikeCraftDogfightMetrics> _dogfightMetricsLookup;
        private ComponentLookup<StrikeCraftDogfightTag> _dogfightTagLookup;
        private ComponentLookup<VesselAimDirective> _aimLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<CarrierTag> _carrierTagLookup;
        private ComponentLookup<Space4XFocusModifiers> _focusLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<Space4XNormalizedIndividualStats> _normalizedStatsLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponMount>();
            state.RequireForUpdate<TimeState>();
            _capabilityStateLookup = state.GetComponentLookup<CapabilityState>(true);
            _effectivenessLookup = state.GetComponentLookup<CapabilityEffectiveness>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _strikeCraftStateLookup = state.GetComponentLookup<StrikeCraftState>(false);
            _dogfightMetricsLookup = state.GetComponentLookup<StrikeCraftDogfightMetrics>(false);
            _dogfightTagLookup = state.GetComponentLookup<StrikeCraftDogfightTag>(true);
            _aimLookup = state.GetComponentLookup<VesselAimDirective>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _carrierTagLookup = state.GetComponentLookup<CarrierTag>(true);
            _focusLookup = state.GetComponentLookup<Space4XFocusModifiers>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _normalizedStatsLookup = state.GetComponentLookup<Space4XNormalizedIndividualStats>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(true);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            uint currentTick = timeState.Tick;

            _capabilityStateLookup.Update(ref state);
            _effectivenessLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _strikeCraftStateLookup.Update(ref state);
            _dogfightMetricsLookup.Update(ref state);
            _dogfightTagLookup.Update(ref state);
            _aimLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _carrierTagLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _normalizedStatsLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _physiqueLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);

            var dogfightConfig = StrikeCraftDogfightConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftDogfightConfig>(out var dogfightConfigSingleton))
            {
                dogfightConfig = dogfightConfigSingleton;
            }

            var dogfightFireConeCos = math.cos(math.radians(dogfightConfig.FireConeDegrees));
            const float defaultFireConeCos = 0.7f;
            var projectileSpeedMultiplier = 1f;
            if (SystemAPI.TryGetSingleton<Space4XWeaponTuningConfig>(out var weaponTuning))
            {
                projectileSpeedMultiplier = math.max(0f, weaponTuning.ProjectileSpeedMultiplier);
            }

            foreach (var (weapons, engagement, transform, supply, entity) in
                SystemAPI.Query<DynamicBuffer<WeaponMount>, RefRO<Space4XEngagement>, RefRO<LocalTransform>, RefRW<SupplyStatus>>()
                    .WithEntityAccess())
            {
                // Check Firing capability - if disabled, skip firing
                bool canFire = true;
                if (_capabilityStateLookup.HasComponent(entity))
                {
                    var capabilityState = _capabilityStateLookup[entity];
                    if ((capabilityState.EnabledCapabilities & CapabilityFlags.Firing) == 0)
                    {
                        canFire = false;
                    }
                }

                // Skip if not engaged or cannot fire
                if (engagement.ValueRO.Phase != EngagementPhase.Engaged || !canFire)
                {
                    continue;
                }

                // Get firing effectiveness multiplier (damaged weapons reduce effectiveness)
                float effectivenessMultiplier = 1f;
                if (_effectivenessLookup.HasComponent(entity))
                {
                    var effectiveness = _effectivenessLookup[entity];
                    effectivenessMultiplier = math.max(0f, effectiveness.FiringEffectiveness);
                }

                var focusAccuracyBonus = 0f;
                var focusRofMultiplier = 1f;
                if (TryResolveFocusModifiers(entity, out var focusModifiers))
                {
                    focusAccuracyBonus = (float)focusModifiers.AccuracyBonus;
                    focusRofMultiplier = math.max(0.1f, (float)focusModifiers.RateOfFireMultiplier);
                }

                Entity target = engagement.ValueRO.PrimaryTarget;
                if (target == Entity.Null || !SystemAPI.Exists(target))
                {
                    continue;
                }

                // Get target position
                if (!SystemAPI.HasComponent<LocalTransform>(target))
                {
                    continue;
                }
                var targetTransform = SystemAPI.GetComponent<LocalTransform>(target);
                float3 toTarget = targetTransform.Position - transform.ValueRO.Position;
                float distance = math.length(toTarget);

                var hasSubsystems = _subsystemLookup.HasBuffer(entity);
                var hasSubsystemDisabled = _subsystemDisabledLookup.HasBuffer(entity);
                DynamicBuffer<SubsystemHealth> subsystems = default;
                DynamicBuffer<SubsystemDisabled> disabledSubsystems = default;
                var weaponsDisabled = false;
                if (hasSubsystems)
                {
                    subsystems = _subsystemLookup[entity];
                    if (hasSubsystemDisabled)
                    {
                        disabledSubsystems = _subsystemDisabledLookup[entity];
                        weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, disabledSubsystems, SubsystemType.Weapons);
                    }
                    else
                    {
                        weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, SubsystemType.Weapons);
                    }
                }

                var forward = math.forward(transform.ValueRO.Rotation);
                if (_aimLookup.HasComponent(entity))
                {
                    var aim = _aimLookup[entity];
                    if (aim.AimWeight > 0f && math.lengthsq(aim.AimDirection) > 0.001f)
                    {
                        forward = math.normalizesafe(aim.AimDirection, forward);
                    }
                }
                else if (_movementLookup.HasComponent(entity))
                {
                    var movement = _movementLookup[entity];
                    if (math.lengthsq(movement.Velocity) > 0.001f)
                    {
                        forward = math.normalizesafe(movement.Velocity, forward);
                    }
                }

                var directionToTarget = distance > 1e-4f ? toTarget / distance : forward;
                var fireConeCos = _dogfightTagLookup.HasComponent(entity) ? dogfightFireConeCos : defaultFireConeCos;
                var broadsidePreferred = IsBroadsidePreferred(entity);

                var targetVelocity = float3.zero;
                if (_movementLookup.HasComponent(target))
                {
                    targetVelocity = _movementLookup[target].Velocity;
                }

                var weaponBuffer = weapons;

                // Process each weapon mount
                for (int i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];

                    if (mount.IsEnabled == 0)
                    {
                        continue;
                    }
                    if (weaponsDisabled)
                    {
                        if (hasSubsystemDisabled)
                        {
                            if (Space4XSubsystemUtility.IsWeaponMountDisabled(entity, i, subsystems, disabledSubsystems))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            if (Space4XSubsystemUtility.ShouldDisableMount(entity, i))
                            {
                                continue;
                            }
                        }
                    }

                    // Cooldown tick
                    if (mount.Weapon.CurrentCooldown > 0)
                    {
                        mount.Weapon.CurrentCooldown--;
                        weaponBuffer[i] = mount;
                        continue;
                    }

                    // Range check
                    if (distance > mount.Weapon.MaxRange)
                    {
                        continue;
                    }

                    var aimPoint = targetTransform.Position;
                    var projectileSpeed = ResolveProjectileSpeed(mount.Weapon, projectileSpeedMultiplier);
                    if (projectileSpeed > 0f && SteeringPrimitives.LeadInterceptPoint(
                            targetTransform.Position, targetVelocity, transform.ValueRO.Position, projectileSpeed,
                            out var interceptPoint, out _))
                    {
                        aimPoint = interceptPoint;
                    }

                    var aimDirection = math.normalizesafe(aimPoint - transform.ValueRO.Position, directionToTarget);

                    var fireArcDegrees = ResolveWeaponFireArcDegrees(mount.Weapon);
                    var coneForward = forward;
                    if (fireArcDegrees > 0f && fireArcDegrees < 360f)
                    {
                        var arcOffset = (float)mount.FireArcCenterOffsetDeg;
                        if (broadsidePreferred && math.abs(arcOffset) < 0.01f)
                        {
                            arcOffset = (i & 1) == 0 ? 90f : -90f;
                        }

                        if (!IsWithinArc(forward, aimDirection, fireArcDegrees, arcOffset))
                        {
                            continue;
                        }
                        coneForward = RotateYaw(forward, arcOffset);
                    }
                    if (math.dot(coneForward, aimDirection) < fireConeCos)
                    {
                        continue;
                    }

                    // Ammo check
                    if (mount.Weapon.AmmoPerShot > 0 && supply.ValueRO.Ammunition < mount.Weapon.AmmoPerShot)
                    {
                        continue;
                    }

                    // Apply effectiveness to cooldown (damaged weapons fire slower)
                    int adjustedCooldown = (int)(mount.Weapon.CooldownTicks / math.max(0.1f, effectivenessMultiplier));
                    adjustedCooldown = (int)(adjustedCooldown / focusRofMultiplier);
                    adjustedCooldown = math.clamp(adjustedCooldown, 0, ushort.MaxValue);

                    // Fire weapon
                    mount.Weapon.CurrentCooldown = (ushort)adjustedCooldown;
                    mount.CurrentTarget = target;
                    weaponBuffer[i] = mount;

                    if (_dogfightTagLookup.HasComponent(entity))
                    {
                        if (_dogfightMetricsLookup.HasComponent(entity))
                        {
                            var metrics = _dogfightMetricsLookup.GetRefRW(entity).ValueRW;
                            if (metrics.FirstFireTick == 0)
                            {
                                metrics.FirstFireTick = currentTick;
                            }

                            metrics.LastFireTick = currentTick;
                            _dogfightMetricsLookup.GetRefRW(entity).ValueRW = metrics;
                        }

                        if (_strikeCraftStateLookup.HasComponent(entity))
                        {
                            var craftState = _strikeCraftStateLookup.GetRefRW(entity);
                            craftState.ValueRW.DogfightLastFireTick = currentTick;
                        }
                    }

                    // Consume ammo
                    if (mount.Weapon.AmmoPerShot > 0)
                    {
                        supply.ValueRW.Ammunition -= mount.Weapon.AmmoPerShot;
                    }
                }
            }
        }

        private bool TryResolveFocusModifiers(Entity shipEntity, out Space4XFocusModifiers modifiers)
        {
            if (_focusLookup.HasComponent(shipEntity))
            {
                modifiers = _focusLookup[shipEntity];
                return true;
            }

            var pilot = ResolvePilot(shipEntity);
            if (pilot != Entity.Null && _focusLookup.HasComponent(pilot))
            {
                modifiers = _focusLookup[pilot];
                return true;
            }

            modifiers = default;
            return false;
        }

        private Entity ResolvePilot(Entity shipEntity)
        {
            if (_pilotLookup.HasComponent(shipEntity))
            {
                var pilot = _pilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            if (_strikePilotLookup.HasComponent(shipEntity))
            {
                var pilot = _strikePilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            return Entity.Null;
        }

        private float ResolveGunnerySkill(Entity shipEntity)
        {
            var pilot = ResolvePilot(shipEntity);
            var profile = pilot != Entity.Null ? pilot : shipEntity;

            if (_normalizedStatsLookup.HasComponent(profile))
            {
                var stats = _normalizedStatsLookup[profile];
                var skill = stats.Tactics * 0.45f + stats.Finesse * 0.35f + stats.Command * 0.2f;
                return math.saturate(skill);
            }

            float command = 0.5f;
            float tactics = 0.5f;
            float finesse = 0.5f;

            if (_statsLookup.HasComponent(profile))
            {
                var stats = _statsLookup[profile];
                command = math.saturate((float)stats.Command / 100f);
                tactics = math.saturate((float)stats.Tactics / 100f);
            }

            if (_physiqueLookup.HasComponent(profile))
            {
                var physique = _physiqueLookup[profile];
                finesse = math.saturate((float)physique.Finesse / 100f);
            }

            var fallbackSkill = tactics * 0.45f + finesse * 0.35f + command * 0.2f;
            return math.saturate(fallbackSkill);
        }

        private static float ResolveTrackingPenalty(
            in Space4XWeapon weapon,
            float distance,
            float3 directionToTarget,
            float3 relativeVelocity,
            float gunnerySkill)
        {
            if (distance <= 0.01f)
            {
                return 1f;
            }

            var omega = math.length(math.cross(relativeVelocity, directionToTarget)) / math.max(distance, 0.1f);
            var basePenalty = weapon.Type switch
            {
                WeaponType.PointDefense => 0.05f,
                WeaponType.Flak => 0.07f,
                WeaponType.Laser => 0.08f,
                WeaponType.Plasma => 0.09f,
                WeaponType.Ion => 0.09f,
                WeaponType.Kinetic => 0.1f,
                WeaponType.Missile => 0.12f,
                WeaponType.Torpedo => 0.15f,
                _ => 0.1f
            };

            var skillFactor = math.lerp(basePenalty * 1.4f, basePenalty * 0.6f, math.saturate(gunnerySkill));
            return math.saturate(1f - omega * skillFactor);
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
    }

    /// <summary>
    /// Resolves weapon hits and calculates damage.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XWeaponSystem))]
    [UpdateAfter(typeof(PureDOTS.Systems.Combat.FormationCombatSystem))]
    [UpdateAfter(typeof(PureDOTS.Systems.Combat.CohesionEffectSystem))]
    public partial struct Space4XDamageResolutionSystem : ISystem
    {
        private ComponentLookup<FormationBonus> _formationBonusLookup;
        private ComponentLookup<FormationIntegrity> _formationIntegrityLookup;
        private ComponentLookup<FormationCombatConfig> _formationConfigLookup;
        private ComponentLookup<CohesionCombatMultipliers> _cohesionMultipliersLookup;
        private ComponentLookup<FormationAssignment> _formationAssignmentLookup;
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<Advantage3D> _advantageLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<Space4XNormalizedIndividualStats> _normalizedStatsLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private BufferLookup<DamageScarEvent> _scarLookup;
        private ComponentLookup<SubsystemTargetDirective> _subsystemDirectiveLookup;
        private ComponentLookup<Space4XFocusModifiers> _focusLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private const float SubsystemDamageFraction = 0.25f;
        private const float AntiSubsystemDamageMultiplier = 1.5f;
        private const float CriticalSubsystemDamageMultiplier = 1.25f;
        private const uint DefaultSubsystemDisableTicks = 120;
        private const float ScarPositionQuantize = 100f;
        private const float ScarNormalQuantize = 100f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponMount>();

            _formationBonusLookup = state.GetComponentLookup<FormationBonus>(true);
            _formationIntegrityLookup = state.GetComponentLookup<FormationIntegrity>(true);
            _formationConfigLookup = state.GetComponentLookup<FormationCombatConfig>(true);
            _cohesionMultipliersLookup = state.GetComponentLookup<CohesionCombatMultipliers>(true);
            _formationAssignmentLookup = state.GetComponentLookup<FormationAssignment>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
            _advantageLookup = state.GetComponentLookup<Advantage3D>(false);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _normalizedStatsLookup = state.GetComponentLookup<Space4XNormalizedIndividualStats>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(false);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(false);
            _scarLookup = state.GetBufferLookup<DamageScarEvent>(false);
            _subsystemDirectiveLookup = state.GetComponentLookup<SubsystemTargetDirective>(true);
            _focusLookup = state.GetComponentLookup<Space4XFocusModifiers>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint currentTick;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                if (timeState.IsPaused)
                {
                    return;
                }

                currentTick = timeState.Tick;
            }
            else
            {
                currentTick = (uint)SystemAPI.Time.ElapsedTime;
            }

            _formationBonusLookup.Update(ref state);
            _formationIntegrityLookup.Update(ref state);
            _formationConfigLookup.Update(ref state);
            _cohesionMultipliersLookup.Update(ref state);
            _formationAssignmentLookup.Update(ref state);
            _entityLookup.Update(ref state);
            _advantageLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _normalizedStatsLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _physiqueLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _scarLookup.Update(ref state);
            _subsystemDirectiveLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);

            foreach (var (weapons, engagement, transform, entity) in
                SystemAPI.Query<DynamicBuffer<WeaponMount>, RefRW<Space4XEngagement>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                if (engagement.ValueRO.Phase != EngagementPhase.Engaged)
                {
                    continue;
                }

                Entity target = engagement.ValueRO.PrimaryTarget;
                if (target == Entity.Null || !SystemAPI.Exists(target))
                {
                    continue;
                }

                // Get target components
                if (!SystemAPI.HasComponent<LocalTransform>(target))
                {
                    continue;
                }

                var targetTransform = SystemAPI.GetComponent<LocalTransform>(target);
                float distance = math.distance(transform.ValueRO.Position, targetTransform.Position);

                // Get target evasion
                float evasion = 0f;
                if (SystemAPI.HasComponent<Space4XEngagement>(target))
                {
                    evasion = (float)SystemAPI.GetComponent<Space4XEngagement>(target).EvasionModifier;
                }

                var focusAccuracyBonus = 0f;
                if (TryResolveFocusModifiers(entity, out var focusModifiers))
                {
                    focusAccuracyBonus = (float)focusModifiers.AccuracyBonus;
                }

                var gunnerySkill = ResolveGunnerySkill(entity);
                var toTarget = targetTransform.Position - transform.ValueRO.Position;
                var directionToTarget = math.lengthsq(toTarget) > 1e-6f ? math.normalizesafe(toTarget) : math.forward(transform.ValueRO.Rotation);
                var attackerVelocity = _movementLookup.HasComponent(entity) ? _movementLookup[entity].Velocity : float3.zero;
                var targetVelocity = _movementLookup.HasComponent(target) ? _movementLookup[target].Velocity : float3.zero;
                var relativeVelocity = targetVelocity - attackerVelocity;

                // Process weapons that just fired (cooldown == max)
                for (int i = 0; i < weapons.Length; i++)
                {
                    var mount = weapons[i];

                    if (mount.CurrentTarget != target || mount.Weapon.CurrentCooldown != mount.Weapon.CooldownTicks)
                    {
                        continue;
                    }

                    // Calculate hit
                    float hitChance = CombatMath.CalculateHitChance(
                        (float)mount.Weapon.BaseAccuracy,
                        evasion,
                        distance,
                        mount.Weapon.OptimalRange,
                        mount.Weapon.MaxRange
                    );

                    uint hitSeed = (uint)(entity.Index * 12345) + currentTick + (uint)i;
                    var random = new Unity.Mathematics.Random(hitSeed);

                    // Apply cohesion accuracy multiplier to hit chance
                    if (_cohesionMultipliersLookup.HasComponent(entity))
                    {
                        var cohesion = _cohesionMultipliersLookup[entity];
                        hitChance *= cohesion.AccuracyMultiplier;
                        hitChance = math.clamp(hitChance, 0f, 1f);
                    }

                    if (math.abs(focusAccuracyBonus) > 0.0001f)
                    {
                        hitChance = math.clamp(hitChance + focusAccuracyBonus, 0f, 1f);
                    }

                    var gunneryMultiplier = math.lerp(0.75f, 1.15f, gunnerySkill);
                    hitChance = math.clamp(hitChance * gunneryMultiplier, 0f, 1f);

                    var trackingPenalty = ResolveTrackingPenalty(mount.Weapon, distance, directionToTarget, relativeVelocity, gunnerySkill);
                    hitChance = math.clamp(hitChance * trackingPenalty, 0f, 1f);

                    if (random.NextFloat() > hitChance)
                    {
                        continue; // Miss
                    }

                    // Hit - calculate damage
                    float rawDamage = mount.Weapon.BaseDamage;

                    // Critical hit check
                    bool isCritical = CombatMath.RollCritical(0.05f, hitSeed + 1);
                    if (isCritical)
                    {
                        rawDamage *= 1.5f;
                    }

                    // Formation bonus (from engagement, already calculated)
                    rawDamage *= (1f + (float)engagement.ValueRO.FormationBonus);

                    // Apply cohesion multipliers
                    if (_cohesionMultipliersLookup.HasComponent(entity))
                    {
                        var cohesion = _cohesionMultipliersLookup[entity];
                        rawDamage *= cohesion.DamageMultiplier;
                    }

                    // Apply 3D advantage multiplier (high ground, flanking bonuses)
                    if (_advantageLookup.HasComponent(entity) && SystemAPI.HasComponent<LocalTransform>(entity) && SystemAPI.HasComponent<LocalTransform>(target))
                    {
                        var attackerPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                        var targetPos = SystemAPI.GetComponent<LocalTransform>(target).Position;
                        var advantageMultiplier = Formation3DService.Get3DAdvantageMultiplier(
                            _entityLookup,
                            _advantageLookup,
                            entity,
                            attackerPos,
                            target,
                            targetPos);
                        rawDamage *= advantageMultiplier;
                    }

                    // Apply damage to target
                    ApplyDamageToTarget(target, entity, mount.Weapon, rawDamage, isCritical, currentTick, transform.ValueRO, targetTransform, ref state);

                    engagement.ValueRW.DamageDealt += rawDamage;
                }
            }
        }

        private void ApplyDamageToTarget(
            Entity target,
            Entity source,
            Space4XWeapon weapon,
            float rawDamage,
            bool isCritical,
            uint tick,
            in LocalTransform sourceTransform,
            in LocalTransform targetTransform,
            ref SystemState state)
        {
            float remainingDamage = rawDamage;
            float shieldDamage = 0f;
            float armorDamage = 0f;
            float hullDamage = 0f;

            // Shield absorption
            if (SystemAPI.HasComponent<Space4XShield>(target))
            {
                var shield = SystemAPI.GetComponent<Space4XShield>(target);

                if (shield.Current > 0)
                {
                    float resistance = CombatMath.GetWeaponResistance(weapon.Type, shield);
                    float effectiveDamage = CombatMath.CalculateShieldDamage(remainingDamage, (float)weapon.ShieldModifier, resistance);

                    shieldDamage = math.min(shield.Current, effectiveDamage);
                    shield.Current -= shieldDamage;
                    shield.CurrentDelay = shield.RechargeDelay;

                    remainingDamage = math.max(0, effectiveDamage - shieldDamage);

                    SystemAPI.SetComponent(target, shield);
                }
            }

            // Armor mitigation
            if (remainingDamage > 0 && SystemAPI.HasComponent<Space4XArmor>(target))
            {
                var armor = SystemAPI.GetComponent<Space4XArmor>(target);
                float resistance = CombatMath.GetArmorResistance(weapon.Type, armor);

                float mitigatedDamage = CombatMath.CalculateArmorDamage(
                    remainingDamage,
                    armor.Thickness,
                    (float)weapon.ArmorPenetration,
                    resistance
                );

                armorDamage = remainingDamage - mitigatedDamage;
                remainingDamage = mitigatedDamage;
            }

            // Hull damage
            if (remainingDamage > 0 && SystemAPI.HasComponent<HullIntegrity>(target))
            {
                var hull = SystemAPI.GetComponent<HullIntegrity>(target);
                hullDamage = remainingDamage;
                hull.Current = (half)math.max(0f, (float)hull.Current - hullDamage);
                SystemAPI.SetComponent(target, hull);
            }

            if (hullDamage > 0f)
            {
                ApplySubsystemDamage(target, source, weapon, hullDamage, isCritical, tick);
                TryEmitScarEvent(target, sourceTransform, targetTransform, hullDamage, weapon.Type, isCritical, tick);
            }

            // Log damage event
            if (SystemAPI.HasBuffer<DamageEvent>(target))
            {
                var events = SystemAPI.GetBuffer<DamageEvent>(target);
                if (events.Length < events.Capacity)
                {
                    events.Add(new DamageEvent
                    {
                        Source = source,
                        WeaponType = weapon.Type,
                        RawDamage = rawDamage,
                        ShieldDamage = shieldDamage,
                        ArmorDamage = armorDamage,
                        HullDamage = hullDamage,
                        Tick = tick,
                        IsCritical = (byte)(isCritical ? 1 : 0)
                    });
                }
            }

            // Update target engagement
            if (SystemAPI.HasComponent<Space4XEngagement>(target))
            {
                var targetEngagement = SystemAPI.GetComponent<Space4XEngagement>(target);
                targetEngagement.DamageReceived += rawDamage;
                SystemAPI.SetComponent(target, targetEngagement);
            }
        }

        private bool TryResolveFocusModifiers(Entity shipEntity, out Space4XFocusModifiers modifiers)
        {
            if (_focusLookup.HasComponent(shipEntity))
            {
                modifiers = _focusLookup[shipEntity];
                return true;
            }

            var pilot = ResolvePilot(shipEntity);
            if (pilot != Entity.Null && _focusLookup.HasComponent(pilot))
            {
                modifiers = _focusLookup[pilot];
                return true;
            }

            modifiers = default;
            return false;
        }

        private Entity ResolvePilot(Entity shipEntity)
        {
            if (_pilotLookup.HasComponent(shipEntity))
            {
                var pilot = _pilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            if (_strikePilotLookup.HasComponent(shipEntity))
            {
                var pilot = _strikePilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            return Entity.Null;
        }

        private float ResolveGunnerySkill(Entity shipEntity)
        {
            var pilot = ResolvePilot(shipEntity);
            var profile = pilot != Entity.Null ? pilot : shipEntity;

            if (_normalizedStatsLookup.HasComponent(profile))
            {
                var stats = _normalizedStatsLookup[profile];
                var skill = stats.Tactics * 0.45f + stats.Finesse * 0.35f + stats.Command * 0.2f;
                return math.saturate(skill);
            }

            float command = 0.5f;
            float tactics = 0.5f;
            float finesse = 0.5f;

            if (_statsLookup.HasComponent(profile))
            {
                var stats = _statsLookup[profile];
                command = math.saturate((float)stats.Command / 100f);
                tactics = math.saturate((float)stats.Tactics / 100f);
            }

            if (_physiqueLookup.HasComponent(profile))
            {
                var physique = _physiqueLookup[profile];
                finesse = math.saturate((float)physique.Finesse / 100f);
            }

            var fallbackSkill = tactics * 0.45f + finesse * 0.35f + command * 0.2f;
            return math.saturate(fallbackSkill);
        }

        private static float ResolveTrackingPenalty(
            in Space4XWeapon weapon,
            float distance,
            float3 directionToTarget,
            float3 relativeVelocity,
            float gunnerySkill)
        {
            if (distance <= 0.01f)
            {
                return 1f;
            }

            var omega = math.length(math.cross(relativeVelocity, directionToTarget)) / math.max(distance, 0.1f);
            var basePenalty = weapon.Type switch
            {
                WeaponType.PointDefense => 0.05f,
                WeaponType.Flak => 0.07f,
                WeaponType.Laser => 0.08f,
                WeaponType.Plasma => 0.09f,
                WeaponType.Ion => 0.09f,
                WeaponType.Kinetic => 0.1f,
                WeaponType.Missile => 0.12f,
                WeaponType.Torpedo => 0.15f,
                _ => 0.1f
            };

            var skillFactor = math.lerp(basePenalty * 1.4f, basePenalty * 0.6f, math.saturate(gunnerySkill));
            return math.saturate(1f - omega * skillFactor);
        }

        private void ApplySubsystemDamage(Entity target, Entity source, in Space4XWeapon weapon, float hullDamage, bool isCritical, uint tick)
        {
            if (!_subsystemLookup.HasBuffer(target) || hullDamage <= 0f)
            {
                return;
            }

            var subsystems = _subsystemLookup[target];
            if (subsystems.Length == 0)
            {
                return;
            }

            var subsystemIndex = -1;
            if (_subsystemDirectiveLookup.HasComponent(source))
            {
                var directive = _subsystemDirectiveLookup[source];
                subsystemIndex = FindSubsystemIndex(subsystems, directive.TargetSubsystem);
            }

            if (subsystemIndex < 0)
            {
                subsystemIndex = SelectSubsystemIndex(subsystems, source, target, tick);
            }

            if (subsystemIndex < 0)
            {
                return;
            }

            var health = subsystems[subsystemIndex];
            if ((health.Flags & SubsystemFlags.Destroyed) != 0)
            {
                return;
            }

            var damage = hullDamage * SubsystemDamageFraction;
            if ((weapon.Flags & WeaponFlags.AntiSubsystem) != 0)
            {
                damage *= AntiSubsystemDamageMultiplier;
            }

            if (isCritical)
            {
                damage *= CriticalSubsystemDamageMultiplier;
            }

            health.Current = math.max(0f, health.Current - damage);
            if (health.Current <= 0f)
            {
                health.Current = 0f;
                var disableUntil = tick + DefaultSubsystemDisableTicks;
                if ((health.Flags & SubsystemFlags.Inherent) == 0 &&
                    ((weapon.Flags & WeaponFlags.AntiSubsystem) != 0 || isCritical))
                {
                    health.Flags |= SubsystemFlags.Destroyed;
                    disableUntil = uint.MaxValue;
                }

                SetSubsystemDisabled(target, health.Type, disableUntil);
            }

            subsystems[subsystemIndex] = health;
        }

        private int FindSubsystemIndex(DynamicBuffer<SubsystemHealth> subsystems, SubsystemType targetType)
        {
            for (int i = 0; i < subsystems.Length; i++)
            {
                var subsystem = subsystems[i];
                if (subsystem.Type == targetType && (subsystem.Flags & SubsystemFlags.Destroyed) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int SelectSubsystemIndex(DynamicBuffer<SubsystemHealth> subsystems, Entity source, Entity target, uint tick)
        {
            var eligibleCount = 0;
            for (int i = 0; i < subsystems.Length; i++)
            {
                if ((subsystems[i].Flags & SubsystemFlags.Destroyed) == 0)
                {
                    eligibleCount++;
                }
            }

            if (eligibleCount == 0)
            {
                return -1;
            }

            var hash = (uint)math.hash(new uint3((uint)source.Index, (uint)target.Index, tick));
            var choice = (int)(hash % (uint)eligibleCount);

            for (int i = 0; i < subsystems.Length; i++)
            {
                if ((subsystems[i].Flags & SubsystemFlags.Destroyed) != 0)
                {
                    continue;
                }

                if (choice == 0)
                {
                    return i;
                }

                choice--;
            }

            return -1;
        }

        private void SetSubsystemDisabled(Entity target, SubsystemType type, uint untilTick)
        {
            if (!_subsystemDisabledLookup.HasBuffer(target))
            {
                return;
            }

            var buffer = _subsystemDisabledLookup[target];
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == type)
                {
                    var entry = buffer[i];
                    if (entry.UntilTick == uint.MaxValue || untilTick == uint.MaxValue)
                    {
                        entry.UntilTick = uint.MaxValue;
                    }
                    else
                    {
                        entry.UntilTick = math.max(entry.UntilTick, untilTick);
                    }

                    buffer[i] = entry;
                    return;
                }
            }

            buffer.Add(new SubsystemDisabled
            {
                Type = type,
                UntilTick = untilTick
            });
        }

        private void TryEmitScarEvent(Entity target, in LocalTransform sourceTransform, in LocalTransform targetTransform, float hullDamage,
            WeaponType weaponType, bool isCritical, uint tick)
        {
            if (!_scarLookup.HasBuffer(target))
            {
                return;
            }

            var buffer = _scarLookup[target];
            if (buffer.Length >= buffer.Capacity)
            {
                return;
            }

            var toSource = math.normalizesafe(sourceTransform.Position - targetTransform.Position, math.forward(targetTransform.Rotation));
            var inverseRotation = math.conjugate(targetTransform.Rotation);
            var localNormal = math.normalizesafe(math.mul(inverseRotation, toSource), new float3(0f, 0f, 1f));
            var radius = math.max(0.5f, targetTransform.Scale * 0.5f);
            var localPos = localNormal * radius;
            var posQ = Quantize(localPos, ScarPositionQuantize);
            var normalQ = Quantize(localNormal, ScarNormalQuantize);
            var intensity = (byte)math.clamp((int)math.round(math.saturate(hullDamage / 50f) * 255f), 1, 255);
            var scarType = (byte)weaponType;
            if (isCritical && scarType < 254)
            {
                scarType += 1;
            }

            buffer.Add(new DamageScarEvent
            {
                LocalPositionQ = posQ,
                NormalQ = normalQ,
                Intensity = intensity,
                ScarType = scarType,
                Tick = tick
            });
        }

        private static int3 Quantize(float3 value, float scale)
        {
            return (int3)math.round(value * scale);
        }
    }

    /// <summary>
    /// Regenerates subsystem health and clears expired disabled timers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XDamageResolutionSystem))]
    public partial struct Space4XSubsystemStatusSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SubsystemHealth>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint currentTick;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                if (timeState.IsPaused)
                {
                    return;
                }

                currentTick = timeState.Tick;
            }
            else
            {
                currentTick = (uint)SystemAPI.Time.ElapsedTime;
            }

            foreach (var (subsystems, entity) in SystemAPI.Query<DynamicBuffer<SubsystemHealth>>().WithEntityAccess())
            {
                var subsystemsBuffer = subsystems;
                var hasDisabled = SystemAPI.HasBuffer<SubsystemDisabled>(entity);
                DynamicBuffer<SubsystemDisabled> disabled = default;
                if (hasDisabled)
                {
                    disabled = SystemAPI.GetBuffer<SubsystemDisabled>(entity);
                }

                for (int i = 0; i < subsystems.Length; i++)
                {
                    var health = subsystemsBuffer[i];
                    if ((health.Flags & SubsystemFlags.Destroyed) != 0)
                    {
                        continue;
                    }

                    if (health.Current < health.Max)
                    {
                        health.Current = math.min(health.Max, health.Current + health.RegenPerTick);
                        subsystemsBuffer[i] = health;
                    }
                }

                if (!hasDisabled || disabled.Length == 0)
                {
                    continue;
                }

                for (int i = disabled.Length - 1; i >= 0; i--)
                {
                    var entry = disabled[i];
                    if (entry.UntilTick == uint.MaxValue)
                    {
                        continue;
                    }

                    if (currentTick < entry.UntilTick)
                    {
                        continue;
                    }

                    if (Space4XSubsystemUtility.TryGetSubsystem(subsystemsBuffer, entry.Type, out var health, out _) &&
                        (health.Flags & SubsystemFlags.Destroyed) == 0 &&
                        health.Current > 0f)
                    {
                        disabled.RemoveAt(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Manages shield regeneration.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XShieldRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XShield>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var shield in SystemAPI.Query<RefRW<Space4XShield>>())
            {
                // Delay countdown
                if (shield.ValueRO.CurrentDelay > 0)
                {
                    shield.ValueRW.CurrentDelay--;
                    continue;
                }

                // Regenerate
                if (shield.ValueRO.Current < shield.ValueRO.Maximum)
                {
                    shield.ValueRW.Current = math.min(
                        shield.ValueRO.Maximum,
                        shield.ValueRO.Current + shield.ValueRO.RechargeRate
                    );
                }
            }
        }
    }

    /// <summary>
    /// Manages engagement state transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XDamageResolutionSystem))]
    public partial struct Space4XEngagementStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEngagement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (engagement, hull, transform, entity) in
                SystemAPI.Query<RefRW<Space4XEngagement>, RefRO<HullIntegrity>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Check for destruction
                if ((float)hull.ValueRO.Current <= 0)
                {
                    engagement.ValueRW.Phase = EngagementPhase.Destroyed;
                    if (SystemAPI.HasComponent<InCombatTag>(entity))
                    {
                        ecb.RemoveComponent<InCombatTag>(entity);
                    }
                    continue;
                }

                // Check for disabled (very low hull)
                if ((float)hull.ValueRO.Current < (float)hull.ValueRO.Max * 0.1f)
                {
                    engagement.ValueRW.Phase = EngagementPhase.Disabled;
                    continue;
                }

                // Update target distance
                if (engagement.ValueRO.PrimaryTarget != Entity.Null &&
                    SystemAPI.HasComponent<LocalTransform>(engagement.ValueRO.PrimaryTarget))
                {
                    var targetTransform = SystemAPI.GetComponent<LocalTransform>(engagement.ValueRO.PrimaryTarget);
                    engagement.ValueRW.TargetDistance = math.distance(transform.ValueRO.Position, targetTransform.Position);
                }

                // Check if target destroyed
                if (engagement.ValueRO.PrimaryTarget != Entity.Null)
                {
                    if (!SystemAPI.Exists(engagement.ValueRO.PrimaryTarget))
                    {
                        engagement.ValueRW.Phase = EngagementPhase.Victorious;
                        engagement.ValueRW.PrimaryTarget = Entity.Null;
                    }
                    else if (SystemAPI.HasComponent<HullIntegrity>(engagement.ValueRO.PrimaryTarget))
                    {
                        var targetHull = SystemAPI.GetComponent<HullIntegrity>(engagement.ValueRO.PrimaryTarget);
                        if ((float)targetHull.Current <= 0)
                        {
                            engagement.ValueRW.Phase = EngagementPhase.Victorious;
                        }
                    }
                }

                // Increment duration
                if (engagement.ValueRO.Phase == EngagementPhase.Engaged)
                {
                    engagement.ValueRW.EngagementDuration++;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Initiates combat engagements based on target priority.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XWeaponSystem))]
    [UpdateAfter(typeof(PureDOTS.Systems.Combat.FormationCombatSystem))]
    public partial struct Space4XCombatInitiationSystem : ISystem
    {
        private ComponentLookup<FormationBonus> _formationBonusLookup;
        private ComponentLookup<FormationIntegrity> _formationIntegrityLookup;
        private ComponentLookup<FormationCombatConfig> _formationConfigLookup;
        private ComponentLookup<FormationAssignment> _formationAssignmentLookup;
        private EntityStorageInfoLookup _entityLookup;
        private BufferLookup<PDCarrierModuleSlot> _slotLookup;
        private ComponentLookup<PDShipModule> _moduleLookup;
        private ComponentLookup<ModuleTargetPriority> _priorityLookup;
        private ComponentLookup<PDModuleHealth> _healthLookup;
        private ComponentLookup<ModuleTargetPolicy> _modulePolicyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetPriority>();
            state.RequireForUpdate<Space4XEngagement>();

            _formationBonusLookup = state.GetComponentLookup<FormationBonus>(true);
            _formationIntegrityLookup = state.GetComponentLookup<FormationIntegrity>(true);
            _formationConfigLookup = state.GetComponentLookup<FormationCombatConfig>(true);
            _formationAssignmentLookup = state.GetComponentLookup<FormationAssignment>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
            _slotLookup = state.GetBufferLookup<PDCarrierModuleSlot>(true);
            _moduleLookup = state.GetComponentLookup<PDShipModule>(true);
            _priorityLookup = state.GetComponentLookup<ModuleTargetPriority>(true);
            _healthLookup = state.GetComponentLookup<PDModuleHealth>(true);
            _modulePolicyLookup = state.GetComponentLookup<ModuleTargetPolicy>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _formationBonusLookup.Update(ref state);
            _formationIntegrityLookup.Update(ref state);
            _formationConfigLookup.Update(ref state);
            _formationAssignmentLookup.Update(ref state);
            _entityLookup.Update(ref state);
            _slotLookup.Update(ref state);
            _moduleLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _modulePolicyLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (priority, engagement, transform, entity) in
                SystemAPI.Query<RefRO<TargetPriority>, RefRW<Space4XEngagement>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Skip if already engaged or no target
                if (engagement.ValueRO.Phase == EngagementPhase.Engaged ||
                    engagement.ValueRO.Phase == EngagementPhase.Destroyed ||
                    engagement.ValueRO.Phase == EngagementPhase.Disabled)
                {
                    continue;
                }

                if (priority.ValueRO.CurrentTarget == Entity.Null)
                {
                    continue;
                }

                // Check if target is in weapon range
                if (!SystemAPI.HasComponent<LocalTransform>(priority.ValueRO.CurrentTarget))
                {
                    continue;
                }

                var targetTransform = SystemAPI.GetComponent<LocalTransform>(priority.ValueRO.CurrentTarget);
                float distance = math.distance(transform.ValueRO.Position, targetTransform.Position);

                // Get max weapon range
                float maxRange = 500f; // Default
                if (SystemAPI.HasBuffer<WeaponMount>(entity))
                {
                    var weapons = SystemAPI.GetBuffer<WeaponMount>(entity);
                    for (int i = 0; i < weapons.Length; i++)
                    {
                        if (weapons[i].Weapon.MaxRange > maxRange)
                        {
                            maxRange = weapons[i].Weapon.MaxRange;
                        }
                    }
                }

                // Initiate engagement if in range
                if (distance <= maxRange * 1.2f) // Slightly beyond max range to start approach
                {
                    var targetShip = priority.ValueRO.CurrentTarget;
                    engagement.ValueRW.PrimaryTarget = targetShip;
                    engagement.ValueRW.Phase = distance <= maxRange ? EngagementPhase.Engaged : EngagementPhase.Approaching;
                    engagement.ValueRW.TargetDistance = distance;
                    engagement.ValueRW.EngagementDuration = 0;
                    engagement.ValueRW.DamageDealt = 0;
                    engagement.ValueRW.DamageReceived = 0;

                    // Select module target if target ship has modules
                    if (_slotLookup.HasBuffer(targetShip))
                    {
                        var moduleTarget = SelectModuleTarget(entity, targetShip);

                        if (moduleTarget != Entity.Null)
                        {
                            // Add ModuleTarget component to attacker
                            if (!SystemAPI.HasComponent<ModuleTarget>(entity))
                            {
                                ecb.AddComponent(entity, new ModuleTarget
                                {
                                    TargetModule = moduleTarget,
                                    TargetShip = targetShip,
                                    TargetSelectedTick = (uint)SystemAPI.Time.ElapsedTime
                                });
                            }
                            else
                            {
                                ecb.SetComponent(entity, new ModuleTarget
                                {
                                    TargetModule = moduleTarget,
                                    TargetShip = targetShip,
                                    TargetSelectedTick = (uint)SystemAPI.Time.ElapsedTime
                                });
                            }
                        }
                    }

                    // Read PureDOTS formation bonus instead of calculating manually
                    float formationBonus = 0f;
                    Entity formationEntity = Entity.Null;

                    // Find formation entity (either this entity or its formation leader)
                    if (_formationAssignmentLookup.HasComponent(entity))
                    {
                        formationEntity = _formationAssignmentLookup[entity].FormationLeader;
                    }
                    else if (_formationBonusLookup.HasComponent(entity))
                    {
                        formationEntity = entity;
                    }

                    if (formationEntity != Entity.Null &&
                        _formationBonusLookup.HasComponent(formationEntity) &&
                        _formationIntegrityLookup.HasComponent(formationEntity) &&
                        _formationConfigLookup.HasComponent(formationEntity))
                    {
                        var bonus = _formationBonusLookup[formationEntity];
                        var integrity = _formationIntegrityLookup[formationEntity];
                        var config = _formationConfigLookup[formationEntity];
                        float attackMultiplier = FormationCombatService.GetFormationAttackBonus(bonus, integrity, config);
                        formationBonus = attackMultiplier - 1f; // Convert multiplier to bonus (e.g., 1.2 -> 0.2)
                    }

                    engagement.ValueRW.FormationBonus = (half)formationBonus;

                    VesselStanceMode stance = VesselStanceMode.Balanced;
                    if (SystemAPI.HasComponent<PatrolStance>(entity))
                    {
                        stance = SystemAPI.GetComponent<PatrolStance>(entity).Stance;
                    }
                    engagement.ValueRW.EvasionModifier = (half)(stance == VesselStanceMode.Evasive ? 0.3f : 0.1f);

                    // Add combat tag
                    if (!SystemAPI.HasComponent<InCombatTag>(entity))
                    {
                        ecb.AddComponent<InCombatTag>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Entity SelectModuleTarget(Entity attacker, Entity targetShip)
        {
            if (_modulePolicyLookup.HasComponent(attacker))
            {
                var policy = _modulePolicyLookup[attacker];
                if (policy.Kind != ModuleTargetPolicyKind.Default)
                {
                    return SelectModuleTargetWithPolicy(attacker, targetShip, policy);
                }
            }

            return ModuleTargetingService.SelectModuleTarget(
                _entityLookup,
                _slotLookup,
                _moduleLookup,
                _priorityLookup,
                _healthLookup,
                attacker,
                targetShip);
        }

        private Entity SelectModuleTargetWithPolicy(Entity attacker, Entity targetShip, ModuleTargetPolicy policy)
        {
            if (!_entityLookup.Exists(targetShip) || !_slotLookup.HasBuffer(targetShip))
            {
                return Entity.Null;
            }

            var slots = _slotLookup[targetShip];
            if (slots.Length == 0)
            {
                return Entity.Null;
            }

            Entity bestModule = Entity.Null;
            int bestScore = int.MinValue;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.InstalledModule == Entity.Null)
                {
                    continue;
                }

                var moduleEntity = slot.InstalledModule;
                if (!_entityLookup.Exists(moduleEntity))
                {
                    continue;
                }

                if (!ModuleTargetingService.IsModuleTargetable(_entityLookup, _moduleLookup, _healthLookup, moduleEntity))
                {
                    continue;
                }

                byte basePriority = 50;
                if (_priorityLookup.HasComponent(moduleEntity))
                {
                    basePriority = _priorityLookup[moduleEntity].Priority;
                }
                else if (_moduleLookup.HasComponent(moduleEntity))
                {
                    basePriority = GetDefaultPriority(_moduleLookup[moduleEntity].Class);
                }

                int score = basePriority + GetPolicyBonus(policy.Kind, moduleEntity);
                if (_healthLookup.HasComponent(moduleEntity))
                {
                    var health = _healthLookup[moduleEntity];
                    if (health.State == ModuleHealthState.Degraded)
                    {
                        score += 10;
                    }
                    else if (health.State == ModuleHealthState.Failed)
                    {
                        score += 20;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestModule = moduleEntity;
                }
            }

            return bestModule;
        }

        private int GetPolicyBonus(ModuleTargetPolicyKind kind, Entity moduleEntity)
        {
            if (!_moduleLookup.HasComponent(moduleEntity))
            {
                return 0;
            }

            var moduleClass = _moduleLookup[moduleEntity].Class;
            return kind switch
            {
                ModuleTargetPolicyKind.DisableMobility when moduleClass == PDModuleClass.Engine => 80,
                ModuleTargetPolicyKind.DisableFighting when IsWeaponClass(moduleClass) => 80,
                ModuleTargetPolicyKind.DisableFighting when moduleClass == PDModuleClass.Hangar => 70,
                ModuleTargetPolicyKind.DisableSensors when moduleClass == PDModuleClass.Sensor => 70,
                ModuleTargetPolicyKind.DisableLogistics when IsLogisticsClass(moduleClass) => 60,
                _ => 0
            };
        }

        private static bool IsWeaponClass(PDModuleClass moduleClass)
        {
            return moduleClass == PDModuleClass.BeamCannon
                   || moduleClass == PDModuleClass.MassDriver
                   || moduleClass == PDModuleClass.Missile
                   || moduleClass == PDModuleClass.PointDefense;
        }

        private static bool IsLogisticsClass(PDModuleClass moduleClass)
        {
            return moduleClass == PDModuleClass.Cargo
                   || moduleClass == PDModuleClass.Fabrication
                   || moduleClass == PDModuleClass.Agriculture
                   || moduleClass == PDModuleClass.Mining
                   || moduleClass == PDModuleClass.Terraforming;
        }

        private static byte GetDefaultPriority(PDModuleClass moduleClass)
        {
            return moduleClass switch
            {
                PDModuleClass.Engine => 200,
                PDModuleClass.BeamCannon => 150,
                PDModuleClass.MassDriver => 150,
                PDModuleClass.Missile => 150,
                PDModuleClass.PointDefense => 140,
                PDModuleClass.Shield => 120,
                PDModuleClass.Armor => 100,
                PDModuleClass.Sensor => 80,
                PDModuleClass.Cargo => 50,
                PDModuleClass.Hangar => 60,
                PDModuleClass.Fabrication => 40,
                PDModuleClass.Research => 40,
                PDModuleClass.Medical => 30,
                _ => 20
            };
        }
    }

    /// <summary>
    /// Telemetry for combat system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XCombatTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEngagement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalCombatants = 0;
            int engaged = 0;
            int approaching = 0;
            int destroyed = 0;
            float totalDamageDealt = 0f;
            float totalDamageReceived = 0f;

            foreach (var engagement in SystemAPI.Query<RefRO<Space4XEngagement>>())
            {
                if (engagement.ValueRO.Phase != EngagementPhase.None)
                {
                    totalCombatants++;
                    totalDamageDealt += engagement.ValueRO.DamageDealt;
                    totalDamageReceived += engagement.ValueRO.DamageReceived;

                    switch (engagement.ValueRO.Phase)
                    {
                        case EngagementPhase.Engaged:
                            engaged++;
                            break;
                        case EngagementPhase.Approaching:
                            approaching++;
                            break;
                        case EngagementPhase.Destroyed:
                            destroyed++;
                            break;
                    }
                }
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Combat] Combatants: {totalCombatants}, Engaged: {engaged}, Approaching: {approaching}, Destroyed: {destroyed}, DmgDealt: {totalDamageDealt:F0}, DmgRecv: {totalDamageReceived:F0}");
        }
    }
}
