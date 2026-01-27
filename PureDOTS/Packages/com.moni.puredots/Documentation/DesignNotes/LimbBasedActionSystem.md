# Limb-Based Action System

**Status**: Concept Design
**Last Updated**: 2025-11-30
**Cross-Project**: Godgame (primary), Space4X (adapted)

---

## Overview

The **Limb-Based Action System** provides independent cooldowns and action capabilities for each body part (hands, legs, head). Unlike traditional global cooldown systems, entities can perform multiple actions simultaneously using different limbs, with skill progression reducing penalties and enabling complex multi-action combos.

**Core Principles**:
- ✅ **No Global Cooldowns**: Each limb has independent action timing
- ✅ **Skill-Based Mastery**: High skill reduces penalties, enables multi-action
- ✅ **Action Interference**: Some actions penalize others (parry slows attack)
- ✅ **Focus Budget**: Complex actions require concentration allocation
- ✅ **Class Asymmetry**: Rogues dual-wield, mages dual-cast, warriors shield+sword
- ✅ **Deterministic**: All timings/penalties calculable, rewindable

**Design Goals**:
- Enable fluid combat with simultaneous actions (attack + parry, cast + ritual)
- Reward skill investment with reduced penalties and faster execution
- Create class-specific playstyles through limb specialization
- Support future extensions (grafted limbs, body part casting)
- Maintain performance with parallel action resolution

---

## Core Components

### Limb System

```csharp
public struct EntityLimbs : IComponentData
{
    public Entity LeftHand;
    public Entity RightHand;
    public Entity LeftLeg;               // Future: Kick attacks, mage leg casting
    public Entity RightLeg;
    public Entity Head;                  // Future: Mage head casting, headbutt
    public byte ExtraLimbCount;          // Future: Grafted limbs
}

public struct Limb : IComponentData
{
    public LimbType Type;
    public LimbState State;
    public Entity Owner;
    public float CurrentCooldown;        // Ticks until next action
    public float BaseCooldown;           // Default cooldown (modified by skill)
    public DynamicBuffer<ActionPenalty> ActivePenalties;
    public bool IsOccupied;              // Currently performing action
}

public enum LimbType : byte
{
    LeftHand,
    RightHand,
    LeftLeg,
    RightLeg,
    Head,
    GraftedExtra,                // Future: Additional grafted limbs
}

public enum LimbState : byte
{
    Idle,
    Attacking,
    Parrying,
    Casting,
    Channeling,                  // Long-duration action (ritual)
    Disabled,                    // Injured/crippled
}
```

### Action Definitions

```csharp
public struct LimbAction : IComponentData
{
    public ActionType Type;
    public LimbType RequiredLimb;
    public float BaseCooldown;           // Ticks
    public float ExecutionTime;          // Action duration
    public float FocusCost;              // 0-1 (concentration required)
    public DynamicBuffer<ActionPenalty> AppliesPenalties;  // To other limbs
    public SkillRequirement SkillReq;
}

public enum ActionType : byte
{
    // Combat
    Attack,
    Parry,
    ShieldBash,
    Kick,
    Headbutt,

    // Magic
    CastSpell,
    DualCast,                    // Casting with both hands
    Ritual,                      // Long-duration channeling
    ImbueMagic,                  // Enchant projectile

    // Ranged
    FireArrow,
    Multishot,
    AimPrecision,                // Focus on accuracy
    FirstStrike,                 // Auto-attack first approaching enemy

    // Utility
    UseItem,
    Grapple,
    Dodge,

    // Space4X Crew
    Communications,              // Comms officer
    ManualControl,               // Pilot override
    RepairAction,                // Engineer
    BoardingDefense,             // Fight while at station
}

public struct ActionPenalty : IBufferElementData
{
    public LimbType AffectedLimb;        // Which limb gets penalty
    public float CooldownMultiplier;     // 1.5 = +50% cooldown
    public float FocusIncrease;          // Additional focus cost
    public uint DurationTicks;           // How long penalty lasts
}
```

### Skill-Based Reduction

