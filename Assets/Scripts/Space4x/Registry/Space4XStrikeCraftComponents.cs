using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Strike craft role determining behavior and loadout.
    /// </summary>
    public enum StrikeCraftRole : byte
    {
        /// <summary>
        /// General purpose fighter.
        /// </summary>
        Fighter = 0,

        /// <summary>
        /// Anti-fighter and missile interception.
        /// </summary>
        Interceptor = 1,

        /// <summary>
        /// Anti-capital ship torpedo/bomb delivery.
        /// </summary>
        Bomber = 2,

        /// <summary>
        /// Extended sensor range, target painting.
        /// </summary>
        Recon = 3,

        /// <summary>
        /// Area denial and point defense suppression.
        /// </summary>
        Suppression = 4,

        /// <summary>
        /// Electronic warfare and jamming.
        /// </summary>
        EWar = 5
    }

    /// <summary>
    /// Current phase of an attack run.
    /// </summary>
    public enum AttackRunPhase : byte
    {
        /// <summary>
        /// Idle in carrier bay.
        /// </summary>
        Docked = 0,

        /// <summary>
        /// Launching from carrier.
        /// </summary>
        Launching = 1,

        /// <summary>
        /// Forming up with wing before attack.
        /// </summary>
        FormUp = 2,

        /// <summary>
        /// Approaching target, evaluating defenses.
        /// </summary>
        Approach = 3,

        /// <summary>
        /// Executing attack (strafing, torpedo drop, etc).
        /// </summary>
        Execute = 4,

        /// <summary>
        /// Breaking off from target.
        /// </summary>
        Disengage = 5,

        /// <summary>
        /// Returning to carrier.
        /// </summary>
        Return = 6,

        /// <summary>
        /// Landing on carrier.
        /// </summary>
        Landing = 7,

        /// <summary>
        /// Orbiting and defending carrier.
        /// </summary>
        CombatAirPatrol = 8
    }

    /// <summary>
    /// Weapon delivery type for attack execution.
    /// </summary>
    public enum WeaponDeliveryType : byte
    {
        /// <summary>
        /// Gun strafing pass.
        /// </summary>
        Strafe = 0,

        /// <summary>
        /// Torpedo launch at range.
        /// </summary>
        TorpedoStandoff = 1,

        /// <summary>
        /// Close-range torpedo drop.
        /// </summary>
        TorpedoDrop = 2,

        /// <summary>
        /// Bombing run.
        /// </summary>
        Bomb = 3,

        /// <summary>
        /// Missile salvo.
        /// </summary>
        MissileSalvo = 4,

        /// <summary>
        /// Suppression fire to distract PD.
        /// </summary>
        Suppression = 5
    }

    /// <summary>
    /// Attack run profile and tactical state for a strike craft.
    /// </summary>
    public struct StrikeCraftProfile : IComponentData
    {
        /// <summary>
        /// Current phase of the attack run.
        /// </summary>
        public AttackRunPhase Phase;

        /// <summary>
        /// Role of this strike craft.
        /// </summary>
        public StrikeCraftRole Role;

        /// <summary>
        /// Current target entity.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Parent carrier entity.
        /// </summary>
        public Entity Carrier;

        /// <summary>
        /// Wing leader (if not self).
        /// </summary>
        public Entity WingLeader;

        /// <summary>
        /// Position in wing formation (0 = leader).
        /// </summary>
        public byte WingPosition;

        /// <summary>
        /// Ticks remaining in current phase.
        /// </summary>
        public ushort PhaseTimer;

        /// <summary>
        /// Number of attack passes completed.
        /// </summary>
        public byte PassCount;

        /// <summary>
        /// Whether weapons have been expended.
        /// </summary>
        public byte WeaponsExpended;

        public static StrikeCraftProfile Create(StrikeCraftRole role, Entity carrier)
        {
            return new StrikeCraftProfile
            {
                Phase = AttackRunPhase.Docked,
                Role = role,
                Target = Entity.Null,
                Carrier = carrier,
                WingLeader = Entity.Null,
                WingPosition = 0,
                PhaseTimer = 0,
                PassCount = 0,
                WeaponsExpended = 0
            };
        }
    }

    /// <summary>
    /// Wing-level directive issued by a wing leader to either regroup or break formation.
    /// </summary>
    public struct StrikeCraftWingDirective : IComponentData
    {
        /// <summary>
        /// 0 = FormUp, 1 = Break.
        /// </summary>
        public byte Mode;

        /// <summary>
        /// Next tick when the wing leader may re-evaluate the directive.
        /// </summary>
        public uint NextDecisionTick;

        /// <summary>
        /// Last tick when the directive changed.
        /// </summary>
        public uint LastDecisionTick;
    }

    /// <summary>
    /// Tracks the last wing directive received and whether it was obeyed.
    /// </summary>
    public struct StrikeCraftOrderDecision : IComponentData
    {
        public uint LastDirectiveTick;
        public byte LastDirectiveMode;
        /// <summary>
        /// 0 = None, 1 = Obey, 2 = Disobey.
        /// </summary>
        public byte LastDecision;
        public uint LastEmittedTick;
    }

    /// <summary>
    /// Links a strike craft to its pilot entity (individual profile owner).
    /// </summary>
    public struct StrikeCraftPilotLink : IComponentData
    {
        public Entity Pilot;
    }

    /// <summary>
    /// Quality of hangar/technical work applied to this craft [0, 1].
    /// </summary>
    public struct StrikeCraftMaintenanceQuality : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Kinematic state used for accel-limited steering and intercept guidance.
    /// </summary>
    public struct StrikeCraftKinematics : IComponentData
    {
        public float3 Velocity;
    }

    /// <summary>
    /// Default profile values for spawned strike craft pilots.
    /// </summary>
    public struct StrikeCraftPilotProfileConfig : IComponentData
    {
        public OutlookId FriendlyOutlook;
        public OutlookId HostileOutlook;
        public OutlookId NeutralOutlook;
        public float LoyalistLawThreshold;
        public float MutinousLawThreshold;

        public static StrikeCraftPilotProfileConfig Default => new StrikeCraftPilotProfileConfig
        {
            FriendlyOutlook = OutlookId.Loyalist,
            HostileOutlook = OutlookId.Mutinous,
            NeutralOutlook = OutlookId.Neutral,
            LoyalistLawThreshold = 0.55f,
            MutinousLawThreshold = -0.55f
        };
    }

    /// <summary>
    /// Tunable thresholds for wing regroup/break decisions.
    /// </summary>
    public struct StrikeCraftWingDecisionConfig : IComponentData
    {
        public uint DecisionCooldownTicks;
        public byte MaxWingSize;
        public float ChaosBreakThreshold;
        public float ChaosBreakAggressiveThreshold;
        public float LawfulnessFormThreshold;

        public static StrikeCraftWingDecisionConfig Default => new StrikeCraftWingDecisionConfig
        {
            DecisionCooldownTicks = 60,
            MaxWingSize = 6,
            ChaosBreakThreshold = 0.55f,
            ChaosBreakAggressiveThreshold = 0.45f,
            LawfulnessFormThreshold = 0.55f
        };
    }

    /// <summary>
    /// Tunable profile-driven behavior for strike craft order compliance and extreme tactics.
    /// </summary>
    public struct StrikeCraftBehaviorProfileConfig : IComponentData
    {
        public byte AllowKamikaze;
        public byte RequireRewindEnabled;
        public byte RequireCombatTechTier;
        public byte AllowDirectiveDisobedience;
        public byte RequireCaptainConsent;
        public byte RequireCultureConsent;
        public byte DefaultCaptainAllowsDireTactics;
        public byte DefaultCultureAllowsDireTactics;
        public float ObedienceThreshold;
        public float LawfulnessWeight;
        public float DisciplineWeight;
        public float BaseDisobeyChance;
        public float ChaosDisobeyBonus;
        public float MutinyDisobeyBonus;
        public float KamikazePurityThreshold;
        public float KamikazeLawfulnessThreshold;
        public float KamikazeChaosThreshold;
        public float KamikazeHullThreshold;
        public float KamikazeChance;
        public float KamikazeSpeedMultiplier;
        public float KamikazeTurnMultiplier;
        public float KitingMinExperience;
        public float KitingChance;
        public float KitingMinDistance;
        public float KitingMaxDistance;
        public float KitingStrafeStrength;

        public static StrikeCraftBehaviorProfileConfig Default => new StrikeCraftBehaviorProfileConfig
        {
            AllowKamikaze = 0,
            RequireRewindEnabled = 1,
            RequireCombatTechTier = 0,
            AllowDirectiveDisobedience = 1,
            RequireCaptainConsent = 1,
            RequireCultureConsent = 1,
            DefaultCaptainAllowsDireTactics = 0,
            DefaultCultureAllowsDireTactics = 0,
            ObedienceThreshold = 0.5f,
            LawfulnessWeight = 0.35f,
            DisciplineWeight = 0.35f,
            BaseDisobeyChance = 0.1f,
            ChaosDisobeyBonus = 0.4f,
            MutinyDisobeyBonus = 0.35f,
            KamikazePurityThreshold = 0.7f,
            KamikazeLawfulnessThreshold = 0.7f,
            KamikazeChaosThreshold = 0.7f,
            KamikazeHullThreshold = 0.35f,
            KamikazeChance = 0.25f,
            KamikazeSpeedMultiplier = 1.35f,
            KamikazeTurnMultiplier = 1.2f,
            KitingMinExperience = 0.6f,
            KitingChance = 0.35f,
            KitingMinDistance = 15f,
            KitingMaxDistance = 80f,
            KitingStrafeStrength = 0.35f
        };
    }

    /// <summary>
    /// Configuration for attack run behavior.
    /// </summary>
    public struct AttackRunConfig : IComponentData
    {
        /// <summary>
        /// Preferred weapon delivery type.
        /// </summary>
        public WeaponDeliveryType DeliveryType;

        /// <summary>
        /// Approach vector preference (0 = direct, 1 = flanking, 2 = above/below).
        /// </summary>
        public byte ApproachVector;

        /// <summary>
        /// Distance to start attack run.
        /// </summary>
        public float AttackRange;

        /// <summary>
        /// Distance to break off after pass.
        /// </summary>
        public float DisengageRange;

        /// <summary>
        /// Maximum passes before return.
        /// </summary>
        public byte MaxPasses;

        /// <summary>
        /// Whether to re-attack after disengaging.
        /// </summary>
        public byte ReattackEnabled;

        /// <summary>
        /// Formation spacing during form-up.
        /// </summary>
        public float FormationSpacing;

        /// <summary>
        /// Speed multiplier during approach (0.5 - 1.5).
        /// </summary>
        public half ApproachSpeedMod;

        /// <summary>
        /// Speed multiplier during attack (0.8 - 1.2).
        /// </summary>
        public half AttackSpeedMod;

        public static AttackRunConfig ForRole(StrikeCraftRole role)
        {
            return role switch
            {
                StrikeCraftRole.Interceptor => new AttackRunConfig
                {
                    DeliveryType = WeaponDeliveryType.MissileSalvo,
                    ApproachVector = 0, // Direct
                    AttackRange = 500f,
                    DisengageRange = 200f,
                    MaxPasses = 3,
                    ReattackEnabled = 1,
                    FormationSpacing = 30f,
                    ApproachSpeedMod = (half)1.3f,
                    AttackSpeedMod = (half)1.1f
                },
                StrikeCraftRole.Bomber => new AttackRunConfig
                {
                    DeliveryType = WeaponDeliveryType.TorpedoDrop,
                    ApproachVector = 1, // Flanking
                    AttackRange = 300f,
                    DisengageRange = 400f,
                    MaxPasses = 1,
                    ReattackEnabled = 0,
                    FormationSpacing = 50f,
                    ApproachSpeedMod = (half)0.8f,
                    AttackSpeedMod = (half)0.9f
                },
                StrikeCraftRole.Recon => new AttackRunConfig
                {
                    DeliveryType = WeaponDeliveryType.Strafe,
                    ApproachVector = 2, // Above
                    AttackRange = 800f,
                    DisengageRange = 600f,
                    MaxPasses = 1,
                    ReattackEnabled = 0,
                    FormationSpacing = 100f,
                    ApproachSpeedMod = (half)1.2f,
                    AttackSpeedMod = (half)1.0f
                },
                StrikeCraftRole.Suppression => new AttackRunConfig
                {
                    DeliveryType = WeaponDeliveryType.Suppression,
                    ApproachVector = 0, // Direct
                    AttackRange = 600f,
                    DisengageRange = 300f,
                    MaxPasses = 2,
                    ReattackEnabled = 1,
                    FormationSpacing = 40f,
                    ApproachSpeedMod = (half)1.0f,
                    AttackSpeedMod = (half)0.8f
                },
                _ => new AttackRunConfig
                {
                    DeliveryType = WeaponDeliveryType.Strafe,
                    ApproachVector = 0,
                    AttackRange = 400f,
                    DisengageRange = 250f,
                    MaxPasses = 2,
                    ReattackEnabled = 1,
                    FormationSpacing = 35f,
                    ApproachSpeedMod = (half)1.0f,
                    AttackSpeedMod = (half)1.0f
                }
            };
        }
    }

    /// <summary>
    /// Experience and progression for strike craft crews.
    /// </summary>
    public struct StrikeCraftExperience : IComponentData
    {
        /// <summary>
        /// Total sorties flown.
        /// </summary>
        public uint SortieCount;

        /// <summary>
        /// Total kills achieved.
        /// </summary>
        public uint KillCount;

        /// <summary>
        /// Missions survived.
        /// </summary>
        public uint SurvivalCount;

        /// <summary>
        /// Experience points accumulated.
        /// </summary>
        public uint ExperiencePoints;

        /// <summary>
        /// Current level (0-5).
        /// </summary>
        public byte Level;

        /// <summary>
        /// Service traits unlocked (bitfield).
        /// </summary>
        public StrikeCraftTraits Traits;

        public static StrikeCraftExperience Rookie => new StrikeCraftExperience
        {
            SortieCount = 0,
            KillCount = 0,
            SurvivalCount = 0,
            ExperiencePoints = 0,
            Level = 0,
            Traits = StrikeCraftTraits.None
        };
    }

    /// <summary>
    /// Service traits for experienced strike craft crews.
    /// </summary>
    [System.Flags]
    public enum StrikeCraftTraits : ushort
    {
        None = 0,

        /// <summary>
        /// Improved evasion against PD.
        /// </summary>
        EvasiveManeuvers = 1 << 0,

        /// <summary>
        /// Tighter formation flying.
        /// </summary>
        FormationDiscipline = 1 << 1,

        /// <summary>
        /// Better target selection.
        /// </summary>
        TargetPrioritization = 1 << 2,

        /// <summary>
        /// Faster response to carrier commands.
        /// </summary>
        QuickReaction = 1 << 3,

        /// <summary>
        /// Improved critical hit chance.
        /// </summary>
        PrecisionStrike = 1 << 4,

        /// <summary>
        /// Reduced fuel/ammo consumption.
        /// </summary>
        ResourceEfficiency = 1 << 5,

        /// <summary>
        /// Better survival when damaged.
        /// </summary>
        DamageControl = 1 << 6,

        /// <summary>
        /// Extended sensor range during recon.
        /// </summary>
        EnhancedSensors = 1 << 7,

        /// <summary>
        /// Wing-wide buff aura.
        /// </summary>
        WingLeadership = 1 << 8,

        /// <summary>
        /// Ace-level performance.
        /// </summary>
        AceStatus = 1 << 9
    }

    /// <summary>
    /// Target evaluation for strike craft approach decisions.
    /// </summary>
    public struct TargetDefenseProfile : IComponentData
    {
        /// <summary>
        /// Point defense effectiveness [0, 1].
        /// </summary>
        public half PointDefense;

        /// <summary>
        /// Shield strength ratio [0, 1].
        /// </summary>
        public half ShieldStrength;

        /// <summary>
        /// Anti-fighter screen present.
        /// </summary>
        public byte HasFighterScreen;

        /// <summary>
        /// Number of escort vessels.
        /// </summary>
        public byte EscortCount;

        /// <summary>
        /// Threat level for approach [0, 1].
        /// </summary>
        public half ApproachThreat;
    }

    /// <summary>
    /// Wing coordination data.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct WingMember : IBufferElementData
    {
        /// <summary>
        /// Wing member entity.
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// Position in formation.
        /// </summary>
        public byte Position;

        /// <summary>
        /// Status (0 = active, 1 = damaged, 2 = destroyed).
        /// </summary>
        public byte Status;
    }

    /// <summary>
    /// Tag for strike craft currently on a sortie.
    /// </summary>
    public struct OnSortieTag : IComponentData { }

    /// <summary>
    /// Utility functions for strike craft calculations.
    /// </summary>
    public static class StrikeCraftUtility
    {
        /// <summary>
        /// Calculates formation offset based on stance and position.
        /// </summary>
        public static float3 CalculateWingOffset(VesselStanceMode stance, int position, float spacing)
        {
            if (position == 0)
            {
                return float3.zero; // Leader
            }

            float3 offset;

            switch (stance)
            {
                case VesselStanceMode.Aggressive:
                    // Tight wedge
                    int side = position % 2 == 0 ? 1 : -1;
                    int row = (position + 1) / 2;
                    offset = new float3(side * row * spacing * 0.5f, 0, -row * spacing);
                    break;

                case VesselStanceMode.Defensive:
                    // Spread line
                    int linePos = position - 1;
                    int lineSide = linePos % 2 == 0 ? 1 : -1;
                    int lineIdx = (linePos / 2) + 1;
                    offset = new float3(lineSide * lineIdx * spacing, 0, 0);
                    break;

                case VesselStanceMode.Evasive:
                    // Staggered/scattered
                    var random = new Unity.Mathematics.Random((uint)(position * 12345));
                    offset = new float3(
                        random.NextFloat(-spacing, spacing),
                        random.NextFloat(-spacing * 0.3f, spacing * 0.3f),
                        random.NextFloat(-spacing, 0)
                    );
                    break;

                default: // Balanced
                    // Standard V formation
                    int vSide = position % 2 == 0 ? 1 : -1;
                    int vRow = (position + 1) / 2;
                    offset = new float3(vSide * vRow * spacing * 0.7f, 0, -vRow * spacing * 0.7f);
                    break;
            }

            return offset;
        }

        /// <summary>
        /// Calculates approach vector based on preference and target position.
        /// </summary>
        public static float3 CalculateApproachVector(byte preference, float3 targetPosition, float3 currentPosition)
        {
            float3 direct = math.normalize(targetPosition - currentPosition);

            switch (preference)
            {
                case 1: // Flanking - approach from 90 degrees
                    float3 up = new float3(0, 1, 0);
                    float3 right = math.cross(up, direct);
                    return math.normalize(direct + right * 0.7f);

                case 2: // Above/below
                    return math.normalize(direct + new float3(0, 0.5f, 0));

                default: // Direct
                    return direct;
            }
        }

        /// <summary>
        /// Evaluates whether approach is safe enough.
        /// </summary>
        public static bool EvaluateApproachSafety(
            in TargetDefenseProfile defense,
            in StrikeCraftExperience experience,
            VesselStanceMode stance)
        {
            float baseThreat = (float)defense.ApproachThreat;

            // Experience reduces perceived threat
            float experienceMod = 1f - (experience.Level * 0.1f);

            // Traits can reduce threat further
            if ((experience.Traits & StrikeCraftTraits.EvasiveManeuvers) != 0)
            {
                experienceMod -= 0.1f;
            }

            float adjustedThreat = baseThreat * experienceMod;

            // Stance affects threshold
            float threshold = stance switch
            {
                VesselStanceMode.Aggressive => 0.9f,
                VesselStanceMode.Balanced => 0.7f,
                VesselStanceMode.Defensive => 0.5f,
                VesselStanceMode.Evasive => 0.3f,
                _ => 0.7f
            };

            return adjustedThreat < threshold;
        }

        /// <summary>
        /// Calculates XP gain from sortie.
        /// </summary>
        public static uint CalculateSortieXP(bool survived, uint kills, bool targetDestroyed)
        {
            uint xp = 10; // Base sortie XP

            if (survived)
            {
                xp += 5;
            }

            xp += kills * 20;

            if (targetDestroyed)
            {
                xp += 30;
            }

            return xp;
        }

        /// <summary>
        /// Returns XP required for next level.
        /// </summary>
        public static uint XPForLevel(byte level)
        {
            return level switch
            {
                0 => 100,
                1 => 300,
                2 => 600,
                3 => 1000,
                4 => 1500,
                _ => uint.MaxValue
            };
        }
    }
}
