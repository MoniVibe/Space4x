using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Math;
using Space4X.Runtime;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Regenerates focus over time and handles exhaustion.
    /// Will be replaced by PureDOTS FocusRegenSystem once available.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFocusRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEntityFocus>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (focus, profile) in SystemAPI.Query<RefRW<Space4XEntityFocus>, RefRO<OfficerFocusProfile>>())
            {
                // Skip if in coma
                if (focus.ValueRO.IsInComa != 0)
                {
                    continue;
                }

                // Skip if not on duty
                if (profile.ValueRO.IsOnDuty == 0)
                {
                    // Faster regen when off duty
                    float offDutyRegen = focus.ValueRO.BaseRegenRate * 2f;
                    focus.ValueRW.CurrentFocus = math.min(
                        focus.ValueRO.MaxFocus,
                        focus.ValueRO.CurrentFocus + offDutyRegen
                    );

                    // Exhaustion recovery when off duty
                    if (focus.ValueRO.ExhaustionLevel > 0)
                    {
                        focus.ValueRW.ExhaustionLevel = (byte)math.max(0, focus.ValueRO.ExhaustionLevel - 1);
                    }
                    continue;
                }

                // Calculate net regen (regen - drain)
                float netRegen = focus.ValueRO.BaseRegenRate - focus.ValueRO.TotalDrainRate;

                // Mental resilience reduces drain impact
                float resilienceBonus = (float)profile.ValueRO.MentalResilience * 0.2f;
                if (netRegen < 0)
                {
                    netRegen *= (1f - resilienceBonus);
                }

                // Apply regen/drain
                focus.ValueRW.CurrentFocus = math.clamp(
                    focus.ValueRO.CurrentFocus + netRegen,
                    0f,
                    focus.ValueRO.MaxFocus
                );

                // Exhaustion accumulates when focus is low and draining
                if (focus.ValueRO.CurrentFocus < focus.ValueRO.MaxFocus * 0.2f && focus.ValueRO.TotalDrainRate > 0)
                {
                    float exhaustionGain = (1f - focus.ValueRO.CurrentFocus / (focus.ValueRO.MaxFocus * 0.2f)) * 0.1f;
                    exhaustionGain *= (1f - (float)profile.ValueRO.MentalResilience);

                    focus.ValueRW.ExhaustionLevel = (byte)math.min(100, focus.ValueRO.ExhaustionLevel + (int)exhaustionGain);
                }

                // Slow exhaustion recovery when focus is high
                if (focus.ValueRO.CurrentFocus > focus.ValueRO.MaxFocus * 0.8f && focus.ValueRO.ExhaustionLevel > 0)
                {
                    focus.ValueRW.ExhaustionLevel = (byte)math.max(0, focus.ValueRO.ExhaustionLevel - 1);
                }

                // Coma trigger at max exhaustion with empty focus
                if (focus.ValueRO.ExhaustionLevel >= 100 && focus.ValueRO.CurrentFocus <= 0)
                {
                    focus.ValueRW.IsInComa = 1;
                }

                focus.ValueRW.LastUpdateTick = currentTick;
            }
        }
    }

    /// <summary>
    /// Processes focus ability activation and deactivation requests.
    /// Will be replaced by PureDOTS FocusAbilitySystem once available.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFocusRegenSystem))]
    public partial struct Space4XFocusAbilitySystem : ISystem
    {
        private ComponentLookup<ShipSpecialEnergyState> _specialEnergyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEntityFocus>();
            _specialEnergyLookup = state.GetComponentLookup<ShipSpecialEnergyState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            _specialEnergyLookup.Update(ref state);

            // Process activation requests
            foreach (var (request, focus, profile, abilities, entity) in
                SystemAPI.Query<RefRO<FocusAbilityRequest>, RefRW<Space4XEntityFocus>, RefRO<OfficerFocusProfile>, DynamicBuffer<Space4XActiveFocusAbility>>()
                    .WithEntityAccess())
            {
                var abilitiesBuffer = abilities;
                var abilityType = (Space4XFocusAbilityType)request.ValueRO.RequestedAbility;

                // Check if can activate
                if (!Space4XFocusAbilityDefinitions.CanActivate(abilityType, focus.ValueRO, profile.ValueRO))
                {
                    ecb.RemoveComponent<FocusAbilityRequest>(entity);
                    continue;
                }

                // Check if already active
                bool alreadyActive = false;
                for (int i = 0; i < abilitiesBuffer.Length; i++)
                {
                    if (abilitiesBuffer[i].AbilityType == request.ValueRO.RequestedAbility)
                    {
                        alreadyActive = true;
                        break;
                    }
                }

                if (alreadyActive || abilitiesBuffer.Length >= abilitiesBuffer.Capacity)
                {
                    ecb.RemoveComponent<FocusAbilityRequest>(entity);
                    continue;
                }

                if (_specialEnergyLookup.HasComponent(entity))
                {
                    var specialEnergy = _specialEnergyLookup[entity];
                    var specialEnergyCost = Space4XFocusAbilityDefinitions.GetSpecialEnergyActivationCost(abilityType);
                    if (!ResourcePoolMath.TrySpend(ref specialEnergy.Current, specialEnergyCost))
                    {
                        specialEnergy.FailedSpendAttempts =
                            (ushort)math.min((int)ushort.MaxValue, specialEnergy.FailedSpendAttempts + 1);
                        _specialEnergyLookup[entity] = specialEnergy;
                        ecb.RemoveComponent<FocusAbilityRequest>(entity);
                        continue;
                    }

                    specialEnergy.LastSpent = specialEnergyCost;
                    specialEnergy.LastSpendTick = currentTick;
                    _specialEnergyLookup[entity] = specialEnergy;
                }

                // Calculate effective drain
                float drainRate = Space4XFocusAbilityDefinitions.GetEffectiveDrainRate(abilityType, profile.ValueRO);
                float effectiveness = Space4XFocusAbilityDefinitions.CalculateEffectiveness(profile.ValueRO, focus.ValueRO);

                // Add active ability
                abilitiesBuffer.Add(new Space4XActiveFocusAbility
                {
                    AbilityType = request.ValueRO.RequestedAbility,
                    DrainRate = drainRate,
                    ActivatedTick = currentTick,
                    RemainingDuration = request.ValueRO.RequestedDuration,
                    Effectiveness = (half)effectiveness,
                    TargetEntity = request.ValueRO.TargetEntity
                });

                // Update total drain
                focus.ValueRW.TotalDrainRate += drainRate;

                ecb.RemoveComponent<FocusAbilityRequest>(entity);
            }

            // Process deactivation requests
            foreach (var (request, focus, abilities, entity) in
                SystemAPI.Query<RefRO<FocusAbilityDeactivateRequest>, RefRW<Space4XEntityFocus>, DynamicBuffer<Space4XActiveFocusAbility>>()
                    .WithEntityAccess())
            {
                var abilitiesBuffer = abilities;

                for (int i = abilitiesBuffer.Length - 1; i >= 0; i--)
                {
                    if (abilitiesBuffer[i].AbilityType == request.ValueRO.AbilityToDeactivate)
                    {
                        focus.ValueRW.TotalDrainRate -= abilitiesBuffer[i].DrainRate;
                        abilitiesBuffer.RemoveAt(i);
                        break;
                    }
                }

                ecb.RemoveComponent<FocusAbilityDeactivateRequest>(entity);
            }

            // Process duration-based deactivations
            foreach (var (focus, abilities) in SystemAPI.Query<RefRW<Space4XEntityFocus>, DynamicBuffer<Space4XActiveFocusAbility>>())
            {
                var abilitiesBuffer = abilities;

                for (int i = abilitiesBuffer.Length - 1; i >= 0; i--)
                {
                    var ability = abilitiesBuffer[i];

                    if (ability.RemainingDuration > 0)
                    {
                        ability.RemainingDuration--;
                        abilitiesBuffer[i] = ability;

                        if (ability.RemainingDuration == 0)
                        {
                            focus.ValueRW.TotalDrainRate -= ability.DrainRate;
                            abilitiesBuffer.RemoveAt(i);
                        }
                    }
                }

                // Force deactivate if focus depleted
                if (focus.ValueRO.CurrentFocus <= 0 && abilitiesBuffer.Length > 0)
                {
                    focus.ValueRW.TotalDrainRate = 0;
                    abilitiesBuffer.Clear();
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Calculates Space4XFocusModifiers from active abilities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFocusAbilitySystem))]
    public partial struct Space4XFocusModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, abilities, focus, profile) in
                SystemAPI.Query<RefRW<Space4XFocusModifiers>, DynamicBuffer<Space4XActiveFocusAbility>, RefRO<Space4XEntityFocus>, RefRO<OfficerFocusProfile>>())
            {
                // Reset modifiers to default
                modifiers.ValueRW = Space4XFocusModifiers.Default();

                // Skip if in coma or no abilities
                if (focus.ValueRO.IsInComa != 0 || abilities.Length == 0)
                {
                    continue;
                }

                // Apply each active ability's effects
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ability = abilities[i];
                    var abilityType = (Space4XFocusAbilityType)ability.AbilityType;

                    // Recalculate effectiveness based on current state
                    float effectiveness = (float)ability.Effectiveness;

                    // Apply ability effects
                    var mods = modifiers.ValueRW;
                    Space4XFocusAbilityDefinitions.ApplyAbilityEffect(abilityType, effectiveness, ref mods);
                    modifiers.ValueRW = mods;
                }
            }
        }
    }

    /// <summary>
    /// Handles captain support to officers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFocusModifierSystem))]
    public partial struct Space4XCaptainSupportSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainSupportLink>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Find captains with OfficerSupport active
            foreach (var (link, abilities, modifiers, entity) in
                SystemAPI.Query<RefRW<CaptainSupportLink>, DynamicBuffer<Space4XActiveFocusAbility>, RefRO<Space4XFocusModifiers>>()
                    .WithEntityAccess())
            {
                // Check if OfficerSupport is active
                bool supportActive = false;
                Entity targetOfficer = Entity.Null;

                for (int i = 0; i < abilities.Length; i++)
                {
                    if (abilities[i].AbilityType == (ushort)Space4XFocusAbilityType.OfficerSupport)
                    {
                        supportActive = true;
                        targetOfficer = abilities[i].TargetEntity;
                        break;
                    }
                }

                if (!supportActive || targetOfficer == Entity.Null)
                {
                    // Clear support link
                    if (link.ValueRO.SupportedOfficer != Entity.Null)
                    {
                        if (SystemAPI.HasComponent<ReceivingCaptainSupportTag>(link.ValueRO.SupportedOfficer))
                        {
                            ecb.RemoveComponent<ReceivingCaptainSupportTag>(link.ValueRO.SupportedOfficer);
                        }
                        link.ValueRW.SupportedOfficer = Entity.Null;
                        link.ValueRW.SupportBonus = (half)0f;
                    }
                    continue;
                }

                // Update support link
                if (link.ValueRO.SupportedOfficer != targetOfficer)
                {
                    // Remove old support tag
                    if (link.ValueRO.SupportedOfficer != Entity.Null &&
                        SystemAPI.HasComponent<ReceivingCaptainSupportTag>(link.ValueRO.SupportedOfficer))
                    {
                        ecb.RemoveComponent<ReceivingCaptainSupportTag>(link.ValueRO.SupportedOfficer);
                    }

                    // Add new support tag
                    if (SystemAPI.Exists(targetOfficer))
                    {
                        ecb.AddComponent(targetOfficer, new ReceivingCaptainSupportTag
                        {
                            SupportingCaptain = entity,
                            BonusReceived = modifiers.ValueRO.OfficerSupportBonus
                        });
                    }

                    link.ValueRW.SupportedOfficer = targetOfficer;
                }

                link.ValueRW.SupportBonus = modifiers.ValueRO.OfficerSupportBonus;
            }

            // Apply support bonus to supported officers
            foreach (var (supportTag, modifiers) in SystemAPI.Query<RefRO<ReceivingCaptainSupportTag>, RefRW<Space4XFocusModifiers>>())
            {
                float bonus = (float)supportTag.ValueRO.BonusReceived;

                // Boost all current modifiers
                if ((float)modifiers.ValueRO.DetectionBonus > 0)
                    modifiers.ValueRW.DetectionBonus = (half)((float)modifiers.ValueRO.DetectionBonus * (1f + bonus));
                if ((float)modifiers.ValueRO.AccuracyBonus > 0)
                    modifiers.ValueRW.AccuracyBonus = (half)((float)modifiers.ValueRO.AccuracyBonus * (1f + bonus));
                if ((float)modifiers.ValueRO.RepairSpeedMultiplier > 1f)
                    modifiers.ValueRW.RepairSpeedMultiplier = (half)(1f + ((float)modifiers.ValueRO.RepairSpeedMultiplier - 1f) * (1f + bonus));
                if ((float)modifiers.ValueRO.EvasionBonus > 0)
                    modifiers.ValueRW.EvasionBonus = (half)((float)modifiers.ValueRO.EvasionBonus * (1f + bonus));
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Handles focus coma recovery.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFocusComaRecoverySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FocusComaRecovery>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (recovery, focus, profile, entity) in
                SystemAPI.Query<RefRW<FocusComaRecovery>, RefRW<Space4XEntityFocus>, RefRO<OfficerFocusProfile>>()
                    .WithEntityAccess())
            {
                // Progress recovery
                float ticksElapsed = currentTick - recovery.ValueRO.ComaStartTick;
                float recoveryRate = (1f + (float)profile.ValueRO.MentalResilience) * 0.0001f;

                recovery.ValueRW.RecoveryProgress = (half)math.saturate(ticksElapsed * recoveryRate);

                // Check if recovered
                if ((float)recovery.ValueRO.RecoveryProgress >= 1f && ticksElapsed >= recovery.ValueRO.MinRecoveryDuration)
                {
                    focus.ValueRW.IsInComa = 0;
                    focus.ValueRW.ExhaustionLevel = 50; // Partial exhaustion remains
                    focus.ValueRW.CurrentFocus = focus.ValueRO.MaxFocus * 0.2f; // Start with 20% focus

                    ecb.RemoveComponent<FocusComaRecovery>(entity);
                }
            }

            // Add recovery component to entities that just entered coma
            foreach (var (focus, entity) in SystemAPI.Query<RefRO<Space4XEntityFocus>>()
                .WithNone<FocusComaRecovery>()
                .WithEntityAccess())
            {
                if (focus.ValueRO.IsInComa != 0)
                {
                    ecb.AddComponent(entity, new FocusComaRecovery
                    {
                        ComaStartTick = currentTick,
                        MinRecoveryDuration = 3000, // Minimum recovery time
                        RecoveryProgress = (half)0f
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Updates effectiveness of active abilities based on current state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XFocusModifierSystem))]
    public partial struct Space4XFocusEffectivenessUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XActiveFocusAbility>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (abilities, focus, profile) in
                SystemAPI.Query<DynamicBuffer<Space4XActiveFocusAbility>, RefRO<Space4XEntityFocus>, RefRO<OfficerFocusProfile>>())
            {
                var abilitiesBuffer = abilities;
                float currentEffectiveness = Space4XFocusAbilityDefinitions.CalculateEffectiveness(profile.ValueRO, focus.ValueRO);

                for (int i = 0; i < abilitiesBuffer.Length; i++)
                {
                    var ability = abilitiesBuffer[i];
                    ability.Effectiveness = (half)currentEffectiveness;
                    abilitiesBuffer[i] = ability;
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for focus system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XFocusTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEntityFocus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalOfficers = 0;
            int activeAbilities = 0;
            int exhausted = 0;
            int inComa = 0;
            float avgFocusRatio = 0f;

            foreach (var (focus, abilities) in SystemAPI.Query<RefRO<Space4XEntityFocus>, DynamicBuffer<Space4XActiveFocusAbility>>())
            {
                totalOfficers++;
                activeAbilities += abilities.Length;
                avgFocusRatio += focus.ValueRO.Ratio;

                if (focus.ValueRO.IsExhausted)
                    exhausted++;
                if (focus.ValueRO.IsInComa != 0)
                    inComa++;
            }

            if (totalOfficers > 0)
            {
                avgFocusRatio /= totalOfficers;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Focus] Officers: {totalOfficers}, ActiveAbilities: {activeAbilities}, AvgFocus: {avgFocusRatio:P0}, Exhausted: {exhausted}, InComa: {inComa}");
        }
    }
}