```csharp
public struct LimbSkillModifiers : IComponentData
{
    public Entity Owner;
    public LimbType Limb;
    public float CooldownReduction;      // 0-1 (0.3 = 30% faster)
    public float PenaltyReduction;       // 0-1 (0.5 = penalties halved)
    public float FocusEfficiency;        // 0-1 (0.4 = 40% less focus cost)
    public bool CanMultiAction;          // Can perform 2+ actions simultaneously
}

// Skill progression reduces penalties
public static LimbSkillModifiers CalculateLimbSkill(VillagerSkills skills, LimbType limb, VillagerClass villagerClass)
{
    float skillLevel = GetRelevantSkill(skills, limb, villagerClass) / 100f;  // 0-1

    return new LimbSkillModifiers
    {
        Limb = limb,
        CooldownReduction = skillLevel * 0.5f,        // Up to 50% faster
        PenaltyReduction = skillLevel * 0.7f,         // Up to 70% penalty reduction
        FocusEfficiency = skillLevel * 0.6f,          // Up to 60% less focus cost
        CanMultiAction = skillLevel > 0.7f,           // Master level (70+) unlocks multi-action
    };
}

// Example: Rogue skill level 85
// - CooldownReduction: 0.425 (42.5% faster attacks)
// - PenaltyReduction: 0.595 (59.5% less parry penalty)
// - FocusEfficiency: 0.51 (51% less focus cost)
// - CanMultiAction: true (can attack + parry simultaneously)
```

---

## Class-Specific Systems

### Rogue: Dual Wielding

```csharp
public struct DualWieldCombat : IComponentData
{
    public Entity LeftHandWeapon;
    public Entity RightHandWeapon;
    public float AttackSpeedBonus;       // 2.0 = twice as fast
    public bool IndependentParry;        // Can parry with each hand separately
    public float MasterLevel;            // 0-1 (skill-based)
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class RogueDualWieldSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref DualWieldCombat dualWield, in EntityLimbs limbs, in VillagerSkills skills) =>
        {
            var leftHand = GetComponent<Limb>(limbs.LeftHand);
            var rightHand = GetComponent<Limb>(limbs.RightHand);

            // Calculate master level (reduces penalties)
            dualWield.MasterLevel = GetDualWieldSkill(skills) / 100f;

            // Attack speed bonus (rogues attack faster with daggers)
            dualWield.AttackSpeedBonus = 1.0f + (dualWield.MasterLevel * 1.0f);  // Up to 2x speed

            // Independent parry at high levels
            dualWield.IndependentParry = dualWield.MasterLevel > 0.6f;

            // Left hand attack
            if (leftHand.State == LimbState.Idle && leftHand.CurrentCooldown == 0f)
            {
                ExecuteAttack(limbs.LeftHand, dualWield.AttackSpeedBonus);

                // Apply penalty to other limbs (if not master)
                if (dualWield.MasterLevel < 0.9f)
                {
                    float penaltyAmount = 1.3f * (1f - dualWield.MasterLevel);  // 30% penalty at low skill
                    ApplyPenalty(limbs.RightHand, penaltyAmount, 100);  // 100 tick penalty
                }
            }

            // Right hand parry (independent)
            if (dualWield.IndependentParry && rightHand.State == LimbState.Idle)
            {
                if (IsIncomingAttack(entity))
                {
                    ExecuteParry(limbs.RightHand);

                    // Parry penalty to attack (reduced by skill)
                    float parryPenalty = 1.5f * (1f - dualWield.MasterLevel * 0.7f);  // Up to 70% reduction
                    ApplyPenalty(limbs.LeftHand, parryPenalty, 50);
                }
            }

        }).Run();
    }
}
```

**Progression Example**:
- **Novice Rogue (Skill 20)**: Attack speed 1.2x, parry penalty 42%, cannot multi-action
- **Adept Rogue (Skill 60)**: Attack speed 1.6x, parry penalty 18%, can parry with one hand
- **Master Rogue (Skill 90)**: Attack speed 1.9x, parry penalty 4.5%, attacks and parries with minimal focus

---

### Warrior: Shield + Sword

```csharp
public struct ShieldAndSwordCombat : IComponentData
{
    public Entity ShieldHand;            // Usually left
    public Entity SwordHand;             // Usually right
    public float ShieldBashCooldown;
    public float ThrustCooldown;
    public bool CanSimultaneousAction;   // Bash + thrust at once
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class WarriorShieldSwordSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref ShieldAndSwordCombat combat, in EntityLimbs limbs, in VillagerSkills skills) =>
        {
            var shieldHand = GetComponent<Limb>(limbs.LeftHand);
            var swordHand = GetComponent<Limb>(limbs.RightHand);

            float warriorSkill = GetMeleeSkill(skills) / 100f;
            combat.CanSimultaneousAction = warriorSkill > 0.5f;  // Unlocked at skill 50

            // Shield bash (stun enemy)
            if (shieldHand.State == LimbState.Idle && combat.ShieldBashCooldown == 0f)
            {
                ExecuteShieldBash(limbs.LeftHand);
                combat.ShieldBashCooldown = 300f * (1f - warriorSkill * 0.3f);  // Faster at high skill

                // Simultaneous thrust (if skilled)
                if (combat.CanSimultaneousAction && swordHand.State == LimbState.Idle)
                {
                    ExecuteThrust(limbs.RightHand);
                    combat.ThrustCooldown = 200f * (1f - warriorSkill * 0.4f);
                }
            }

        }).Run();
    }
}
```

