using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Static helpers for integrating focus modifiers into other systems.
    /// </summary>
    public static class Space4XFocusIntegration
    {
        /// <summary>
        /// Applies weapons focus modifiers to combat stats.
        /// </summary>
        public static void ApplyWeaponsModifiers(
            in Space4XFocusModifiers modifiers,
            ref float accuracy,
            ref float rateOfFire,
            ref float coolingRate)
        {
            accuracy += (float)modifiers.AccuracyBonus;
            rateOfFire *= (float)modifiers.RateOfFireMultiplier;
            coolingRate *= (float)modifiers.CoolingEfficiency;
        }

        /// <summary>
        /// Applies sensors focus modifiers to detection.
        /// </summary>
        public static void ApplySensorsModifiers(
            in Space4XFocusModifiers modifiers,
            ref float detectionRange,
            ref float cloakDetection,
            ref int maxTrackedTargets)
        {
            detectionRange *= (float)modifiers.ScanRangeMultiplier;
            cloakDetection += (float)modifiers.DetectionBonus;
            maxTrackedTargets += (int)(float)modifiers.TrackingCapacityBonus;
        }

        /// <summary>
        /// Applies engineering focus modifiers to repairs.
        /// </summary>
        public static void ApplyEngineeringModifiers(
            in Space4XFocusModifiers modifiers,
            ref float repairSpeed,
            ref float systemEfficiency,
            ref float damageControlRate)
        {
            repairSpeed *= (float)modifiers.RepairSpeedMultiplier;
            systemEfficiency += (float)modifiers.SystemEfficiencyBonus;
            damageControlRate += (float)modifiers.DamageControlBonus;
        }

        /// <summary>
        /// Applies tactical focus modifiers to movement.
        /// </summary>
        public static void ApplyTacticalModifiers(
            in Space4XFocusModifiers modifiers,
            ref float evasion,
            ref float formationCohesion)
        {
            evasion += (float)modifiers.EvasionBonus;
            formationCohesion += (float)modifiers.FormationCohesionBonus;
        }

        /// <summary>
        /// Applies operations focus modifiers to production.
        /// </summary>
        public static void ApplyOperationsModifiers(
            in Space4XFocusModifiers modifiers,
            ref float productionSpeed,
            ref float productionQuality,
            ref float wasteReduction,
            ref int batchSize)
        {
            productionSpeed *= (float)modifiers.ProductionSpeedMultiplier;
            productionQuality *= (float)modifiers.ProductionQualityMultiplier;
            wasteReduction += (float)modifiers.ResourceEfficiencyBonus;
            batchSize += modifiers.BatchCapacityBonus;
        }

        /// <summary>
        /// Gets multi-target count bonus for missiles.
        /// </summary>
        public static int GetMultiTargetBonus(in Space4XFocusModifiers modifiers)
        {
            return modifiers.MultiTargetCount;
        }

        /// <summary>
        /// Gets subsystem targeting bonus.
        /// </summary>
        public static float GetSubsystemTargetingBonus(in Space4XFocusModifiers modifiers)
        {
            return (float)modifiers.SubsystemTargetingBonus;
        }

        /// <summary>
        /// Gets shield adaptation rate for modulation.
        /// </summary>
        public static float GetShieldAdaptationRate(in Space4XFocusModifiers modifiers)
        {
            return (float)modifiers.ShieldAdaptationRate;
        }

        /// <summary>
        /// Gets ECM resistance bonus.
        /// </summary>
        public static float GetECMResistance(in Space4XFocusModifiers modifiers)
        {
            return (float)modifiers.ECMResistance;
        }

        /// <summary>
        /// Gets strike craft coordination bonus.
        /// </summary>
        public static float GetStrikeCraftCoordinationBonus(in Space4XFocusModifiers modifiers)
        {
            return (float)modifiers.StrikeCraftCoordinationBonus;
        }

        /// <summary>
        /// Gets boarding effectiveness bonus.
        /// </summary>
        public static float GetBoardingEffectivenessBonus(in Space4XFocusModifiers modifiers)
        {
            return (float)modifiers.BoardingEffectivenessBonus;
        }

        /// <summary>
        /// Gets morale bonus for crew.
        /// </summary>
        public static float GetMoraleBonus(in Space4XFocusModifiers modifiers)
        {
            return (float)modifiers.MoraleBonus;
        }

        /// <summary>
        /// Gets crew stress reduction.
        /// </summary>
        public static float GetCrewStressReduction(in Space4XFocusModifiers modifiers)
        {
            return (float)modifiers.CrewStressReduction;
        }
    }

    /// <summary>
    /// Applies focus modifiers to combat damage resolution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XWeaponSystem))]
    public partial struct Space4XFocusCombatIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<WeaponMount>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Apply weapons officer focus to weapon mounts
            foreach (var (modifiers, weapons, engagement) in
                SystemAPI.Query<RefRO<Space4XFocusModifiers>, DynamicBuffer<WeaponMount>, RefRW<Space4XEngagement>>())
            {
                var weaponBuffer = weapons;

                // Apply accuracy and rate of fire bonuses from focus
                float accuracyBonus = (float)modifiers.ValueRO.AccuracyBonus;
                float rofMultiplier = (float)modifiers.ValueRO.RateOfFireMultiplier;
                float coolingMult = (float)modifiers.ValueRO.CoolingEfficiency;

                for (int i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];

                    // Apply cooling efficiency to cooldown
                    if (mount.Weapon.CurrentCooldown > 0 && coolingMult > 1f)
                    {
                        // Faster cooling = shorter remaining cooldown
                        ushort reduction = (ushort)((coolingMult - 1f) * 2);
                        mount.Weapon.CurrentCooldown = (ushort)math.max(0, mount.Weapon.CurrentCooldown - reduction);
                        weaponBuffer[i] = mount;
                    }
                }

                // Apply evasion bonus from tactical focus
                engagement.ValueRW.EvasionModifier = (half)((float)engagement.ValueRO.EvasionModifier + (float)modifiers.ValueRO.EvasionBonus);
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to target priority calculations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XTargetPrioritySystem))]
    public partial struct Space4XFocusTargetingIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<TargetSelectionProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, profile) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<TargetSelectionProfile>>())
            {
                // Tracking bonuses are consumed by downstream targeting systems; no direct profile field to adjust here.
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to shield regeneration.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XShieldRegenSystem))]
    public partial struct Space4XFocusShieldIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<Space4XShield>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, shield) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<Space4XShield>>())
            {
                float adaptationRate = (float)modifiers.ValueRO.ShieldAdaptationRate;

                if (adaptationRate > 0)
                {
                    // Adaptive shields learn from incoming damage
                    // This would normally track recent damage types and boost resistance
                    // Simplified: boost recharge rate when modulating
                    shield.ValueRW.RechargeRate = shield.ValueRO.RechargeRate * (1f + adaptationRate * 0.5f);
                }
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to formation cohesion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XFormationSystem))]
    public partial struct Space4XFocusFormationIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<FormationAssignment>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, assignment) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<FormationAssignment>>())
            {
                float cohesionBonus = (float)modifiers.ValueRO.FormationCohesionBonus;

                if (cohesionBonus > 0)
                {
                    // Boost formation tightness (closest available proxy for cohesion)
                    assignment.ValueRW.FormationTightness = (half)math.clamp(
                        (float)assignment.ValueRO.FormationTightness + cohesionBonus,
                        0f,
                        1f);
                }
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to strike craft coordination.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XStrikeCraftSystem))]
    public partial struct Space4XFocusStrikeCraftIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<StrikeCraftProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, strikeCraft) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<StrikeCraftProfile>>())
            {
                float coordinationBonus = (float)modifiers.ValueRO.StrikeCraftCoordinationBonus;

                if (coordinationBonus > 0)
                {
                    // Better coordination means tighter attack runs
                    // Would reduce scatter and improve timing
                }
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to morale updates.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XMoraleSystem))]
    public partial struct Space4XFocusMoraleIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<MoraleState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, morale) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<MoraleState>>())
            {
                float moraleBonus = (float)modifiers.ValueRO.MoraleBonus;

                if (moraleBonus > 0)
                {
                    // Captain's inspiring presence boosts morale
                    morale.ValueRW.Current = (half)math.min(1f, (float)morale.ValueRO.Current + moraleBonus * 0.01f);
                }
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to department stress.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XDepartmentSystem))]
    public partial struct Space4XFocusDepartmentIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<DepartmentStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, stats) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<DepartmentStats>>())
            {
                float stressReduction = (float)modifiers.ValueRO.CrewStressReduction;

                if (stressReduction > 0)
                {
                    // Crisis management reduces stress accumulation
                    stats.ValueRW.Stress = (half)math.max(0, (float)stats.ValueRO.Stress - stressReduction * 0.01f);
                }
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to processing facilities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFocusProductionIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<ProcessingFacility>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (modifiers, facility) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<ProcessingFacility>>())
            {
                float speedMult = math.max(1f, (float)modifiers.ValueRO.ProductionSpeedMultiplier);
                float efficiencyBonus = math.max(0f, (float)modifiers.ValueRO.ResourceEfficiencyBonus);

                float baseSpeed = facility.ValueRO.Tier switch
                {
                    1 => (float)ProcessingFacility.Tier1.SpeedMultiplier,
                    2 => (float)ProcessingFacility.Tier2.SpeedMultiplier,
                    3 => (float)ProcessingFacility.Tier3.SpeedMultiplier,
                    _ => (float)facility.ValueRO.SpeedMultiplier
                };

                float baseEfficiency = facility.ValueRO.Tier switch
                {
                    1 => (float)ProcessingFacility.Tier1.EnergyEfficiency,
                    2 => (float)ProcessingFacility.Tier2.EnergyEfficiency,
                    3 => (float)ProcessingFacility.Tier3.EnergyEfficiency,
                    _ => (float)facility.ValueRO.EnergyEfficiency
                };

                facility.ValueRW.SpeedMultiplier = (half)(baseSpeed * speedMult);
                facility.ValueRW.EnergyEfficiency = (half)math.max(0f, baseEfficiency * (1f + efficiencyBonus));
            }
        }
    }

    /// <summary>
    /// Applies focus modifiers to hull repair.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XFieldRepairHullSystem))]
    public partial struct Space4XFocusRepairIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFocusModifiers>();
            state.RequireForUpdate<HullIntegrity>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (modifiers, hull) in SystemAPI.Query<RefRO<Space4XFocusModifiers>, RefRW<HullIntegrity>>())
            {
                float repairSpeedMult = (float)modifiers.ValueRO.RepairSpeedMultiplier;

                if (repairSpeedMult > 1f)
                {
                    // Apply repair speed bonus
                    float repairAmount = (repairSpeedMult - 1f) * 0.1f; // Bonus repair
                    float repairedHull = math.min(hull.ValueRO.Max, hull.ValueRO.Current + repairAmount);

                    hull.ValueRW.Current = repairedHull;
                    hull.ValueRW.LastRepairTick = currentTick;
                }
            }
        }
    }
}

