using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Manages weapon cooldowns and firing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XWeaponSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponMount>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (weapons, engagement, transform, supply, entity) in
                SystemAPI.Query<DynamicBuffer<WeaponMount>, RefRO<Space4XEngagement>, RefRO<LocalTransform>, RefRW<SupplyStatus>>()
                    .WithEntityAccess())
            {
                // Skip if not engaged
                if (engagement.ValueRO.Phase != EngagementPhase.Engaged)
                {
                    continue;
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

                    // Fire weapon
                    mount.Weapon.CurrentCooldown = mount.Weapon.CooldownTicks;
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
    public partial struct Space4XDamageResolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponMount>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

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

                    // Formation bonus
                    rawDamage *= (1f + (float)engagement.ValueRO.FormationBonus);

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
    public partial struct Space4XCombatInitiationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TargetPriority>();
            state.RequireForUpdate<Space4XEngagement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
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
                    engagement.ValueRW.PrimaryTarget = priority.ValueRO.CurrentTarget;
                    engagement.ValueRW.Phase = distance <= maxRange ? EngagementPhase.Engaged : EngagementPhase.Approaching;
                    engagement.ValueRW.TargetDistance = distance;
                    engagement.ValueRW.EngagementDuration = 0;
                    engagement.ValueRW.DamageDealt = 0;
                    engagement.ValueRW.DamageReceived = 0;

                    // Calculate formation bonus
                    float cohesion = 1f;
                    if (SystemAPI.HasComponent<FormationAssignment>(entity))
                    {
                        // Would calculate actual cohesion from formation
                        cohesion = 0.8f;
                    }

                    VesselStanceMode stance = VesselStanceMode.Balanced;
                    if (SystemAPI.HasComponent<PatrolStance>(entity))
                    {
                        stance = SystemAPI.GetComponent<PatrolStance>(entity).Stance;
                    }

                    engagement.ValueRW.FormationBonus = (half)CombatMath.CalculateFormationBonus(cohesion, stance);
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