---

### Mage: Dual Casting & Rituals

```csharp
public struct MageCastingCapability : IComponentData
{
    public float FinesseLevel;           // 0-1 (skill-based)
    public bool CanDualCast;             // Cast different spells with each hand
    public bool CanRitualCast;           // Ritual + spell casting
    public bool CanBodyCast;             // Use legs/head for casting
    public float FocusBudget;            // 0-1 (total concentration available)
    public float FocusAllocated;         // Currently used focus
}

public struct RitualChanneling : IComponentData
{
    public Entity Ritual;
    public float FocusCost;              // 0-1 (portion of focus budget)
    public uint ChannelStartTick;
    public uint RequiredDuration;
    public bool CanInterrupt;            // If takes damage
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MageDualCastSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref MageCastingCapability mage, in EntityLimbs limbs, in VillagerSkills skills) =>
        {
            var leftHand = GetComponent<Limb>(limbs.LeftHand);
            var rightHand = GetComponent<Limb>(limbs.RightHand);

            float magicSkill = GetMagicSkill(skills) / 100f;
            mage.FinesseLevel = magicSkill;
            mage.CanDualCast = magicSkill > 0.7f;           // Unlocked at 70 skill
            mage.CanRitualCast = magicSkill > 0.6f;         // Unlocked at 60 skill
            mage.CanBodyCast = magicSkill > 0.9f;           // Master level (90+)

            // Focus budget (increases with skill)
            mage.FocusBudget = 0.5f + (magicSkill * 0.5f);  // 0.5 to 1.0

            // Ritual channeling (takes focus)
            if (HasComponent<RitualChanneling>(entity))
            {
                var ritual = GetComponent<RitualChanneling>(entity);
                mage.FocusAllocated = ritual.FocusCost;

                // Can still cast spells if focus available
                float remainingFocus = mage.FocusBudget - mage.FocusAllocated;
                if (mage.CanRitualCast && remainingFocus > 0.3f)
                {
                    // Cast spell with remaining focus
                    if (leftHand.State == LimbState.Idle)
                    {
                        CastSpell(limbs.LeftHand, remainingFocus);
                    }
                }
            }
            else
            {
                // Dual casting (different spells with each hand)
                if (mage.CanDualCast)
                {
                    if (leftHand.State == LimbState.Idle && rightHand.State == LimbState.Idle)
                    {
                        float focusPerHand = mage.FocusBudget / 2f;

                        // Cast different spells simultaneously
                        CastSpell(limbs.LeftHand, focusPerHand);   // Fireball
                        CastSpell(limbs.RightHand, focusPerHand);  // Ice Bolt

                        mage.FocusAllocated = mage.FocusBudget;
                    }
                }

                // Body casting (legs, head)
                if (mage.CanBodyCast)
                {
                    var head = GetComponent<Limb>(limbs.Head);
                    if (head.State == LimbState.Idle)
                    {
                        CastSpell(limbs.Head, 0.2f);  // Minor spell from head
                    }
                }
            }

        }).Run();
    }
}
```

**Progression Example**:
- **Novice Mage (Skill 30)**: Focus budget 0.65, cannot dual cast, rituals interrupt casting
- **Adept Mage (Skill 70)**: Focus budget 0.85, can dual cast, ritual + spell simultaneous
- **Master Mage (Skill 95)**: Focus budget 0.975, dual cast + body casting (4 spells at once)

---

### Archer: Precision & Imbue

