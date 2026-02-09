using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Steering;
using PureDOTS.Runtime.Telemetry;
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
        private ComponentLookup<StrikeCraftFireDiscipline> _fireDisciplineLookup;
        private ComponentLookup<VesselAimDirective> _aimLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<CarrierTag> _carrierTagLookup;
        private ComponentLookup<Space4XFocusModifiers> _focusLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<Space4XNormalizedIndividualStats> _normalizedStatsLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;
        private ComponentLookup<Space4XOrbitalBandState> _orbitalBandLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private ComponentLookup<ModuleLimbProfile> _limbProfileLookup;
        private BufferLookup<ModuleLimbState> _limbStateLookup;
        private BufferLookup<ModuleLimbDamageEvent> _limbDamageLookup;
        private ComponentLookup<ModuleTarget> _moduleTargetLookup;
        private BufferLookup<PDCarrierModuleSlot> _moduleSlotLookup;
        private ComponentLookup<PDShipModule> _moduleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<Space4XShield> _shieldLookup;
        private ComponentLookup<Space4XArmor> _armorLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private BufferLookup<DamageEvent> _damageEventLookup;
        private EntityStorageInfoLookup _entityLookup;

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
            _fireDisciplineLookup = state.GetComponentLookup<StrikeCraftFireDiscipline>(true);
            _aimLookup = state.GetComponentLookup<VesselAimDirective>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _carrierTagLookup = state.GetComponentLookup<CarrierTag>(true);
            _focusLookup = state.GetComponentLookup<Space4XFocusModifiers>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _normalizedStatsLookup = state.GetComponentLookup<Space4XNormalizedIndividualStats>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
            _orbitalBandLookup = state.GetComponentLookup<Space4XOrbitalBandState>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(true);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(true);
            _limbProfileLookup = state.GetComponentLookup<ModuleLimbProfile>(true);
            _limbStateLookup = state.GetBufferLookup<ModuleLimbState>(true);
            _limbDamageLookup = state.GetBufferLookup<ModuleLimbDamageEvent>(false);
            _moduleTargetLookup = state.GetComponentLookup<ModuleTarget>(true);
            _moduleSlotLookup = state.GetBufferLookup<PDCarrierModuleSlot>(true);
            _moduleLookup = state.GetComponentLookup<PDShipModule>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(false);
            _shieldLookup = state.GetComponentLookup<Space4XShield>(false);
            _armorLookup = state.GetComponentLookup<Space4XArmor>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(false);
            _damageEventLookup = state.GetBufferLookup<DamageEvent>(false);
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

            uint currentTick = timeState.Tick;

            _capabilityStateLookup.Update(ref state);
            _effectivenessLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _strikeCraftStateLookup.Update(ref state);
            _dogfightMetricsLookup.Update(ref state);
            _dogfightTagLookup.Update(ref state);
            _fireDisciplineLookup.Update(ref state);
            _aimLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _carrierTagLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _normalizedStatsLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _physiqueLookup.Update(ref state);
            _orbitalBandLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _limbProfileLookup.Update(ref state);
            _limbStateLookup.Update(ref state);
            _limbDamageLookup.Update(ref state);
            _moduleTargetLookup.Update(ref state);
            _moduleSlotLookup.Update(ref state);
            _moduleLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _shieldLookup.Update(ref state);
            _armorLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _damageEventLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _shieldLookup.Update(ref state);
            _armorLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _damageEventLookup.Update(ref state);
            _entityLookup.Update(ref state);

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

            const float heatPenaltyStart = 0.65f;
            const float heatPenaltyScale = 0.75f;
            const float overheatDamageScale = 0.08f;
            const float defaultHeatDissipation = 0.02f;
            const float defaultHeatPerShot = 0.1f;
            const float defaultHeatCapacity = 1f;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

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
                if (target == Entity.Null || !_entityLookup.Exists(target))
                {
                    continue;
                }

                var rangeScale = ResolveRangeScale(entity);

                // Get target position
                if (!_transformLookup.HasComponent(target))
                {
                    continue;
                }
                var targetTransform = _transformLookup[target];
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

                var suppressFire = false;
                var suppressChance = 0f;
                if (_fireDisciplineLookup.HasComponent(entity))
                {
                    var discipline = _fireDisciplineLookup[entity];
                    if (discipline.SuppressFire != 0 &&
                        (discipline.UntilTick == 0 || currentTick <= discipline.UntilTick) &&
                        (discipline.Target == Entity.Null || discipline.Target == target))
                    {
                        suppressFire = true;
                        suppressChance = math.saturate(discipline.SuppressChance);
                    }
                }

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
                    var mountDirty = false;
                    var heatCapacity = mount.HeatCapacity > 0f ? mount.HeatCapacity : defaultHeatCapacity;
                    var heatDissipation = mount.HeatDissipation > 0f ? mount.HeatDissipation : defaultHeatDissipation;
                    var heatPerShot = mount.HeatPerShot > 0f ? mount.HeatPerShot : defaultHeatPerShot;
                    var heat = math.clamp(mount.Heat01, 0f, 1f);
                    var coolingRating = mount.CoolingRating;

                    if (mount.SourceModule != Entity.Null && _limbProfileLookup.HasComponent(mount.SourceModule))
                    {
                        coolingRating = (half)math.clamp(_limbProfileLookup[mount.SourceModule].Cooling, 0f, 1f);
                        heatCapacity = math.lerp(0.6f, 1.4f, (float)coolingRating);
                        heatDissipation = math.lerp(0.01f, 0.06f, (float)coolingRating);
                    }

                    if (heat > 0f)
                    {
                        var cooled = math.max(0f, heat - heatDissipation);
                        if (cooled != heat)
                        {
                            heat = cooled;
                            mount.Heat01 = heat;
                            mountDirty = true;
                        }
                    }

                    if (heat >= 1f && mount.SourceModule != Entity.Null)
                    {
                        if (!_limbDamageLookup.HasBuffer(mount.SourceModule))
                        {
                            ecb.AddBuffer<ModuleLimbDamageEvent>(mount.SourceModule);
                        }

                        var damage = math.max(0.02f, heatPerShot * overheatDamageScale);
                        ecb.AppendToBuffer(mount.SourceModule, new ModuleLimbDamageEvent
                        {
                            Family = ModuleLimbFamily.Cooling,
                            LimbId = ModuleLimbId.Unknown,
                            Damage = damage,
                            Tick = currentTick
                        });
                    }

                    if (mount.IsEnabled == 0)
                    {
                        if (mountDirty)
                        {
                            weaponBuffer[i] = mount;
                        }
                        continue;
                    }
                    if (weaponsDisabled)
                    {
                        if (hasSubsystemDisabled)
                        {
                            if (Space4XSubsystemUtility.IsWeaponMountDisabled(entity, i, subsystems, disabledSubsystems))
                            {
                                if (mountDirty)
                                {
                                    weaponBuffer[i] = mount;
                                }
                                continue;
                            }
                        }
                        else
                        {
                            if (Space4XSubsystemUtility.ShouldDisableMount(entity, i))
                            {
                                if (mountDirty)
                                {
                                    weaponBuffer[i] = mount;
                                }
                                continue;
                            }
                        }
                    }

                    // Cooldown tick
                    if (mount.Weapon.CurrentCooldown > 0)
                    {
                        mount.Weapon.CurrentCooldown--;
                        mountDirty = true;
                        weaponBuffer[i] = mount;
                        continue;
                    }

                    // Range check
                    if (distance > mount.Weapon.MaxRange * rangeScale)
                    {
                        if (mountDirty)
                        {
                            weaponBuffer[i] = mount;
                        }
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
                            if (mountDirty)
                            {
                                weaponBuffer[i] = mount;
                            }
                            continue;
                        }
                        coneForward = RotateYaw(forward, arcOffset);
                    }
                    if (math.dot(coneForward, aimDirection) < fireConeCos)
                    {
                        if (mountDirty)
                        {
                            weaponBuffer[i] = mount;
                        }
                        continue;
                    }

                    if (suppressFire && suppressChance > 0f)
                    {
                        var roll = DeterministicRoll(entity, target, currentTick, (uint)i);
                        if (roll < suppressChance)
                        {
                            if (mountDirty)
                            {
                                weaponBuffer[i] = mount;
                            }
                            continue;
                        }
                    }

                    // Ammo check
                    if (mount.Weapon.AmmoPerShot > 0 && supply.ValueRO.Ammunition < mount.Weapon.AmmoPerShot)
                    {
                        if (mountDirty)
                        {
                            weaponBuffer[i] = mount;
                        }
                        continue;
                    }

                    // Apply effectiveness to cooldown (damaged weapons fire slower)
                    int adjustedCooldown = (int)(mount.Weapon.CooldownTicks / math.max(0.1f, effectivenessMultiplier));
                    adjustedCooldown = (int)(adjustedCooldown / focusRofMultiplier);
                    if (heat > heatPenaltyStart)
                    {
                        var heatPenalty = math.saturate((heat - heatPenaltyStart) / math.max(0.0001f, 1f - heatPenaltyStart));
                        adjustedCooldown = (int)math.round(adjustedCooldown * (1f + heatPenalty * heatPenaltyScale));
                    }
                    adjustedCooldown = math.clamp(adjustedCooldown, 0, ushort.MaxValue);

                    // Fire weapon
                    mount.Weapon.CurrentCooldown = (ushort)adjustedCooldown;
                    mount.CurrentTarget = target;
                    heat = math.saturate(heat + (heatPerShot / math.max(0.1f, heatCapacity)));
                    mount.Heat01 = heat;
                    mountDirty = true;
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

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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
            var tracking = math.clamp(Space4XWeapon.ResolveTracking(weapon), 0.05f, 1f);
            var basePenalty = math.lerp(0.18f, 0.04f, tracking);
            var skillFactor = math.lerp(basePenalty * 1.4f, basePenalty * 0.6f, math.saturate(gunnerySkill));
            return math.saturate(1f - omega * skillFactor);
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

        private static float DeterministicRoll(Entity actor, Entity target, uint tick, uint salt)
        {
            var hash = math.hash(new uint4((uint)actor.Index, (uint)target.Index, tick, salt));
            return (hash & 0xFFFF) / 65535f;
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
        private ComponentLookup<Space4XOrbitalBandState> _orbitalBandLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private BufferLookup<DamageScarEvent> _scarLookup;
        private ComponentLookup<SubsystemTargetDirective> _subsystemDirectiveLookup;
        private ComponentLookup<Space4XFocusModifiers> _focusLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private BufferLookup<ModuleLimbState> _limbStateLookup;
        private BufferLookup<ModuleLimbDamageEvent> _limbDamageLookup;
        private ComponentLookup<ModuleTarget> _moduleTargetLookup;
        private BufferLookup<PDCarrierModuleSlot> _moduleSlotLookup;
        private ComponentLookup<PDShipModule> _moduleLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<Space4XShield> _shieldLookup;
        private ComponentLookup<Space4XArmor> _armorLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private BufferLookup<DamageEvent> _damageEventLookup;
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
            _orbitalBandLookup = state.GetComponentLookup<Space4XOrbitalBandState>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(false);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(false);
            _scarLookup = state.GetBufferLookup<DamageScarEvent>(false);
            _subsystemDirectiveLookup = state.GetComponentLookup<SubsystemTargetDirective>(true);
            _focusLookup = state.GetComponentLookup<Space4XFocusModifiers>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _limbStateLookup = state.GetBufferLookup<ModuleLimbState>(true);
            _limbDamageLookup = state.GetBufferLookup<ModuleLimbDamageEvent>(false);
            _moduleTargetLookup = state.GetComponentLookup<ModuleTarget>(true);
            _moduleSlotLookup = state.GetBufferLookup<PDCarrierModuleSlot>(true);
            _moduleLookup = state.GetComponentLookup<PDShipModule>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(false);
            _shieldLookup = state.GetComponentLookup<Space4XShield>(false);
            _armorLookup = state.GetComponentLookup<Space4XArmor>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(false);
            _damageEventLookup = state.GetBufferLookup<DamageEvent>(false);
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

            var combatTuning = Space4XCombatTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XCombatTuningConfig>(out var combatTuningSingleton))
            {
                combatTuning = combatTuningSingleton;
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
            _orbitalBandLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _scarLookup.Update(ref state);
            _subsystemDirectiveLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _limbStateLookup.Update(ref state);
            _limbDamageLookup.Update(ref state);
            _moduleTargetLookup.Update(ref state);
            _moduleSlotLookup.Update(ref state);
            _moduleLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (weapons, engagement, transform, entity) in
                SystemAPI.Query<DynamicBuffer<WeaponMount>, RefRW<Space4XEngagement>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                if (engagement.ValueRO.Phase != EngagementPhase.Engaged)
                {
                    continue;
                }

                Entity target = engagement.ValueRO.PrimaryTarget;
                if (target == Entity.Null || !_entityLookup.Exists(target))
                {
                    continue;
                }

                // Get target components
                if (!_transformLookup.HasComponent(target))
                {
                    continue;
                }

                var targetTransform = _transformLookup[target];
                float distance = math.distance(transform.ValueRO.Position, targetTransform.Position);

                // Get target evasion
                float evasion = 0f;
                if (_engagementLookup.HasComponent(target))
                {
                    evasion = (float)_engagementLookup[target].EvasionModifier;
                }

                var focusAccuracyBonus = 0f;
                if (TryResolveFocusModifiers(entity, out var focusModifiers))
                {
                    focusAccuracyBonus = (float)focusModifiers.AccuracyBonus;
                }

                var gunnerySkill = ResolveGunnerySkill(entity, combatTuning);
                var toTarget = targetTransform.Position - transform.ValueRO.Position;
                var directionToTarget = math.lengthsq(toTarget) > 1e-6f ? math.normalizesafe(toTarget) : math.forward(transform.ValueRO.Rotation);
                var attackerVelocity = _movementLookup.HasComponent(entity) ? _movementLookup[entity].Velocity : float3.zero;
                var targetVelocity = _movementLookup.HasComponent(target) ? _movementLookup[target].Velocity : float3.zero;
                var relativeVelocity = targetVelocity - attackerVelocity;
                var rangeScale = ResolveRangeScale(entity);
                var weaponsBuffer = weapons;

                // Process weapons that just fired (cooldown == max)
                for (int i = 0; i < weaponsBuffer.Length; i++)
                {
                    var mount = weaponsBuffer[i];

                    if (mount.CurrentTarget != target || mount.Weapon.CurrentCooldown != mount.Weapon.CooldownTicks)
                    {
                        continue;
                    }

                    mount.ShotsFired++;

                    // Calculate hit
                    float hitChance = CombatMath.CalculateHitChance(
                        (float)mount.Weapon.BaseAccuracy,
                        evasion,
                        distance,
                        mount.Weapon.OptimalRange * rangeScale,
                        mount.Weapon.MaxRange * rangeScale
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

                    var trackingPenalty = ResolveTrackingPenalty(mount.Weapon, distance, directionToTarget, relativeVelocity, gunnerySkill, combatTuning);
                    hitChance = math.clamp(hitChance * trackingPenalty, 0f, 1f);

                    if (random.NextFloat() > hitChance)
                    {
                        weaponsBuffer[i] = mount;
                        continue; // Miss
                    }

                    mount.ShotsHit++;
                    weaponsBuffer[i] = mount;

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
                    if (_advantageLookup.HasComponent(entity))
                    {
                        var attackerPos = transform.ValueRO.Position;
                        var targetPos = targetTransform.Position;
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
                    ApplyDamageToTarget(target, entity, mount.Weapon, rawDamage, isCritical, currentTick, transform.ValueRO, targetTransform, ref ecb);

                    engagement.ValueRW.DamageDealt += rawDamage;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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
            ref EntityCommandBuffer ecb)
        {
            float remainingDamage = rawDamage;
            float shieldDamage = 0f;
            float armorDamage = 0f;
            float hullDamage = 0f;
            var damageType = Space4XWeapon.ResolveDamageType(weapon.Type, weapon.DamageType);

            // Shield absorption
            if (_shieldLookup.HasComponent(target))
            {
                var shield = _shieldLookup[target];

                if (shield.Current > 0)
                {
                    float resistance = CombatMath.GetWeaponResistance(damageType, shield);
                    float effectiveDamage = CombatMath.CalculateShieldDamage(remainingDamage, (float)weapon.ShieldModifier, resistance);

                    shieldDamage = math.min(shield.Current, effectiveDamage);
                    shield.Current -= shieldDamage;
                    shield.CurrentDelay = shield.RechargeDelay;

                    remainingDamage = math.max(0, effectiveDamage - shieldDamage);

                    _shieldLookup[target] = shield;
                }
            }

            // Armor mitigation
            if (remainingDamage > 0 && _armorLookup.HasComponent(target))
            {
                var armor = _armorLookup[target];
                float resistance = CombatMath.GetArmorResistance(damageType, armor);

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
            if (remainingDamage > 0 && _hullLookup.HasComponent(target))
            {
                var hull = _hullLookup[target];
                hullDamage = remainingDamage;
                hull.Current = (half)math.max(0f, (float)hull.Current - hullDamage);
                _hullLookup[target] = hull;
            }

            if (armorDamage > 0f || hullDamage > 0f)
            {
                TryApplyLimbDamage(target, source, damageType, armorDamage, hullDamage, tick, ref ecb);
            }

            if (hullDamage > 0f)
            {
                ApplySubsystemDamage(target, source, weapon, hullDamage, isCritical, tick);
                TryEmitScarEvent(target, sourceTransform, targetTransform, hullDamage, weapon.Type, isCritical, tick);
            }

            // Log damage event
            if (_damageEventLookup.HasBuffer(target))
            {
                var events = _damageEventLookup[target];
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
            if (_engagementLookup.HasComponent(target))
            {
                var targetEngagement = _engagementLookup[target];
                targetEngagement.DamageReceived += rawDamage;
                _engagementLookup[target] = targetEngagement;
            }
        }

        private void TryApplyLimbDamage(
            Entity targetShip,
            Entity source,
            Space4XDamageType damageType,
            float armorDamage,
            float hullDamage,
            uint tick,
            ref EntityCommandBuffer ecb)
        {
            var targetModule = ResolveTargetModule(source, targetShip);
            if (targetModule == Entity.Null)
            {
                return;
            }

            if (!_limbStateLookup.HasBuffer(targetModule))
            {
                return;
            }

            var damageBasis = math.max(hullDamage, armorDamage * 0.35f);
            if (damageBasis <= 0f)
            {
                return;
            }

            var limbDamage = math.saturate(damageBasis * 0.0015f);
            if (limbDamage <= 0f)
            {
                return;
            }

            limbDamage *= ResolveLimbDamageScale(damageType);
            if (limbDamage <= 0f)
            {
                return;
            }

            if (!_limbDamageLookup.HasBuffer(targetModule))
            {
                ecb.AddBuffer<ModuleLimbDamageEvent>(targetModule);
            }

            ecb.AppendToBuffer(targetModule, new ModuleLimbDamageEvent
            {
                Family = ResolveLimbFamily(damageType),
                LimbId = ModuleLimbId.Unknown,
                Damage = limbDamage,
                Tick = tick
            });
        }

        private Entity ResolveTargetModule(Entity source, Entity targetShip)
        {
            if (_moduleTargetLookup.HasComponent(source))
            {
                var moduleTarget = _moduleTargetLookup[source];
                if (moduleTarget.TargetShip == targetShip && moduleTarget.TargetModule != Entity.Null && _moduleLookup.HasComponent(moduleTarget.TargetModule))
                {
                    return moduleTarget.TargetModule;
                }
            }

            if (_moduleSlotLookup.HasBuffer(targetShip))
            {
                var slots = _moduleSlotLookup[targetShip];
                for (int i = 0; i < slots.Length; i++)
                {
                    var module = slots[i].InstalledModule;
                    if (module == Entity.Null)
                    {
                        continue;
                    }
                    if (!_moduleLookup.HasComponent(module))
                    {
                        continue;
                    }
                    return module;
                }
            }

            return Entity.Null;
        }

        private static ModuleLimbFamily ResolveLimbFamily(Space4XDamageType damageType)
        {
            return damageType switch
            {
                Space4XDamageType.Thermal => ModuleLimbFamily.Cooling,
                Space4XDamageType.EM => ModuleLimbFamily.Power,
                Space4XDamageType.Radiation => ModuleLimbFamily.Sensors,
                Space4XDamageType.Caustic => ModuleLimbFamily.Structural,
                Space4XDamageType.Energy => ModuleLimbFamily.Lensing,
                Space4XDamageType.Kinetic => ModuleLimbFamily.Structural,
                Space4XDamageType.Explosive => ModuleLimbFamily.Structural,
                _ => ModuleLimbFamily.Structural
            };
        }

        private static float ResolveLimbDamageScale(Space4XDamageType damageType)
        {
            return damageType switch
            {
                Space4XDamageType.Thermal => 1.15f,
                Space4XDamageType.EM => 1.1f,
                Space4XDamageType.Caustic => 1.2f,
                _ => 1f
            };
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

        private float ResolveGunnerySkill(Entity shipEntity, in Space4XCombatTuningConfig tuning)
        {
            var pilot = ResolvePilot(shipEntity);
            var profile = pilot != Entity.Null ? pilot : shipEntity;

            var tacticsWeight = math.max(0f, tuning.GunneryTacticsWeight);
            var finesseWeight = math.max(0f, tuning.GunneryFinesseWeight);
            var commandWeight = math.max(0f, tuning.GunneryCommandWeight);
            var weightSum = tacticsWeight + finesseWeight + commandWeight;
            if (weightSum <= 1e-4f)
            {
                tacticsWeight = 0.34f;
                finesseWeight = 0.33f;
                commandWeight = 0.33f;
                weightSum = 1f;
            }
            var invWeight = 1f / weightSum;
            tacticsWeight *= invWeight;
            finesseWeight *= invWeight;
            commandWeight *= invWeight;

            if (_normalizedStatsLookup.HasComponent(profile))
            {
                var stats = _normalizedStatsLookup[profile];
                var skill = stats.Tactics * tacticsWeight + stats.Finesse * finesseWeight + stats.Command * commandWeight;
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

            var fallbackSkill = tactics * tacticsWeight + finesse * finesseWeight + command * commandWeight;
            return math.saturate(fallbackSkill);
        }

        private static float ResolveTrackingPenalty(
            in Space4XWeapon weapon,
            float distance,
            float3 directionToTarget,
            float3 relativeVelocity,
            float gunnerySkill,
            in Space4XCombatTuningConfig tuning)
        {
            if (distance <= 0.01f)
            {
                return 1f;
            }

            var omega = math.length(math.cross(relativeVelocity, directionToTarget)) / math.max(distance, 0.1f);
            var tracking = math.clamp(Space4XWeapon.ResolveTracking(weapon), 0.05f, 1f);
            var basePenalty = math.lerp(0.18f, 0.04f, tracking);

            var minScale = math.max(0f, tuning.TrackingPenaltyMinScale);
            var maxScale = math.max(minScale, tuning.TrackingPenaltyMaxScale);
            var skillFactor = math.lerp(basePenalty * maxScale, basePenalty * minScale, math.saturate(gunnerySkill));
            return math.saturate(1f - omega * skillFactor);
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
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SubsystemHealth>();
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(false);
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

            _subsystemDisabledLookup.Update(ref state);

            foreach (var (subsystems, entity) in SystemAPI.Query<DynamicBuffer<SubsystemHealth>>().WithEntityAccess())
            {
                var subsystemsBuffer = subsystems;
                var hasDisabled = _subsystemDisabledLookup.HasBuffer(entity);
                DynamicBuffer<SubsystemDisabled> disabled = default;
                if (hasDisabled)
                {
                    disabled = _subsystemDisabledLookup[entity];
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
        private ComponentLookup<InCombatTag> _inCombatTagLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEngagement>();
            state.RequireForUpdate<TimeState>();
            _inCombatTagLookup = state.GetComponentLookup<InCombatTag>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _inCombatTagLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (engagement, hull, transform, entity) in
                SystemAPI.Query<RefRW<Space4XEngagement>, RefRO<HullIntegrity>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Check for destruction
                if ((float)hull.ValueRO.Current <= 0)
                {
                    engagement.ValueRW.Phase = EngagementPhase.Destroyed;
                    if (_inCombatTagLookup.HasComponent(entity))
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
                    _transformLookup.HasComponent(engagement.ValueRO.PrimaryTarget))
                {
                    var targetTransform = _transformLookup[engagement.ValueRO.PrimaryTarget];
                    engagement.ValueRW.TargetDistance = math.distance(transform.ValueRO.Position, targetTransform.Position);
                }

                // Check if target destroyed
                if (engagement.ValueRO.PrimaryTarget != Entity.Null)
                {
                    if (!_entityLookup.Exists(engagement.ValueRO.PrimaryTarget))
                    {
                        engagement.ValueRW.Phase = EngagementPhase.Victorious;
                        engagement.ValueRW.PrimaryTarget = Entity.Null;
                    }
                    else if (_hullLookup.HasComponent(engagement.ValueRO.PrimaryTarget))
                    {
                        var targetHull = _hullLookup[engagement.ValueRO.PrimaryTarget];
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
        private ComponentLookup<Space4XOrbitalBandState> _orbitalBandLookup;
        private EntityStorageInfoLookup _entityLookup;
        private BufferLookup<PDCarrierModuleSlot> _slotLookup;
        private ComponentLookup<PDShipModule> _moduleLookup;
        private ComponentLookup<ModuleTargetPriority> _priorityLookup;
        private ComponentLookup<PDModuleHealth> _healthLookup;
        private ComponentLookup<ModuleTargetPolicy> _modulePolicyLookup;
        private ComponentLookup<ModuleTargetPolicyOverride> _modulePolicyOverrideLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ModuleTarget> _moduleTargetLookup;
        private ComponentLookup<PatrolStance> _patrolStanceLookup;
        private ComponentLookup<InCombatTag> _inCombatTagLookup;
        private BufferLookup<WeaponMount> _weaponLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetPriority>();
            state.RequireForUpdate<Space4XEngagement>();
            state.RequireForUpdate<TimeState>();

            _formationBonusLookup = state.GetComponentLookup<FormationBonus>(true);
            _formationIntegrityLookup = state.GetComponentLookup<FormationIntegrity>(true);
            _formationConfigLookup = state.GetComponentLookup<FormationCombatConfig>(true);
            _formationAssignmentLookup = state.GetComponentLookup<FormationAssignment>(true);
            _orbitalBandLookup = state.GetComponentLookup<Space4XOrbitalBandState>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
            _slotLookup = state.GetBufferLookup<PDCarrierModuleSlot>(true);
            _moduleLookup = state.GetComponentLookup<PDShipModule>(true);
            _priorityLookup = state.GetComponentLookup<ModuleTargetPriority>(true);
            _healthLookup = state.GetComponentLookup<PDModuleHealth>(true);
            _modulePolicyLookup = state.GetComponentLookup<ModuleTargetPolicy>(true);
            _modulePolicyOverrideLookup = state.GetComponentLookup<ModuleTargetPolicyOverride>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _moduleTargetLookup = state.GetComponentLookup<ModuleTarget>(true);
            _patrolStanceLookup = state.GetComponentLookup<PatrolStance>(true);
            _inCombatTagLookup = state.GetComponentLookup<InCombatTag>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMount>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _formationBonusLookup.Update(ref state);
            _formationIntegrityLookup.Update(ref state);
            _formationConfigLookup.Update(ref state);
            _formationAssignmentLookup.Update(ref state);
            _orbitalBandLookup.Update(ref state);
            _entityLookup.Update(ref state);
            _slotLookup.Update(ref state);
            _moduleLookup.Update(ref state);
            _priorityLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _modulePolicyLookup.Update(ref state);
            _modulePolicyOverrideLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _moduleTargetLookup.Update(ref state);
            _patrolStanceLookup.Update(ref state);
            _inCombatTagLookup.Update(ref state);
            _weaponLookup.Update(ref state);

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
                if (!_transformLookup.HasComponent(priority.ValueRO.CurrentTarget))
                {
                    continue;
                }

                var targetTransform = _transformLookup[priority.ValueRO.CurrentTarget];
                float distance = math.distance(transform.ValueRO.Position, targetTransform.Position);

                var rangeScale = ResolveRangeScale(entity);

                // Get max weapon range
                float maxRange = 500f; // Default
                if (_weaponLookup.HasBuffer(entity))
                {
                    var weapons = _weaponLookup[entity];
                    for (int i = 0; i < weapons.Length; i++)
                    {
                        if (weapons[i].Weapon.MaxRange > maxRange)
                        {
                            maxRange = weapons[i].Weapon.MaxRange;
                        }
                    }
                }
                maxRange *= rangeScale;

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
                        var moduleTarget = SelectModuleTarget(entity, targetShip, timeState.Tick);

                        if (moduleTarget != Entity.Null)
                        {
                            // Add ModuleTarget component to attacker
                            if (!_moduleTargetLookup.HasComponent(entity))
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
                    if (_patrolStanceLookup.HasComponent(entity))
                    {
                        stance = _patrolStanceLookup[entity].Stance;
                    }
                    engagement.ValueRW.EvasionModifier = (half)(stance == VesselStanceMode.Evasive ? 0.3f : 0.1f);

                    // Add combat tag
                    if (!_inCombatTagLookup.HasComponent(entity))
                    {
                        ecb.AddComponent<InCombatTag>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
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

        private Entity SelectModuleTarget(Entity attacker, Entity targetShip, uint currentTick)
        {
            if (_modulePolicyOverrideLookup.HasComponent(attacker))
            {
                var overridePolicy = _modulePolicyOverrideLookup[attacker];
                if (overridePolicy.Kind != ModuleTargetPolicyKind.Default &&
                    (overridePolicy.ExpireTick == 0 || currentTick <= overridePolicy.ExpireTick) &&
                    (overridePolicy.TargetShip == Entity.Null || overridePolicy.TargetShip == targetShip))
                {
                    return SelectModuleTargetWithPolicy(attacker, targetShip, new ModuleTargetPolicy
                    {
                        Kind = overridePolicy.Kind
                    });
                }
            }

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
        private ComponentLookup<ScenarioSide> _sideLookup;

        private struct SideTally
        {
            public int ShipsTotal;
            public int ShipsDestroyed;
            public int ShipsEngaged;
            public int ShipsApproaching;
            public int ShipsDisabled;
            public int ShipsAlive;
            public float DamageDealt;
            public float DamageReceived;
            public float HullCurrent;
            public float HullMax;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryExportConfig>();
            _sideLookup = state.GetComponentLookup<ScenarioSide>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                config.Enabled == 0 ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!TryGetTelemetryMetricBuffer(ref state, out var metricBuffer))
            {
                return;
            }

            _sideLookup.Update(ref state);

            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            int totalCombatants = 0;
            int engaged = 0;
            int approaching = 0;
            int destroyed = 0;
            int disabled = 0;
            float totalDamageDealt = 0f;
            float totalDamageReceived = 0f;

            var sideStats = new NativeHashMap<byte, SideTally>(8, Allocator.Temp);

            foreach (var (engagement, entity) in SystemAPI.Query<RefRO<Space4XEngagement>>().WithEntityAccess())
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
                        case EngagementPhase.Disabled:
                            disabled++;
                            break;
                    }

                    if (_sideLookup.HasComponent(entity))
                    {
                        var side = _sideLookup[entity].Side;
                        var tally = sideStats.TryGetValue(side, out var existing) ? existing : default;
                        tally.DamageDealt += engagement.ValueRO.DamageDealt;
                        tally.DamageReceived += engagement.ValueRO.DamageReceived;
                        switch (engagement.ValueRO.Phase)
                        {
                            case EngagementPhase.Engaged:
                                tally.ShipsEngaged++;
                                break;
                            case EngagementPhase.Approaching:
                                tally.ShipsApproaching++;
                                break;
                            case EngagementPhase.Disabled:
                                tally.ShipsDisabled++;
                                break;
                            case EngagementPhase.Destroyed:
                                tally.ShipsDestroyed++;
                                break;
                        }

                        sideStats[side] = tally;
                    }
                }
            }

            foreach (var (hull, side) in SystemAPI.Query<RefRO<HullIntegrity>, RefRO<ScenarioSide>>())
            {
                var tally = sideStats.TryGetValue(side.ValueRO.Side, out var existing) ? existing : default;
                tally.ShipsTotal++;
                if ((float)hull.ValueRO.Current <= 0f)
                {
                    tally.ShipsDestroyed++;
                }
                else
                {
                    tally.ShipsAlive++;
                }
                tally.HullCurrent += math.max(0f, hull.ValueRO.Current);
                tally.HullMax += math.max(0f, hull.ValueRO.Max);
                sideStats[side.ValueRO.Side] = tally;
            }

            // Aggregate weapon tracking telemetry.
            uint shotsFiredTotal = 0;
            uint shotsHitTotal = 0;
            foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>())
            {
                for (int i = 0; i < weapons.Length; i++)
                {
                    shotsFiredTotal += weapons[i].ShotsFired;
                    shotsHitTotal += weapons[i].ShotsHit;
                }
            }

            uint shotsMissedTotal = shotsFiredTotal >= shotsHitTotal ? shotsFiredTotal - shotsHitTotal : 0;

            // Aggregate damage by type since last tick.
            float deltaEnergy = 0f;
            float deltaThermal = 0f;
            float deltaEm = 0f;
            float deltaRadiation = 0f;
            float deltaCaustic = 0f;
            float deltaKinetic = 0f;
            float deltaExplosive = 0f;

            Space4XCombatTelemetry telemetry = default;
            Entity telemetryEntity;
            if (!SystemAPI.TryGetSingletonEntity<Space4XCombatTelemetry>(out telemetryEntity))
            {
                telemetryEntity = state.EntityManager.CreateEntity(typeof(Space4XCombatTelemetry));
                telemetry = new Space4XCombatTelemetry();
            }
            else
            {
                telemetry = state.EntityManager.GetComponentData<Space4XCombatTelemetry>(telemetryEntity);
            }

            foreach (var damageEvents in SystemAPI.Query<DynamicBuffer<DamageEvent>>())
            {
                for (int i = 0; i < damageEvents.Length; i++)
                {
                    var damageEvent = damageEvents[i];
                    if (damageEvent.Tick <= telemetry.LastProcessedTick)
                    {
                        continue;
                    }

                    var damageType = Space4XWeapon.ResolveDamageType(damageEvent.WeaponType);
                    var amount = damageEvent.RawDamage;
                    switch (damageType)
                    {
                        case Space4XDamageType.Energy:
                            deltaEnergy += amount;
                            break;
                        case Space4XDamageType.Thermal:
                            deltaThermal += amount;
                            break;
                        case Space4XDamageType.EM:
                            deltaEm += amount;
                            break;
                        case Space4XDamageType.Radiation:
                            deltaRadiation += amount;
                            break;
                        case Space4XDamageType.Caustic:
                            deltaCaustic += amount;
                            break;
                        case Space4XDamageType.Kinetic:
                            deltaKinetic += amount;
                            break;
                        case Space4XDamageType.Explosive:
                            deltaExplosive += amount;
                            break;
                    }
                }

                if (damageEvents.Length > 0)
                {
                    damageEvents.Clear();
                }
            }

            telemetry.LastProcessedTick = currentTick;
            telemetry.DamageEnergyDelta = deltaEnergy;
            telemetry.DamageThermalDelta = deltaThermal;
            telemetry.DamageEMDelta = deltaEm;
            telemetry.DamageRadiationDelta = deltaRadiation;
            telemetry.DamageCausticDelta = deltaCaustic;
            telemetry.DamageKineticDelta = deltaKinetic;
            telemetry.DamageExplosiveDelta = deltaExplosive;
            telemetry.TotalDamageEnergy += deltaEnergy;
            telemetry.TotalDamageThermal += deltaThermal;
            telemetry.TotalDamageEM += deltaEm;
            telemetry.TotalDamageRadiation += deltaRadiation;
            telemetry.TotalDamageCaustic += deltaCaustic;
            telemetry.TotalDamageKinetic += deltaKinetic;
            telemetry.TotalDamageExplosive += deltaExplosive;

            telemetry.ShotsFiredDelta = shotsFiredTotal >= telemetry.TotalShotsFired ? shotsFiredTotal - telemetry.TotalShotsFired : 0;
            telemetry.ShotsHitDelta = shotsHitTotal >= telemetry.TotalShotsHit ? shotsHitTotal - telemetry.TotalShotsHit : 0;
            telemetry.ShotsMissedDelta = shotsMissedTotal >= telemetry.TotalShotsMissed ? shotsMissedTotal - telemetry.TotalShotsMissed : 0;

            telemetry.TotalShotsFired = shotsFiredTotal;
            telemetry.TotalShotsHit = shotsHitTotal;
            telemetry.TotalShotsMissed = shotsMissedTotal;

            state.EntityManager.SetComponentData(telemetryEntity, telemetry);

            metricBuffer.AddMetric("space4x.combat.combatants.total", totalCombatants, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.combatants.engaged", engaged, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.combatants.approaching", approaching, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.combatants.destroyed", destroyed, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.combatants.disabled", disabled, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.damage.dealt_total", totalDamageDealt, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.received_total", totalDamageReceived, TelemetryMetricUnit.Custom);

            metricBuffer.AddMetric("space4x.combat.shots.fired_total", telemetry.TotalShotsFired, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.shots.hit_total", telemetry.TotalShotsHit, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.shots.missed_total", telemetry.TotalShotsMissed, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.shots.fired_delta", telemetry.ShotsFiredDelta, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.shots.hit_delta", telemetry.ShotsHitDelta, TelemetryMetricUnit.Count);
            metricBuffer.AddMetric("space4x.combat.shots.missed_delta", telemetry.ShotsMissedDelta, TelemetryMetricUnit.Count);

            metricBuffer.AddMetric("space4x.combat.damage.energy.total", telemetry.TotalDamageEnergy, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.thermal.total", telemetry.TotalDamageThermal, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.em.total", telemetry.TotalDamageEM, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.radiation.total", telemetry.TotalDamageRadiation, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.caustic.total", telemetry.TotalDamageCaustic, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.kinetic.total", telemetry.TotalDamageKinetic, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.explosive.total", telemetry.TotalDamageExplosive, TelemetryMetricUnit.Custom);

            metricBuffer.AddMetric("space4x.combat.damage.energy.delta", telemetry.DamageEnergyDelta, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.thermal.delta", telemetry.DamageThermalDelta, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.em.delta", telemetry.DamageEMDelta, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.radiation.delta", telemetry.DamageRadiationDelta, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.caustic.delta", telemetry.DamageCausticDelta, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.kinetic.delta", telemetry.DamageKineticDelta, TelemetryMetricUnit.Custom);
            metricBuffer.AddMetric("space4x.combat.damage.explosive.delta", telemetry.DamageExplosiveDelta, TelemetryMetricUnit.Custom);

            using var sidePairs = sideStats.GetKeyValueArrays(Allocator.Temp);
            metricBuffer.AddMetric("space4x.combat.sides.count", sidePairs.Length, TelemetryMetricUnit.Count);
            for (int i = 0; i < sidePairs.Length; i++)
            {
                var side = sidePairs.Keys[i];
                var tally = sidePairs.Values[i];
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.ships.total"), tally.ShipsTotal, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.ships.destroyed"), tally.ShipsDestroyed, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.ships.engaged"), tally.ShipsEngaged, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.ships.approaching"), tally.ShipsApproaching, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.ships.disabled"), tally.ShipsDisabled, TelemetryMetricUnit.Count);
                var shipsAlive = tally.ShipsAlive > 0 ? tally.ShipsAlive : math.max(0, tally.ShipsTotal - tally.ShipsDestroyed);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.ships.alive"), shipsAlive, TelemetryMetricUnit.Count);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.hull.current_total"), tally.HullCurrent, TelemetryMetricUnit.Custom);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.hull.max_total"), tally.HullMax, TelemetryMetricUnit.Custom);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.damage.dealt"), tally.DamageDealt, TelemetryMetricUnit.Custom);
                metricBuffer.AddMetric(new FixedString64Bytes($"space4x.combat.side.{side}.damage.received"), tally.DamageReceived, TelemetryMetricUnit.Custom);
            }

            sideStats.Dispose();
        }

        private bool TryGetTelemetryMetricBuffer(ref SystemState state, out DynamicBuffer<TelemetryMetric> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryRef.Stream))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryRef.Stream);
            return true;
        }
    }
}
