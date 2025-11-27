using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Space4X focus ability types.
    /// Values start at 200 to avoid collision with PureDOTS base abilities.
    /// </summary>
    public enum Space4XFocusAbilityType : ushort
    {
        None = 0,

        // === Command Abilities (200-219) ===

        /// <summary>
        /// Boost one officer's ability effectiveness.
        /// </summary>
        OfficerSupport = 200,

        /// <summary>
        /// Real-time boarding party direction.
        /// </summary>
        BoardingCoordination = 201,

        /// <summary>
        /// Sync focus effects with fleet vessels.
        /// </summary>
        FleetCoordination = 202,

        /// <summary>
        /// Reduce crew stress during situations.
        /// </summary>
        CrisisManagement = 203,

        /// <summary>
        /// Passive morale boost to bridge crew.
        /// </summary>
        InspiringPresence = 204,

        /// <summary>
        /// Override automation for manual control.
        /// </summary>
        DirectCommand = 205,

        /// <summary>
        /// Coordinate damage control teams.
        /// </summary>
        DamageControlCommand = 206,

        // === Sensors Abilities (220-239) ===

        /// <summary>
        /// Detect cloaked enemies more accurately.
        /// </summary>
        CloakPenetration = 220,

        /// <summary>
        /// Track more enemy targets simultaneously.
        /// </summary>
        FleetTracking = 221,

        /// <summary>
        /// Identify unknown contacts faster.
        /// </summary>
        SignatureAnalysis = 222,

        /// <summary>
        /// Extend sensor range temporarily.
        /// </summary>
        LongRangeScan = 223,

        /// <summary>
        /// Resist enemy jamming.
        /// </summary>
        ECMCountermeasures = 224,

        /// <summary>
        /// Predict enemy movement patterns.
        /// </summary>
        PredictiveTracking = 225,

        /// <summary>
        /// Detect stealth missiles/torpedoes.
        /// </summary>
        ThreatDetection = 226,

        /// <summary>
        /// Analyze enemy shield frequencies.
        /// </summary>
        ShieldAnalysis = 227,

        // === Weapons Abilities (240-259) ===

        /// <summary>
        /// Increase weapon cooling efficiency.
        /// </summary>
        CoolingOverdrive = 240,

        /// <summary>
        /// Track additional targets for missiles.
        /// </summary>
        MultiTargetLock = 241,

        /// <summary>
        /// Boost accuracy at cost of rate of fire.
        /// </summary>
        PrecisionFire = 242,

        /// <summary>
        /// Boost rate of fire at cost of accuracy.
        /// </summary>
        RapidFire = 243,

        /// <summary>
        /// Target specific enemy subsystems.
        /// </summary>
        SubsystemTargeting = 244,

        /// <summary>
        /// Coordinate point defense network.
        /// </summary>
        PointDefenseNetwork = 245,

        /// <summary>
        /// Synchronize broadside timing.
        /// </summary>
        SynchronizedFire = 246,

        /// <summary>
        /// Overcharge weapons for burst damage.
        /// </summary>
        WeaponOvercharge = 247,

        // === Engineering Abilities (260-279) ===

        /// <summary>
        /// Faster repairs during combat.
        /// </summary>
        EmergencyRepairs = 260,

        /// <summary>
        /// Boost one system by draining another.
        /// </summary>
        PowerReroute = 261,

        /// <summary>
        /// Reduce fire/leak spread rate.
        /// </summary>
        DamageControl = 262,

        /// <summary>
        /// Temporary efficiency boost.
        /// </summary>
        SystemOptimization = 263,

        /// <summary>
        /// Adapt shields to incoming damage type.
        /// </summary>
        ShieldModulation = 264,

        /// <summary>
        /// Emergency reactor output boost.
        /// </summary>
        ReactorOverdrive = 265,

        /// <summary>
        /// Bypass damaged systems temporarily.
        /// </summary>
        SystemBypass = 266,

        /// <summary>
        /// Prioritize critical system repairs.
        /// </summary>
        CriticalRepairs = 267,

        // === Tactical Abilities (280-299) ===

        /// <summary>
        /// Boost ship evasion.
        /// </summary>
        EvasiveManeuvers = 280,

        /// <summary>
        /// Coordinate strike craft timing.
        /// </summary>
        AttackRunCoordination = 281,

        /// <summary>
        /// Maintain formation under fire.
        /// </summary>
        FormationHold = 282,

        /// <summary>
        /// Find weak points in enemy formation.
        /// </summary>
        BreakthroughVector = 283,

        /// <summary>
        /// Cover allied vessels during withdrawal.
        /// </summary>
        RetreatCover = 284,

        /// <summary>
        /// Predict optimal intercept course.
        /// </summary>
        InterceptCalculation = 285,

        /// <summary>
        /// Ram speed approach maneuver.
        /// </summary>
        AggressiveApproach = 286,

        /// <summary>
        /// Maintain optimal weapon range.
        /// </summary>
        RangeManagement = 287,

        // === Operations Abilities (300-319) ===

        /// <summary>
        /// Faster facility output.
        /// </summary>
        ProductionSurge = 300,

        /// <summary>
        /// Higher quality at slower speed.
        /// </summary>
        QualityFocus = 301,

        /// <summary>
        /// Reduce waste in processing.
        /// </summary>
        ResourceEfficiency = 302,

        /// <summary>
        /// Process multiple jobs simultaneously.
        /// </summary>
        BatchProcessing = 303,

        /// <summary>
        /// Accelerate scheduled maintenance.
        /// </summary>
        MaintenanceBlitz = 304,

        /// <summary>
        /// Rush critical production orders.
        /// </summary>
        PriorityProduction = 305,

        /// <summary>
        /// Optimize supply chain flow.
        /// </summary>
        LogisticsOptimization = 306,

        /// <summary>
        /// Extend equipment operational life.
        /// </summary>
        PreventiveMaintenance = 307,

        // === Medical Abilities (320-339) ===

        /// <summary>
        /// Intensive care for critical patients.
        /// </summary>
        IntensiveCare = 320,

        /// <summary>
        /// Treat multiple patients simultaneously.
        /// </summary>
        MassTriage = 321,

        /// <summary>
        /// Accelerate recovery time.
        /// </summary>
        AcceleratedHealing = 322,

        /// <summary>
        /// Stabilize critical injuries.
        /// </summary>
        EmergencyStabilization = 323,

        // === Communications Abilities (340-359) ===

        /// <summary>
        /// Break enemy communications encryption.
        /// </summary>
        SignalDecryption = 340,

        /// <summary>
        /// Coordinate multi-ship actions.
        /// </summary>
        FleetCommunications = 341,

        /// <summary>
        /// Resist enemy communications jamming.
        /// </summary>
        SignalFiltering = 342,

        /// <summary>
        /// Negotiate under pressure.
        /// </summary>
        CrisisNegotiation = 343
    }

    /// <summary>
    /// Static definitions for Space4X focus abilities.
    /// </summary>
    public static class Space4XFocusAbilityDefinitions
    {
        /// <summary>
        /// Gets the focus drain rate per tick for an ability.
        /// </summary>
        public static float GetDrainRate(Space4XFocusAbilityType ability)
        {
            return ability switch
            {
                // Command - moderate drain (leadership is taxing)
                Space4XFocusAbilityType.OfficerSupport => 0.15f,
                Space4XFocusAbilityType.BoardingCoordination => 0.25f,
                Space4XFocusAbilityType.FleetCoordination => 0.3f,
                Space4XFocusAbilityType.CrisisManagement => 0.2f,
                Space4XFocusAbilityType.InspiringPresence => 0.1f,
                Space4XFocusAbilityType.DirectCommand => 0.2f,
                Space4XFocusAbilityType.DamageControlCommand => 0.25f,

                // Sensors - low-moderate drain (concentration-based)
                Space4XFocusAbilityType.CloakPenetration => 0.2f,
                Space4XFocusAbilityType.FleetTracking => 0.15f,
                Space4XFocusAbilityType.SignatureAnalysis => 0.1f,
                Space4XFocusAbilityType.LongRangeScan => 0.18f,
                Space4XFocusAbilityType.ECMCountermeasures => 0.22f,
                Space4XFocusAbilityType.PredictiveTracking => 0.25f,
                Space4XFocusAbilityType.ThreatDetection => 0.12f,
                Space4XFocusAbilityType.ShieldAnalysis => 0.15f,

                // Weapons - moderate-high drain (high stress)
                Space4XFocusAbilityType.CoolingOverdrive => 0.18f,
                Space4XFocusAbilityType.MultiTargetLock => 0.25f,
                Space4XFocusAbilityType.PrecisionFire => 0.2f,
                Space4XFocusAbilityType.RapidFire => 0.22f,
                Space4XFocusAbilityType.SubsystemTargeting => 0.28f,
                Space4XFocusAbilityType.PointDefenseNetwork => 0.2f,
                Space4XFocusAbilityType.SynchronizedFire => 0.15f,
                Space4XFocusAbilityType.WeaponOvercharge => 0.35f,

                // Engineering - moderate drain (technical focus)
                Space4XFocusAbilityType.EmergencyRepairs => 0.22f,
                Space4XFocusAbilityType.PowerReroute => 0.18f,
                Space4XFocusAbilityType.DamageControl => 0.2f,
                Space4XFocusAbilityType.SystemOptimization => 0.15f,
                Space4XFocusAbilityType.ShieldModulation => 0.25f,
                Space4XFocusAbilityType.ReactorOverdrive => 0.3f,
                Space4XFocusAbilityType.SystemBypass => 0.2f,
                Space4XFocusAbilityType.CriticalRepairs => 0.28f,

                // Tactical - high drain (split-second decisions)
                Space4XFocusAbilityType.EvasiveManeuvers => 0.25f,
                Space4XFocusAbilityType.AttackRunCoordination => 0.22f,
                Space4XFocusAbilityType.FormationHold => 0.15f,
                Space4XFocusAbilityType.BreakthroughVector => 0.28f,
                Space4XFocusAbilityType.RetreatCover => 0.2f,
                Space4XFocusAbilityType.InterceptCalculation => 0.18f,
                Space4XFocusAbilityType.AggressiveApproach => 0.25f,
                Space4XFocusAbilityType.RangeManagement => 0.12f,

                // Operations - low drain (sustained focus)
                Space4XFocusAbilityType.ProductionSurge => 0.15f,
                Space4XFocusAbilityType.QualityFocus => 0.12f,
                Space4XFocusAbilityType.ResourceEfficiency => 0.1f,
                Space4XFocusAbilityType.BatchProcessing => 0.18f,
                Space4XFocusAbilityType.MaintenanceBlitz => 0.2f,
                Space4XFocusAbilityType.PriorityProduction => 0.22f,
                Space4XFocusAbilityType.LogisticsOptimization => 0.12f,
                Space4XFocusAbilityType.PreventiveMaintenance => 0.08f,

                // Medical - moderate drain
                Space4XFocusAbilityType.IntensiveCare => 0.25f,
                Space4XFocusAbilityType.MassTriage => 0.2f,
                Space4XFocusAbilityType.AcceleratedHealing => 0.18f,
                Space4XFocusAbilityType.EmergencyStabilization => 0.3f,

                // Communications - low-moderate drain
                Space4XFocusAbilityType.SignalDecryption => 0.2f,
                Space4XFocusAbilityType.FleetCommunications => 0.12f,
                Space4XFocusAbilityType.SignalFiltering => 0.15f,
                Space4XFocusAbilityType.CrisisNegotiation => 0.25f,

                _ => 0.1f
            };
        }

        /// <summary>
        /// Gets the archetype for an ability.
        /// </summary>
        public static Space4XFocusArchetype GetArchetype(Space4XFocusAbilityType ability)
        {
            int id = (int)ability;

            if (id >= 200 && id < 220) return Space4XFocusArchetype.Command;
            if (id >= 220 && id < 240) return Space4XFocusArchetype.Sensors;
            if (id >= 240 && id < 260) return Space4XFocusArchetype.Weapons;
            if (id >= 260 && id < 280) return Space4XFocusArchetype.Engineering;
            if (id >= 280 && id < 300) return Space4XFocusArchetype.Tactical;
            if (id >= 300 && id < 320) return Space4XFocusArchetype.Operations;
            if (id >= 320 && id < 340) return Space4XFocusArchetype.Medical;
            if (id >= 340 && id < 360) return Space4XFocusArchetype.Communications;

            return Space4XFocusArchetype.None;
        }

        /// <summary>
        /// Gets the minimum focus required to activate an ability.
        /// </summary>
        public static float GetMinimumFocus(Space4XFocusAbilityType ability)
        {
            float drainRate = GetDrainRate(ability);
            // Need at least 10 ticks worth of drain + 10% buffer
            return drainRate * 10f * 1.1f;
        }

        /// <summary>
        /// Gets whether ability requires a target.
        /// </summary>
        public static bool RequiresTarget(Space4XFocusAbilityType ability)
        {
            return ability switch
            {
                Space4XFocusAbilityType.OfficerSupport => true,
                Space4XFocusAbilityType.BoardingCoordination => true,
                Space4XFocusAbilityType.PowerReroute => true,
                Space4XFocusAbilityType.IntensiveCare => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets experience gain rate for using an ability.
        /// </summary>
        public static float GetExperienceGain(Space4XFocusAbilityType ability)
        {
            float drainRate = GetDrainRate(ability);
            // Higher drain = more experience
            return drainRate * 0.01f;
        }

        /// <summary>
        /// Applies ability effects to modifiers.
        /// </summary>
        public static void ApplyAbilityEffect(
            Space4XFocusAbilityType ability,
            float effectiveness,
            ref Space4XFocusModifiers modifiers)
        {
            float eff = effectiveness;

            switch (ability)
            {
                // Command
                case Space4XFocusAbilityType.OfficerSupport:
                    modifiers.OfficerSupportBonus = (half)(0.3f * eff);
                    break;
                case Space4XFocusAbilityType.BoardingCoordination:
                    modifiers.BoardingEffectivenessBonus = (half)(0.4f * eff);
                    break;
                case Space4XFocusAbilityType.CrisisManagement:
                    modifiers.CrewStressReduction = (half)(0.25f * eff);
                    break;
                case Space4XFocusAbilityType.InspiringPresence:
                    modifiers.MoraleBonus = (half)(0.15f * eff);
                    break;

                // Sensors
                case Space4XFocusAbilityType.CloakPenetration:
                    modifiers.DetectionBonus = (half)(0.4f * eff);
                    break;
                case Space4XFocusAbilityType.FleetTracking:
                    modifiers.TrackingCapacityBonus = (half)(3f * eff);
                    break;
                case Space4XFocusAbilityType.LongRangeScan:
                    modifiers.ScanRangeMultiplier = (half)(1f + 0.5f * eff);
                    break;
                case Space4XFocusAbilityType.ECMCountermeasures:
                    modifiers.ECMResistance = (half)(0.5f * eff);
                    break;

                // Weapons
                case Space4XFocusAbilityType.CoolingOverdrive:
                    modifiers.CoolingEfficiency = (half)(1f + 0.4f * eff);
                    break;
                case Space4XFocusAbilityType.MultiTargetLock:
                    modifiers.MultiTargetCount = (byte)(2 * eff);
                    break;
                case Space4XFocusAbilityType.PrecisionFire:
                    modifiers.AccuracyBonus = (half)(0.25f * eff);
                    modifiers.RateOfFireMultiplier = (half)(1f - 0.2f * eff);
                    break;
                case Space4XFocusAbilityType.RapidFire:
                    modifiers.RateOfFireMultiplier = (half)(1f + 0.3f * eff);
                    modifiers.AccuracyBonus = (half)(-0.15f * eff);
                    break;
                case Space4XFocusAbilityType.SubsystemTargeting:
                    modifiers.SubsystemTargetingBonus = (half)(0.35f * eff);
                    break;

                // Engineering
                case Space4XFocusAbilityType.EmergencyRepairs:
                    modifiers.RepairSpeedMultiplier = (half)(1f + 0.5f * eff);
                    break;
                case Space4XFocusAbilityType.DamageControl:
                    modifiers.DamageControlBonus = (half)(0.4f * eff);
                    break;
                case Space4XFocusAbilityType.SystemOptimization:
                    modifiers.SystemEfficiencyBonus = (half)(0.2f * eff);
                    break;
                case Space4XFocusAbilityType.ShieldModulation:
                    modifiers.ShieldAdaptationRate = (half)(0.3f * eff);
                    break;

                // Tactical
                case Space4XFocusAbilityType.EvasiveManeuvers:
                    modifiers.EvasionBonus = (half)(0.3f * eff);
                    break;
                case Space4XFocusAbilityType.AttackRunCoordination:
                    modifiers.StrikeCraftCoordinationBonus = (half)(0.35f * eff);
                    break;
                case Space4XFocusAbilityType.FormationHold:
                    modifiers.FormationCohesionBonus = (half)(0.25f * eff);
                    break;

                // Operations
                case Space4XFocusAbilityType.ProductionSurge:
                    modifiers.ProductionSpeedMultiplier = (half)(1f + 0.4f * eff);
                    modifiers.ProductionQualityMultiplier = (half)(1f - 0.1f * eff);
                    break;
                case Space4XFocusAbilityType.QualityFocus:
                    modifiers.ProductionQualityMultiplier = (half)(1f + 0.3f * eff);
                    modifiers.ProductionSpeedMultiplier = (half)(1f - 0.2f * eff);
                    break;
                case Space4XFocusAbilityType.ResourceEfficiency:
                    modifiers.ResourceEfficiencyBonus = (half)(0.25f * eff);
                    break;
                case Space4XFocusAbilityType.BatchProcessing:
                    modifiers.BatchCapacityBonus = (byte)(2 * eff);
                    break;
            }
        }

        /// <summary>
        /// Checks if ability can be activated given current state.
        /// </summary>
        public static bool CanActivate(
            Space4XFocusAbilityType ability,
            in Space4XEntityFocus focus,
            in OfficerFocusProfile profile)
        {
            if (!focus.CanActivateAbility)
            {
                return false;
            }

            float minFocus = GetMinimumFocus(ability);
            if (focus.CurrentFocus < minFocus)
            {
                return false;
            }

            // Check archetype compatibility
            var abilityArchetype = GetArchetype(ability);
            if (abilityArchetype != profile.PrimaryArchetype &&
                abilityArchetype != profile.SecondaryArchetype)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates effective drain rate considering profile affinity.
        /// </summary>
        public static float GetEffectiveDrainRate(
            Space4XFocusAbilityType ability,
            in OfficerFocusProfile profile)
        {
            float baseDrain = GetDrainRate(ability);
            var archetype = GetArchetype(ability);

            // Primary archetype reduces drain
            if (archetype == profile.PrimaryArchetype)
            {
                baseDrain *= (1f - (float)profile.ArchetypeAffinity);
            }

            return baseDrain;
        }

        /// <summary>
        /// Calculates ability effectiveness based on experience and exhaustion.
        /// </summary>
        public static float CalculateEffectiveness(
            in OfficerFocusProfile profile,
            in Space4XEntityFocus focus)
        {
            float baseEffectiveness = 0.5f + (float)profile.FocusExperience * 0.5f;

            // Exhaustion penalty
            float exhaustionPenalty = focus.ExhaustionLevel * 0.005f;
            baseEffectiveness -= exhaustionPenalty;

            // Low focus penalty
            if (focus.Ratio < 0.3f)
            {
                baseEffectiveness *= focus.Ratio / 0.3f;
            }

            return math.saturate(baseEffectiveness);
        }
    }
}