```csharp
public struct ArcherCombat : IComponentData
{
    public Entity BowHand;               // Usually both hands for bow
    public float AimPrecision;           // 0-1 (skill-based)
    public bool CanParryWhileAiming;     // High-level archers
    public bool CanMultishot;
    public int MultishotCount;           // Arrows per shot
    public bool HasFirstStrike;          // Auto-attack first approaching enemy
    public float FirstStrikeBonusDamage; // Bonus damage to first target
}

public struct ImbuedArrow : IComponentData
{
    public Entity Projectile;
    public MagicProperty Property;       // Fire, Ice, Lightning, etc.
    public float IntelligenceBonus;      // Damage/effect bonus from INT
    public float WillpowerBonus;         // Imbue strength from Will
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ArcherCombatSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref ArcherCombat archer, in EntityLimbs limbs, in VillagerSkills skills, in VillagerStats stats) =>
        {
            var bowHand = GetComponent<Limb>(limbs.BowHand);

            float archerSkill = GetRangedSkill(skills) / 100f;
            archer.AimPrecision = archerSkill;
            archer.CanParryWhileAiming = archerSkill > 0.7f;     // Unlocked at 70
            archer.CanMultishot = archerSkill > 0.6f;            // Unlocked at 60
            archer.MultishotCount = (int)(archerSkill * 5);      // Up to 5 arrows
            archer.HasFirstStrike = archerSkill > 0.5f;          // Unlocked at 50
            archer.FirstStrikeBonusDamage = archerSkill * 0.5f;  // Up to +50% damage

            // Fire arrow
            if (bowHand.State == LimbState.Idle)
            {
                FireArrow(limbs.BowHand);

                // Multishot (multiple arrows)
                if (archer.CanMultishot && Random.NextFloat() < 0.3f)
                {
                    for (int i = 0; i < archer.MultishotCount; i++)
                    {
                        FireArrow(limbs.BowHand);
                    }
                }

                // Imbue with magic (if intelligent/high will)
                if (stats.Intelligence > 60 || stats.Willpower > 60)
                {
                    var arrow = GetLastFiredArrow(entity);
                    ImbueArrowWithMagic(arrow, stats.Intelligence, stats.Willpower);
                }
            }

            // Parry while aiming (no aim penalty)
            if (archer.CanParryWhileAiming && IsIncomingAttack(entity))
            {
                ExecuteParry(limbs.LeftHand);  // Free hand parry
                // No penalty to aim precision (skill negates penalty)
            }

            // First strike (auto-attack first approaching enemy)
            if (archer.HasFirstStrike)
            {
                var nearestEnemy = GetNearestApproachingEnemy(entity, 50f);
                if (nearestEnemy != Entity.Null && !HasBeenFirstStruck(nearestEnemy))
                {
                    var arrow = FireArrow(limbs.BowHand);
                    ApplyFirstStrikeBonus(arrow, archer.FirstStrikeBonusDamage);
                    MarkAsFirstStruck(nearestEnemy);
                }
            }

        }).Run();
    }
}
```

**Imbue Arrow Logic**:
```csharp
public static void ImbueArrowWithMagic(Entity arrow, int intelligence, int willpower)
{
    var imbue = new ImbuedArrow
    {
        Projectile = arrow,
        IntelligenceBonus = intelligence / 100f,  // 0-1
        WillpowerBonus = willpower / 100f,
    };

    // Choose property based on stats
    if (intelligence > 80)
    {
        imbue.Property = MagicProperty.Lightning;  // High INT = lightning
    }
    else if (willpower > 80)
    {
        imbue.Property = MagicProperty.Fire;       // High Will = fire
    }
    else
    {
        imbue.Property = MagicProperty.Ice;        // Default = ice
    }

    // Apply damage bonus
    var projectile = GetComponent<ProjectileComponents>(arrow);
    projectile.Damage *= (1f + imbue.IntelligenceBonus * 0.5f + imbue.WillpowerBonus * 0.3f);

    EntityManager.AddComponent(arrow, imbue);
}
```

---

## Action Penalty System

### Penalty Application

```csharp
public struct ActionPenalty : IBufferElementData
{
    public LimbType AffectedLimb;
    public float CooldownMultiplier;     // 1.5 = +50% cooldown
    public float FocusIncrease;          // +0.2 = 20% more focus cost
    public uint StartTick;
    public uint DurationTicks;
    public float SkillReduction;         // How much skill reduces this penalty
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ActionPenaltySystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity entity, ref Limb limb, in DynamicBuffer<ActionPenalty> penalties, in LimbSkillModifiers skillMods) =>
        {
            float totalCooldownMultiplier = 1f;
            float totalFocusIncrease = 0f;

            foreach (var penalty in penalties)
            {
                if (penalty.AffectedLimb == limb.Type)
                {
                    // Apply penalty (reduced by skill)
                    float penaltyReduction = skillMods.PenaltyReduction * penalty.SkillReduction;
                    totalCooldownMultiplier *= math.lerp(penalty.CooldownMultiplier, 1f, penaltyReduction);
                    totalFocusIncrease += penalty.FocusIncrease * (1f - penaltyReduction);

                    // Remove expired penalties
                    if (CurrentTick - penalty.StartTick > penalty.DurationTicks)
                    {
                        RemovePenalty(entity, penalty);
                    }
                }
            }

            // Apply total penalties to limb
            limb.CurrentCooldown *= totalCooldownMultiplier;
            // Focus increase applied when action attempted

        }).Run();
    }
}
```

