using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Ships;
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

    
    /// <summary>
    /// Manages weapon cooldowns and firing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XWeaponSystem : ISystem
    {
        private ComponentLookup<CapabilityState> _capabilityStateLookup;
        private ComponentLookup<CapabilityEffectiveness> _effectivenessLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponMount>();
            _capabilityStateLookup = state.GetComponentLookup<CapabilityState>(true);
            _effectivenessLookup = state.GetComponentLookup<CapabilityEffectiveness>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            _capabilityStateLookup.Update(ref state);
            _effectivenessLookup.Update(ref state);

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
                float distance = math.distance(transform.ValueRO.Position, targetTransform.Position);

                var weaponBuffer = weapons;

                // Process each weapon mount
                for (int i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];

                    if (mount.IsEnabled == 0)
                    {
                        continue;
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

                    // Ammo check
                    if (mount.Weapon.AmmoPerShot > 0 && supply.ValueRO.Ammunition < mount.Weapon.AmmoPerShot)
                    {
                        continue;
                    }

                    // Apply effectiveness to cooldown (damaged weapons fire slower)
                    int adjustedCooldown = (int)(mount.Weapon.CooldownTicks / math.max(0.1f, effectivenessMultiplier));
                    adjustedCooldown = math.clamp(adjustedCooldown, 0, ushort.MaxValue);

                    // Fire weapon
                    mount.Weapon.CurrentCooldown = (ushort)adjustedCooldown;
                    mount.CurrentTarget = target;
                    weaponBuffer[i] = mount;

                    // Consume ammo
                    if (mount.Weapon.AmmoPerShot > 0)
                    {
                        supply.ValueRW.Ammunition -= mount.Weapon.AmmoPerShot;
                    }
                }
            }
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            _formationBonusLookup.Update(ref state);
            _formationIntegrityLookup.Update(ref state);
            _formationConfigLookup.Update(ref state);
            _cohesionMultipliersLookup.Update(ref state);
            _formationAssignmentLookup.Update(ref state);
            _entityLookup.Update(ref state);
            _advantageLookup.Update(ref state);

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
                    ApplyDamageToTarget(target, entity, mount.Weapon, rawDamage, isCritical, currentTick, ref state);

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
                ModuleTargetPolicyKind.DisableMobility when moduleClass == ModuleClass.Engine => 80,
                ModuleTargetPolicyKind.DisableFighting when IsWeaponClass(moduleClass) => 80,
                ModuleTargetPolicyKind.DisableFighting when moduleClass == ModuleClass.Hangar => 70,
                ModuleTargetPolicyKind.DisableSensors when moduleClass == ModuleClass.Sensor => 70,
                ModuleTargetPolicyKind.DisableLogistics when IsLogisticsClass(moduleClass) => 60,
                _ => 0
            };
        }

        private static bool IsWeaponClass(ModuleClass moduleClass)
        {
            return moduleClass == ModuleClass.BeamCannon
                   || moduleClass == ModuleClass.MassDriver
                   || moduleClass == ModuleClass.Missile
                   || moduleClass == ModuleClass.PointDefense;
        }

        private static bool IsLogisticsClass(ModuleClass moduleClass)
        {
            return moduleClass == ModuleClass.Cargo
                   || moduleClass == ModuleClass.Fabrication
                   || moduleClass == ModuleClass.Agriculture
                   || moduleClass == ModuleClass.Mining
                   || moduleClass == ModuleClass.Terraforming;
        }

        private static byte GetDefaultPriority(ModuleClass moduleClass)
        {
            return moduleClass switch
            {
                ModuleClass.Engine => 200,
                ModuleClass.BeamCannon => 150,
                ModuleClass.MassDriver => 150,
                ModuleClass.Missile => 150,
                ModuleClass.PointDefense => 140,
                ModuleClass.Shield => 120,
                ModuleClass.Armor => 100,
                ModuleClass.Sensor => 80,
                ModuleClass.Cargo => 50,
                ModuleClass.Hangar => 60,
                ModuleClass.Fabrication => 40,
                ModuleClass.Research => 40,
                ModuleClass.Medical => 30,
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