### Parry Penalty Example

```csharp
// Parry increases attack cooldown
public static void ExecuteParry(Entity limb)
{
    var limbComp = GetComponent<Limb>(limb);
    limbComp.State = LimbState.Parrying;
    limbComp.CurrentCooldown = 100f;  // Base parry cooldown

    // Apply penalty to attacking (other hand or same hand)
    var owner = limbComp.Owner;
    var limbs = GetComponent<EntityLimbs>(owner);

    var penalty = new ActionPenalty
    {
        AffectedLimb = limbComp.Type == LimbType.LeftHand ? LimbType.RightHand : LimbType.LeftHand,
        CooldownMultiplier = 1.5f,      // +50% attack cooldown
        FocusIncrease = 0.1f,            // +10% focus cost
        StartTick = CurrentTick,
        DurationTicks = 50,              // Lasts 50 ticks
        SkillReduction = 0.7f,           // 70% of this penalty can be negated by skill
    };

    ApplyPenalty(owner, penalty);
}
```

---

## Focus Budget System

### Focus Allocation

```csharp
public struct FocusBudget : IComponentData
{
    public float MaxFocus;               // 0-1 (skill-based)
    public float CurrentFocus;           // Currently allocated
    public DynamicBuffer<FocusAllocation> Allocations;
}

public struct FocusAllocation : IBufferElementData
{
    public ActionType Action;
    public LimbType Limb;
    public float FocusCost;              // 0-1
    public uint StartTick;
    public bool IsChanneling;            // Long-duration action
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class FocusBudgetSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref FocusBudget focus, in VillagerSkills skills) =>
        {
            // Calculate max focus (skill-based)
            float concentrationSkill = GetConcentrationSkill(skills) / 100f;
            focus.MaxFocus = 0.5f + (concentrationSkill * 0.5f);  // 0.5 to 1.0

            // Tally current allocations
            float totalAllocated = 0f;
            foreach (var allocation in focus.Allocations)
            {
                totalAllocated += allocation.FocusCost;

                // Release focus when action completes
                if (!IsActionActive(allocation))
                {
                    RemoveAllocation(focus, allocation);
                }
            }

            focus.CurrentFocus = totalAllocated;

            // Focus exhaustion (penalties if over budget)
            if (focus.CurrentFocus > focus.MaxFocus)
            {
                float overload = focus.CurrentFocus - focus.MaxFocus;
                ApplyFocusExhaustionPenalty(entity, overload);
            }

        }).Run();
    }
}
```

### Multi-Action Focus Cost

```csharp
// Example: Mage casting ritual + spell
public static bool TryAllocateFocus(Entity entity, ActionType action, LimbType limb, float focusCost)
{
    var focus = GetComponent<FocusBudget>(entity);
    var skillMods = GetComponent<LimbSkillModifiers>(entity, limb);

    // Reduce focus cost by skill efficiency
    float actualCost = focusCost * (1f - skillMods.FocusEfficiency);

    // Check if focus available
    if (focus.CurrentFocus + actualCost > focus.MaxFocus)
    {
        return false;  // Not enough focus
    }

    // Allocate focus
    var allocation = new FocusAllocation
    {
        Action = action,
        Limb = limb,
        FocusCost = actualCost,
        StartTick = CurrentTick,
        IsChanneling = action == ActionType.Ritual,
    };

    focus.Allocations.Add(allocation);
    focus.CurrentFocus += actualCost;

    return true;
}
```

---

## Space4X Adaptation

### Crew Multi-Tasking

```csharp
public struct CrewMember : IComponentData
{
    public Entity Ship;
    public CrewRole PrimaryRole;
    public CrewRole SecondaryRole;         // Can perform secondary tasks
    public float MultiTaskSkill;           // 0-1 (reduces penalties)
    public bool HasNeuralInterface;        // Tech unlock
}

public enum CrewRole : byte
{
    Captain,
    CommsOfficer,
    Engineer,
    Pilot,
    Gunner,
    Medic,
    Marine,
}

public struct CrewAction : IComponentData
{
    public ActionType PrimaryAction;
    public ActionType SecondaryAction;
    public float PrimaryCooldown;
    public float SecondaryCooldown;
    public float EfficiencyPenalty;        // Penalty for multi-tasking
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class CrewMultiTaskSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref CrewAction action, in CrewMember crew, in CaptainSkills skills) =>
        {
            // Comms officer multi-tasking
            if (crew.PrimaryRole == CrewRole.CommsOfficer)
            {
                action.PrimaryAction = ActionType.Communications;
                action.PrimaryCooldown = 200f;

                // Check if boarding party engaged command bridge
                if (IsBoardingPartyOnBridge(crew.Ship))
                {
                    action.SecondaryAction = ActionType.BoardingDefense;
                    action.SecondaryCooldown = 150f;

                    // Efficiency penalty (reduced by skill and neural interface)
                    float basePenalty = 0.4f;  // 40% penalty
                    if (crew.HasNeuralInterface)
                    {
                        basePenalty *= 0.5f;  // Neural interface halves penalty
                    }
                    action.EfficiencyPenalty = basePenalty * (1f - crew.MultiTaskSkill);

                    // Apply penalty to both actions
                    action.PrimaryCooldown *= (1f + action.EfficiencyPenalty);
                    action.SecondaryCooldown *= (1f + action.EfficiencyPenalty);
                }
            }

        }).Run();
    }
}
```

### Neural Interface Tech

```csharp
public struct NeuralInterface : IComponentData
{
    public Entity Crew;
    public TechLevel Level;
    public float PenaltyReduction;         // 0-1 (reduces multi-task penalty)
    public bool EnablesSimultaneousControl;  // Pilot ship + fight simultaneously
    public float CognitiveLoad;            // 0-1 (mental strain)
}

public enum TechLevel : byte
{
    Basic,                     // -50% multi-task penalty
    Advanced,                  // -75% multi-task penalty
    Neural,                    // -90% multi-task penalty, enables simultaneous
}

// Example: Advanced neural interface
public static void ApplyNeuralInterface(Entity crew, TechLevel level)
{
    var neuralInterface = new NeuralInterface
    {
        Crew = crew,
        Level = level,
        PenaltyReduction = level switch
        {
            TechLevel.Basic => 0.5f,
            TechLevel.Advanced => 0.75f,
            TechLevel.Neural => 0.9f,
            _ => 0f,
        },
        EnablesSimultaneousControl = level == TechLevel.Neural,
        CognitiveLoad = 0f,
    };

    // If boarding party engaged bridge
    if (IsBoardingPartyOnBridge(GetShip(crew)))
    {
        // Without neural interface: Cripple ship command
        // With neural interface: Reduce penalties

        var crewAction = GetComponent<CrewAction>(crew);
        crewAction.EfficiencyPenalty *= (1f - neuralInterface.PenaltyReduction);

        // Neural level: Can pilot + fight with minimal penalty
        if (neuralInterface.EnablesSimultaneousControl)
        {
            crewAction.EfficiencyPenalty = 0.1f;  // Only 10% penalty
        }
    }

    EntityManager.AddComponent(crew, neuralInterface);
}
```

---

## Future Extensions

### Grafted Limbs

```csharp
public struct GraftedLimb : IComponentData
{
    public Entity Owner;
    public LimbType Type;                // GraftedExtra
    public GraftOrigin Origin;
    public float EfficiencyModifier;     // 0-1 (quality of graft)
    public bool IsRejectRisk;            // Body may reject graft
}

public enum GraftOrigin : byte
{
    Mechanical,            // Prosthetic
    Biological,            // Cloned/transplanted
    Magical,               // Enchanted limb
    Demonic,               // Forbidden graft (alignment shift)
    AlienTech,             // Space4X xenotech graft
}

// Example: Archer with grafted extra arms (can dual-wield bows)
public static void GraftExtraArms(Entity archer)
{
    var limbs = GetComponent<EntityLimbs>(archer);
    limbs.ExtraLimbCount = 2;  // 2 additional arms

    var graftedLeft = EntityManager.CreateEntity();
    EntityManager.AddComponent(graftedLeft, new GraftedLimb
    {
        Owner = archer,
        Type = LimbType.GraftedExtra,
        Origin = GraftOrigin.Biological,
        EfficiencyModifier = 0.8f,  // 80% efficiency (not native limb)
        IsRejectRisk = true,
    });

    var graftedRight = EntityManager.CreateEntity();
    EntityManager.AddComponent(graftedRight, new GraftedLimb
    {
        Owner = archer,
        Type = LimbType.GraftedExtra,
        Origin = GraftOrigin.Biological,
        EfficiencyModifier = 0.8f,
        IsRejectRisk = true,
    });

    // Now archer can dual-wield bows (4 arms total)
    var archerCombat = GetComponent<ArcherCombat>(archer);
    archerCombat.CanDualWieldBows = true;
}
```

### Body Part Casting (Mages)

```csharp
// Master mages can cast with legs, head, or grafted limbs
public struct BodyPartCasting : IComponentData
{
    public bool CanCastWithLegs;         // Kick + cast
    public bool CanCastWithHead;         // Headbutt + cast
    public bool CanCastWithGrafted;      // Extra limbs
    public float BodyCastEfficiency;     // 0-1 (power of non-hand casts)
}

// Example: Master mage casting with 4 limbs simultaneously
public static void MasterMageQuadCast(Entity mage)
{
    var limbs = GetComponent<EntityLimbs>(mage);
    var casting = GetComponent<BodyPartCasting>(mage);

    if (casting.CanCastWithLegs && casting.CanCastWithHead)
    {
        // Cast 4 different spells at once
        CastSpell(limbs.LeftHand, 0.25f);   // 25% focus each
        CastSpell(limbs.RightHand, 0.25f);
        CastSpell(limbs.LeftLeg, 0.25f);
        CastSpell(limbs.Head, 0.25f);

        // Total focus: 1.0 (entire budget)
    }
}
```

---

## Integration with Existing Systems

### Skill Progression

- Limb skill levels stored in `VillagerSkills` or `CaptainSkills`
- XP gained from successful actions (attack, parry, cast)
- Skill thresholds unlock capabilities (dual cast at 70, parry while aiming at 70)
- Skill reduces penalties and cooldowns

### Alignment & Moral Conflict

- Demonic grafts shift alignment toward Evil (−20 Moral, −30 Purity)
- Forbidden body magic (necromancy via limbs) triggers moral conflict
- Good-aligned villagers refuse grafts from Evil sources

### Combat Systems

- Limb-based damage (targeting specific limbs to disable actions)
- Parry reduces incoming damage, increases enemy cooldown
- Shield bash stuns enemy (interrupt actions)

### Rewind & Determinism

- All limb states, cooldowns, and penalties stored in history buffer
- Action timing deterministic (seeded random for crits, multishot chances)
- Player can rewind to retry multi-action combos

---

## Implementation Checklist

- [ ] Define `EntityLimbs` component with limb entities
- [ ] Define `Limb` component with state, cooldown, penalties
- [ ] Define `LimbAction` with action types and penalties
- [ ] Implement `LimbSkillModifiers` calculation from skills
- [ ] Create `DualWieldCombat` system for rogues
- [ ] Create `ShieldAndSwordCombat` system for warriors
- [ ] Create `MageCastingCapability` system with dual cast
- [ ] Create `ArcherCombat` system with precision, multishot, first strike
- [ ] Implement `ActionPenaltySystem` with skill-based reduction
- [ ] Implement `FocusBudgetSystem` for concentration allocation
- [ ] Add imbue arrow logic (intelligence/willpower scaling)
- [ ] Adapt for Space4X (crew multi-tasking, neural interface)
- [ ] Add grafted limb system (future)
- [ ] Add body part casting for master mages (future)
- [ ] Integrate with combat systems (limb damage, parry)
- [ ] Add UI for cooldown visualization (per-limb timers)
- [ ] Test skill progression (novice → master unlocks)
- [ ] Balance cooldowns, penalties, and focus costs

---

## Example Scenarios

### Scenario 1: Master Rogue vs. 3 Bandits

**Setup**:
- Master rogue (Dual Wield Skill 90)
- 3 bandits (Melee Skill 40 each)

**Combat Flow**:
1. **Tick 0**: Rogue attacks with left hand (dagger)
   - Attack speed: 1.9x (190% of base)
   - Damage: 15 HP
   - Penalty to right hand: 4.5% (negligible)

2. **Tick 20**: Bandit 1 attacks, rogue parries with right hand
   - Parry successful (blocks 80% damage)
   - Penalty to left hand: 4.5% (negligible due to master level)
   - Right hand cooldown: 100 ticks

3. **Tick 40**: Rogue attacks with left hand again (penalty minimal)
   - Damage: 15 HP
   - Bandit 1 at 50% HP

4. **Tick 60**: Bandits 2 and 3 attack simultaneously
   - Rogue parries bandit 2 with left hand
   - Rogue dodges bandit 3 (focus budget allows)

5. **Tick 80**: Right hand cooldown complete, attacks bandit 1
   - Damage: 15 HP
   - Bandit 1 defeated

**Outcome**: Master rogue defeats 3 bandits with minimal injuries (parry efficiency + dual wield speed)

---

### Scenario 2: Adept Mage Ritual + Combat

**Setup**:
- Adept mage (Magic Skill 70, Focus Budget 0.85)
- Channeling ritual (Focus Cost 0.5)
- 2 enemies approaching

**Combat Flow**:
1. **Tick 0**: Mage begins ritual
   - Focus allocated: 0.5 (50% of budget)
   - Remaining focus: 0.35

2. **Tick 200**: Enemy 1 approaches
   - Mage casts fireball with left hand (Focus Cost 0.3)
   - Total focus: 0.8 (within budget)
   - Fireball hits, enemy 1 at 60% HP

3. **Tick 400**: Enemy 2 approaches, enemy 1 still advancing
   - Mage cannot dual cast (focus budget exceeded if attempted)
   - Chooses to interrupt ritual, cast ice bolt
   - Ritual progress lost, but enemy 2 frozen

4. **Tick 600**: Ritual restarted, enemies defeated

**Outcome**: Mage successfully defended while channeling (skill 70 allows ritual + single spell)

---

### Scenario 3: Archer First Strike

**Setup**:
- Master archer (Ranged Skill 85, First Strike enabled)
- 5 enemies approaching (50m away)

**Combat Flow**:
1. **Tick 0**: Enemies detected at 50m
2. **Tick 100**: Enemy 1 enters strike range (40m)
   - First strike activates (auto-attack)
   - Damage: 30 HP + 42.5% bonus = 42.75 HP
   - Enemy 1 one-shot killed

3. **Tick 200**: Enemy 2 enters range
   - First strike activates (auto-attack)
   - Enemy 2 killed

4. **Tick 300**: Enemies 3-5 enter range simultaneously
   - Only 1 first strike available
   - Archer manually fires at enemy 3 (multishot: 4 arrows)
   - Enemies 4-5 require normal combat

**Outcome**: Archer eliminates 3 enemies before melee engagement (first strike efficiency)

---

### Scenario 4: Space4X Comms Officer Under Boarding Attack

**Setup**:
- Comms officer (Multi-Task Skill 60, Advanced Neural Interface)
- Boarding party breaches command bridge
- Critical communications in progress (treaty negotiation)

**Combat Flow**:
1. **Tick 0**: Comms officer engaged in negotiations
   - Primary action: Communications (Cooldown 200)
   - Secondary action: None

2. **Tick 500**: Boarding party breaches bridge (5 hostiles)
   - Secondary action: Boarding Defense (Cooldown 150)
   - Efficiency penalty: 40% base → 10% (neural interface + skill)

3. **Tick 650**: Comms officer fires sidearm (boarding defense)
   - Cooldown: 150 * 1.1 = 165 ticks
   - Kills 1 hostile

4. **Tick 700**: Continues negotiations (penalty minimal)
   - Cooldown: 200 * 1.1 = 220 ticks
   - Treaty negotiation progresses

5. **Tick 865**: Fires again (boarding defense)
   - Kills 2nd hostile
   - Marines arrive to support (tick 900)

**Outcome**: Comms officer successfully defended bridge while maintaining negotiations (neural interface enabled multi-tasking)

---

## Summary

The **Limb-Based Action System** enables:

1. **Independent Cooldowns**: Each limb operates separately, no global cooldowns
2. **Skill-Based Mastery**: High skill reduces penalties, enables multi-action combos
3. **Class Asymmetry**: Rogues dual-wield, warriors shield+sword, mages dual-cast, archers imbue
4. **Action Interference**: Parry slows attack (skill mitigates), focus budget limits simultaneous actions
5. **Progression Depth**: Novice struggles with penalties, master performs fluid combos
6. **Space4X Adaptation**: Crew multi-tasking, neural interfaces reduce penalties
7. **Future Extensions**: Grafted limbs (extra arms for dual bows), body part casting (mage quad-cast)

**Next Steps**:
- Prototype limb entity structure with cooldown tracking
- Implement skill-based penalty reduction formulas
- Create class-specific systems (rogue, warrior, mage, archer)
- Design UI for per-limb cooldown visualization
- Balance action timing, penalties, and focus costs
- Test progression from novice to master across classes

---

**Related Documents**:
- [BehaviorAlignment_Summary.md](../../../Docs/BehaviorAlignment_Summary.md) - Alignment system
- [VillagerDecisionMaking.md](VillagerDecisionMaking.md) - AI decision framework
- [EnvironmentalQuestsAndLootVectors.md](EnvironmentalQuestsAndLootVectors.md) - Class effectiveness

**Design Lead**: [TBD]
**Technical Lead**: [TBD]
**Last Review**: 2025-11-30
