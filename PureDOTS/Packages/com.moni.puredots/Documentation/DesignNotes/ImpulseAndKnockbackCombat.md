# Impulse and Knockback Combat System

**Status**: Concept Design
**Last Updated**: 2025-11-30
**Cross-Project**: Godgame (primary), Space4X (adapted for boarding)

---

## Overview

**Impulse-Based Combat** creates dramatic, physics-driven battles where every hit carries force (impulse) that drains stamina, knocks back opponents, and enables "anime-style" fights between master-level combatants. Warriors hit like freight trains, rogues dance around heavy blows while draining stamina, mages teleport away from devastating impacts, and masters shrug off attacks that would flatten novices.

**Core Principles**:
- ✅ **Physics-Driven**: All hits transfer impulse based on Physical/Strength/Weapon stats
- ✅ **Stamina Economy**: Parrying/blocking drains stamina proportional to impulse
- ✅ **Knockback Dynamics**: Heavy hits knock opponents back, creating space/repositioning
- ✅ **Class Asymmetry**: Warriors hit hard, rogues evade, mages teleport, each with unique responses
- ✅ **Skill Mitigation**: Master level reduces knockback and stamina drain dramatically
- ✅ **Focus Flare-Ups**: Focus system enables enhanced movements and abilities

**Design Goals**:
- Create visually spectacular high-level duels (master rogue vs. master warrior)
- Reward skill progression (masters handle impulse better than novices)
- Maintain stamina as combat resource (not just HP slugfest)
- Enable tactical positioning through knockback
- Adapt mechanics for Space4X boarding actions (no visuals, same physics)

---

## Impulse Calculation

### Hit Impulse Formula

```csharp
public struct HitImpulse : IComponentData
{
    public Entity Attacker;
    public Entity Target;
    public float ImpulseValue;            // Total force transferred (0-1000+)
    public float3 ImpulseDirection;       // Normalized direction vector
    public WeaponType Weapon;
    public uint HitTick;
}

public static float CalculateHitImpulse(Entity attacker, WeaponDefinition weapon)
{
    var stats = GetComponent<EntityStats>(attacker);

    // Base impulse from physical stats
    float baseImpulse = (stats.Physical * 2f) + (stats.Strength * 3f);  // STR weighted higher

    // Weapon impulse multiplier
    float weaponMultiplier = weapon.ImpulseMultiplier;
    // Warhammer: 2.5x, Greatsword: 2.0x, Longsword: 1.5x, Dagger: 0.5x, Staff: 1.0x

    // Weapon mass contributes (heavy weapons = more impulse)
    float weaponMass = weapon.Mass;  // 0.5 (dagger) to 8.0 (warhammer)

    // Attack speed penalty for heavy weapons already factored in limb cooldowns

    // Total impulse
    float totalImpulse = baseImpulse * weaponMultiplier + weaponMass * 10f;

    return totalImpulse;
}
```

**Example Impulse Values**:

| Attacker | Physical | Strength | Weapon | Impulse | Description |
|----------|----------|----------|--------|---------|-------------|
| Novice Warrior | 30 | 40 | Longsword (1.5x, 2kg) | (30*2 + 40*3) * 1.5 + 20 = 290 | Moderate impact |
| Master Warrior | 80 | 90 | Warhammer (2.5x, 8kg) | (80*2 + 90*3) * 2.5 + 80 = 1155 | Devastating blow |
| Master Rogue | 90 | 50 | Dual Daggers (0.5x, 0.5kg) | (90*2 + 50*3) * 0.5 + 5 = 170 | Light, fast strikes |
| Master Mage | 40 | 30 | Staff (1.0x, 1.5kg) | (40*2 + 30*3) * 1.0 + 15 = 185 | Moderate, magical |

---

## Knockback Physics

### Knockback Distance and Direction

```csharp
public struct KnockbackEvent : IComponentData
{
    public Entity Target;
    public float3 KnockbackVector;        // Direction * Distance
    public float Distance;                // Meters (0-10+)
    public uint KnockbackTick;
    public bool InterruptAction;          // Knockback interrupts current action
}

public static KnockbackEvent CalculateKnockback(HitImpulse impulse, Entity target)
{
    var targetStats = GetComponent<EntityStats>(target);
    var targetMass = GetComponent<EntityMass>(target);  // Rogues light, warriors heavy

    // Base knockback distance (impulse / target mass)
    float baseDistance = impulse.ImpulseValue / (targetMass.Value * 10f);
    // Light rogue (mass 60kg): 1155 impulse / 600 = 1.925m knockback
    // Heavy warrior (mass 90kg): 1155 impulse / 900 = 1.28m knockback

    // Skill mitigation (master level reduces knockback)
    var combatSkill = GetComponent<CombatSkill>(target);
    float skillReduction = combatSkill.MasterLevel * 0.7f;  // Up to 70% reduction

    // Stance mitigation (defensive stance reduces knockback)
    var stance = GetComponent<CombatStance>(target);
    float stanceReduction = stance.IsDefensive ? 0.4f : 0f;  // 40% reduction when defensive

    // Final knockback distance
    float finalDistance = baseDistance * (1f - skillReduction) * (1f - stanceReduction);

    // Direction is from attacker to target (pushing away)
    float3 direction = math.normalize(GetPosition(target) - GetPosition(impulse.Attacker));

    return new KnockbackEvent
    {
        Target = target,
        KnockbackVector = direction * finalDistance,
        Distance = finalDistance,
        InterruptAction = finalDistance > 0.5f,  // Interrupt if knocked back >0.5m
    };
}
```

**Example Knockback Scenarios**:

1. **Master Warrior hits Novice Rogue (1155 impulse, rogue 60kg, skill 20)**:
   - Base: 1155 / 600 = 1.925m
   - Skill reduction: 20% → 0.14 (14% reduction)
   - Final: 1.925 * 0.86 = **1.66m knockback** (significant, interrupts action)

2. **Master Warrior hits Master Rogue (1155 impulse, rogue 60kg, skill 90)**:
   - Base: 1.925m
   - Skill reduction: 90% → 0.63 (63% reduction)
   - Final: 1.925 * 0.37 = **0.71m knockback** (moderate, barely interrupts)

3. **Master Warrior hits Master Warrior (1155 impulse, warrior 90kg, skill 90, defensive stance)**:
   - Base: 1155 / 900 = 1.28m
   - Skill reduction: 63%
   - Stance reduction: 40%
   - Final: 1.28 * 0.37 * 0.6 = **0.28m knockback** (minimal, no interrupt, "shrugs off")

---

## Stamina Drain from Parrying

### Parry Stamina Cost

```csharp
public struct ParryEvent : IComponentData
{
    public Entity Defender;
    public HitImpulse IncomingHit;
    public float StaminaCost;             // Stamina drained by parry
    public bool ParrySuccess;             // False if stamina insufficient
    public float KnockbackOnParry;        // Parry still causes some knockback
}

public static ParryEvent ResolveParry(Entity defender, HitImpulse hit)
{
    var defenderStamina = GetComponent<StaminaComponent>(defender);
    var combatSkill = GetComponent<CombatSkill>(defender);

    // Base stamina cost = impulse / 10
    float baseCost = hit.ImpulseValue / 10f;
    // Master warrior warhammer (1155 impulse): 115.5 stamina to parry
    // Novice warrior sword (290 impulse): 29 stamina to parry

    // Skill reduces stamina cost (up to 70% at master level)
    float skillReduction = combatSkill.ParrySkill / 100f * 0.7f;

    // Weapon weight differential (heavy weapon parries heavy hits better)
    var defenderWeapon = GetComponent<EquippedWeapon>(defender);
    float weaponRatio = defenderWeapon.Mass / 4f;  // 4kg = baseline
    float weaponBonus = math.clamp(weaponRatio, 0.5f, 1.5f);  // 50-150%

    // Final stamina cost
    float finalCost = baseCost * (1f - skillReduction) / weaponBonus;

    // Check if defender has enough stamina
    bool success = defenderStamina.CurrentStamina >= finalCost;

    if (success)
    {
        defenderStamina.CurrentStamina -= finalCost;

        // Parry still causes some knockback (20% of normal)
        float parryKnockback = CalculateKnockback(hit, defender).Distance * 0.2f;

        return new ParryEvent
        {
            Defender = defender,
            IncomingHit = hit,
            StaminaCost = finalCost,
            ParrySuccess = true,
            KnockbackOnParry = parryKnockback,
        };
    }
    else
    {
        // Parry fails, full hit connects (stamina exhausted)
        return new ParryEvent
        {
            Defender = defender,
            ParrySuccess = false,
            StaminaCost = finalCost,  // Shows how much was needed
        };
    }
}
```

**Example Parry Costs**:

| Defender | Parry Skill | Weapon | Hit Impulse | Stamina Cost | Description |
|----------|-------------|--------|-------------|--------------|-------------|
| Novice Rogue (50 stamina) | 20 | Dagger (0.5kg) | 1155 (warhammer) | 115.5 * 0.86 / 0.125 = **794 stamina** | FAIL (not enough stamina) |
| Master Rogue (150 stamina) | 90 | Longsword (2kg) | 1155 | 115.5 * 0.37 / 0.5 = **85.5 stamina** | SUCCESS (high cost but doable) |
| Master Warrior (200 stamina) | 90 | Greatsword (5kg) | 1155 | 115.5 * 0.37 / 1.25 = **34.2 stamina** | SUCCESS (easy, heavy weapon helps) |

---

## Class-Specific Responses

### Rogue: Dodge and Parry Dance

```csharp
public struct RogueCombatResponse : IComponentData
{
    public Entity Rogue;
    public float DodgeChance;             // 0-0.9 (skill-based)
    public float ParryStaminaCost;        // High cost vs. heavy hits
    public bool CanCounterAttack;         // After successful dodge/parry
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class RogueCombatSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity rogue, ref RogueCombatResponse response, ref StaminaComponent stamina, in CombatSkill skill) =>
        {
            // Incoming hit
            var incomingHit = GetIncomingHit(rogue);
            if (incomingHit == default) return;

            // Rogue decision: Dodge or Parry?
            float dodgeChance = skill.DodgeSkill / 100f * 0.9f;  // Up to 90% at master

            if (Random.NextFloat() < dodgeChance && stamina.CurrentStamina > 20f)
            {
                // DODGE: Costs 20 stamina, no damage, no knockback
                stamina.CurrentStamina -= 20f;
                response.CanCounterAttack = true;

                // Dodge animation (sidestep, backflip, roll)
                PlayDodgeAnimation(rogue, incomingHit.ImpulseDirection);
            }
            else
            {
                // PARRY: High stamina cost vs. heavy hits
                var parryEvent = ResolveParry(rogue, incomingHit);

                if (parryEvent.ParrySuccess)
                {
                    // Parried, but drained stamina
                    response.ParryStaminaCost = parryEvent.StaminaCost;
                    response.CanCounterAttack = parryEvent.StaminaCost < 50f;  // Only counter if not too drained

                    // Small knockback on parry (0.2x)
                    ApplyKnockback(rogue, parryEvent.KnockbackOnParry);
                }
                else
                {
                    // FAILED: Not enough stamina, hit connects
                    ApplyDamage(rogue, incomingHit);
                    ApplyKnockback(rogue, CalculateKnockback(incomingHit, rogue).Distance);
                }
            }

            // Master rogue can attack fast enough to exploit warrior cooldowns
            if (response.CanCounterAttack && skill.AttackSpeed > 70)
            {
                // Fast counter-attack (dual-wield daggers)
                ExecuteCounterAttack(rogue, incomingHit.Attacker);
            }

        }).Run();
    }
}
```

**Master Rogue vs. Master Warrior Duel**:
- Warrior swings warhammer (1155 impulse, 3s cooldown)
- Rogue dodges (90% success, 20 stamina) → counterattacks with dual daggers (2x attacks)
- Warrior blocks dagger strikes (low impulse, 10 stamina each)
- Rogue dodges 3-4 warrior swings, counterattacks each time (stamina 150 → 90)
- Rogue parries 5th swing (85 stamina cost, now at 5 stamina, exhausted)
- Warrior's 6th swing hits (rogue no stamina to dodge/parry)
- Rogue knocked back 1.66m, takes damage, retreats to recover stamina

---

### Mage: Teleport and Shield

```csharp
public struct MageCombatResponse : IComponentData
{
    public Entity Mage;
    public bool CanTeleport;              // Mana-gated
    public float ShieldStrength;          // 0-1 (absorbs impulse)
    public float TeleportDistance;        // Based on impulse (knocked back far)
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MageCombatSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity mage, ref MageCombatResponse response, ref ManaComponent mana, in CombatSkill skill) =>
        {
            var incomingHit = GetIncomingHit(mage);
            if (incomingHit == default) return;

            // Mage uses shield to absorb impulse
            if (response.ShieldStrength > 0f)
            {
                // Shield absorbs damage but mage gets knocked back far
                float knockbackDistance = CalculateKnockback(incomingHit, mage).Distance;

                // Shield reduces knockback slightly (20%) but mage still pushed back
                float finalKnockback = knockbackDistance * 0.8f;

                // Mage pushed back 2-5m from warrior hits (creating space)
                ApplyKnockback(mage, finalKnockback);

                // Shield strength depleted
                response.ShieldStrength -= incomingHit.ImpulseValue / 1000f;

                // Mage unscathed (no damage) but far away
            }
            else if (response.CanTeleport && mana.CurrentMana > 50f)
            {
                // TELEPORT: Blink away from hit (50 mana)
                mana.CurrentMana -= 50f;

                // Teleport in direction of knockback (predictive)
                float3 teleportDirection = CalculateKnockback(incomingHit, mage).KnockbackVector;
                float3 teleportPosition = GetPosition(mage) + teleportDirection * 3f;  // 3m blink

                SetPosition(mage, teleportPosition);

                // Teleport animation (particle effect, fade)
                PlayTeleportAnimation(mage);
            }
            else
            {
                // No shield, no mana: Hit connects
                ApplyDamage(mage, incomingHit);
                ApplyKnockback(mage, CalculateKnockback(incomingHit, mage).Distance);
            }

        }).Run();
    }
}
```

**Master Mage vs. Master Warrior Duel**:
- Warrior charges, swings warhammer
- Mage shield absorbs (no damage), knocked back 2.5m
- Mage casts fireball from distance
- Warrior tanks fireball (high HP), advances
- Mage teleports 3m away (50 mana)
- Warrior swings again, mage shield absorbs, knocked back 2.5m
- Mage kites, maintains distance, pelts with spells
- Warrior cannot close gap (mage teleports every time)
- Eventually mage runs out of mana (150 mana → 0 after 3 teleports)
- Warrior finally connects, mage knocked back 5m, takes heavy damage

---

### Warrior: Shrug Off and Counter

```csharp
public struct WarriorCombatResponse : IComponentData
{
    public Entity Warrior;
    public float ArmorReduction;          // 0-0.7 (armor reduces impulse)
    public bool CanShrugOff;              // Master warriors resist knockback
    public float CounterStrikeDamage;     // Bonus damage after tanking hit
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class WarriorCombatSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity warrior, ref WarriorCombatResponse response, in CombatSkill skill, in ArmorComponent armor) =>
        {
            var incomingHit = GetIncomingHit(warrior);
            if (incomingHit == default) return;

            // Armor reduces impulse (heavy armor = better resistance)
            float effectiveImpulse = incomingHit.ImpulseValue * (1f - response.ArmorReduction);

            // Master warriors can shrug off hits (skill-based)
            if (response.CanShrugOff && skill.MasterLevel > 0.7f)
            {
                // SHRUG OFF: Reduce knockback by 90%, take reduced damage
                float knockbackReduction = 0.9f;
                float damageReduction = 0.5f;  // Tank half damage

                float knockback = CalculateKnockback(incomingHit, warrior).Distance * (1f - knockbackReduction);
                ApplyKnockback(warrior, knockback);  // 0.28m knockback (minimal)

                ApplyDamage(warrior, incomingHit.DamageValue * (1f - damageReduction));

                // Counter-strike bonus (tanking hit empowers next attack)
                response.CounterStrikeDamage += 50f;  // +50 damage on next swing

                // Animation: Warrior plants feet, grunts, stands firm
                PlayShrugOffAnimation(warrior);
            }
            else
            {
                // Normal hit (novice warrior or non-master)
                ApplyDamage(warrior, incomingHit.DamageValue);
                ApplyKnockback(warrior, CalculateKnockback(incomingHit, warrior).Distance);
            }

        }).Run();
    }
}
```

**Master Warrior vs. Master Warrior Duel**:
- Warrior A swings warhammer (1155 impulse)
- Warrior B shrugs off (0.28m knockback, half damage, +50 counter damage)
- Warrior B counter-swings (1155 + 50 = 1205 impulse)
- Warrior A shrugs off (0.28m knockback, half damage, +50 counter)
- Duel continues with both warriors standing firm, tanking hits
- Eventually one warrior's stamina/HP depletes from sustained damage
- Dramatic "titans clashing" visual (minimal movement, heavy impacts)

---

## Focus Flare-Ups (Enhanced Abilities)

### Focus-Enhanced Movements

```csharp
public struct FocusFlareUp : IComponentData
{
    public Entity User;
    public FocusAbilityType Type;
    public float FocusCost;               // 0.5-1.0 focus bar consumed
    public uint ActivationTick;
}

public enum FocusAbilityType : byte
{
    // Rogue
    ShadowStep,          // Instant teleport behind enemy (3m)
    BladeDance,          // 5x attack speed for 3 attacks
    PerfectDodge,        // 100% dodge chance for 2s

    // Mage
    ArcaneBlast,         // Massive damage spell (200% damage)
    TimeStop,            // Freeze time for 1s (auto-dodge all attacks)
    ManaShield,          // 100% damage absorption for 5s

    // Warrior
    DestroyerStrike,     // 3x impulse attack (3465 impulse, massive knockback)
    IronWill,            // 100% knockback immunity for 5s
    Rampage,             // 2x attack speed, ignore pain for 10s
}

public static void ActivateFocusFlareUp(Entity user, FocusAbilityType ability)
{
    var focus = GetComponent<FocusComponent>(user);

    // Flare-up costs 50-100% of focus bar
    float focusCost = GetFocusCost(ability);  // 0.5-1.0

    if (focus.CurrentFocus < focusCost) return;  // Not enough focus

    focus.CurrentFocus -= focusCost;

    switch (ability)
    {
        case FocusAbilityType.ShadowStep:
            // Rogue teleports behind enemy (instant repositioning)
            var target = GetTargetEnemy(user);
            float3 behindPosition = GetPosition(target) - GetForward(target) * 2f;
            SetPosition(user, behindPosition);
            PlayShadowStepAnimation(user);
            break;

        case FocusAbilityType.DestroyerStrike:
            // Warrior 3x impulse attack (9000+ impulse, knocks enemies 5-10m back)
            var weapon = GetComponent<EquippedWeapon>(user);
            float normalImpulse = CalculateHitImpulse(user, weapon);
            float enhancedImpulse = normalImpulse * 3f;  // 1155 * 3 = 3465 impulse

            ExecuteAttack(user, enhancedImpulse);
            PlayDestroyerStrikeAnimation(user);  // Massive wind-up, ground cracks
            break;

        case FocusAbilityType.TimeStop:
            // Mage freezes time (auto-dodge all attacks for 1s)
            var timeStop = new TimeStopEffect
            {
                User = user,
                Duration = 60,  // 1s = 60 ticks
                DodgeAll = true,
            };
            ApplyStatusEffect(user, timeStop);
            PlayTimeStopAnimation(user);  // Time ripple effect, slow-mo
            break;
    }
}
```

**Focus Flare-Up Scenarios**:

1. **Rogue Shadow Step**: Warrior swings warhammer, rogue spends 0.5 focus to teleport behind warrior → backstab critical hit
2. **Warrior Destroyer Strike**: Warrior spends 1.0 focus, winds up massive swing (3465 impulse) → hits rogue → 5m knockback → rogue slammed into wall → massive damage
3. **Mage Time Stop**: Warrior charges, mage spends 0.8 focus, freezes time for 1s → warrior frozen mid-swing → mage casts arcane blast → warrior unfrozen, takes blast to face

---

## Master Level Mitigation

### Skill-Based Resistance

```csharp
public struct MasterCombatMitigation : IComponentData
{
    public float KnockbackReduction;      // 0-0.7 (70% at master level)
    public float StaminaCostReduction;    // 0-0.7 (70% cheaper parries)
    public float ImpulseAbsorption;       // 0-0.5 (50% impulse negation)
    public bool UnlockFocusFlareUps;      // Master level unlocks flare-ups
}

public static MasterCombatMitigation CalculateMasterMitigation(CombatSkill skill)
{
    float masterLevel = skill.OverallSkill / 100f;  // 0-1

    return new MasterCombatMitigation
    {
        KnockbackReduction = masterLevel * 0.7f,        // 0 (novice) to 0.7 (master)
        StaminaCostReduction = masterLevel * 0.7f,      // 70% cheaper parries at master
        ImpulseAbsorption = masterLevel * 0.5f,         // 50% impulse negated at master
        UnlockFocusFlareUps = masterLevel >= 0.7f,      // Unlocked at skill 70+
    };
}
```

**Novice vs. Master Comparison**:

| Entity | Skill | Knockback (vs. 1155 impulse) | Parry Cost | Impulse Absorbed |
|--------|-------|------------------------------|------------|------------------|
| Novice Rogue | 20 | 1.66m (full) | 794 stamina (FAIL) | 0% (takes full hit) |
| Adept Rogue | 50 | 0.96m (moderate) | 200 stamina | 25% (575 impulse negated) |
| Master Rogue | 90 | 0.71m (minimal) | 85.5 stamina | 45% (520 impulse negated) |

**Progression Feel**:
- Novice: Ragdolled by heavy hits, stamina exhausted quickly, survival difficult
- Adept: Can handle some hits, parries viable, knockback manageable
- Master: Handles devastating blows with ease, barely knocked back, fights like anime protagonist

---

## Space4X Boarding Action Adaptation

### Impulse in Boarding Combat (No Visuals)

```csharp
public struct BoardingCombatImpulse : IComponentData
{
    public Entity Attacker;
    public Entity Defender;
    public float ImpulseValue;            // Same calculation as Godgame
    public float KnockbackDistance;       // Meters (knocks crew into walls, corridors)
    public float StaminaDrain;            // Same stamina economy
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class BoardingCombatSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Same impulse/knockback mechanics, no visual presentation
        Entities.ForEach((Entity crew, ref BoardingCombatState combatState, in EntityStats stats) =>
        {
            var incomingHit = GetIncomingBoardingHit(crew);
            if (incomingHit == default) return;

            // Calculate impulse (same formula)
            float impulse = CalculateHitImpulse(incomingHit.Attacker, GetWeapon(incomingHit.Attacker));

            // Calculate knockback (same physics)
            float knockback = impulse / (GetMass(crew) * 10f);

            // Apply knockback (crew slammed into walls, bulkheads)
            ApplyBoardingKnockback(crew, knockback, incomingHit.ImpulseDirection);

            // Cramped corridors: Knockback into walls causes extra damage
            if (IsInCorridor(crew) && knockback > 1f)
            {
                // Wall impact damage (10% of impulse)
                float wallDamage = impulse * 0.1f;
                ApplyDamage(crew, wallDamage);
            }

            // Stamina drain from parrying (same mechanics)
            if (combatState.IsParrying)
            {
                var parryEvent = ResolveParry(crew, incomingHit);
                combatState.Stamina -= parryEvent.StaminaCost;
            }

        }).Run();
    }
}
```

**Boarding Action Scenarios**:

1. **Warrior Pilot Boarding**:
   - Pilot crashes into enemy carrier (kamikaze boarding)
   - Pilot survives (30% chance), inside hangar bay
   - Pilot encounters enemy marines (3 vs. 1)
   - Pilot swings warhammer (1155 impulse) → marine knocked back 2m → slams into bulkhead → 115 wall damage
   - Marines fire rifles (300 impulse each) → pilot parries (35 stamina each)
   - Pilot uses Destroyer Strike focus flare (3465 impulse) → marine knocked through corridor door → instant kill

2. **Boarding Party vs. Ship Crew**:
   - 5 marines board enemy ship (airlocks breached)
   - Ship crew (10 engineers, 2 security) defend
   - Marines have rifles (600 impulse), crew have tools/improvised weapons (200 impulse)
   - Marines knock crew back into machinery → crew take collision damage
   - Security uses focus flare (Iron Will) → immune to knockback for 5s → holds chokepoint
   - Marines stamina depletes from sustained firefight (parrying 10 crew attacks)
   - Marines retreat to recover stamina, crew holds position

---

## Integration with Limb-Based Actions

### Combined Mechanics

The Impulse system integrates with [LimbBasedActionSystem.md](LimbBasedActionSystem.md):

```csharp
// Parry with left hand while attacking with right hand (rogue dual-wield)
public void DualWieldParryAndAttack(Entity rogue)
{
    var leftHand = GetLimb(rogue, LimbType.LeftHand);
    var rightHand = GetLimb(rogue, LimbType.RightHand);

    // Left hand parries incoming warhammer (85 stamina cost)
    if (leftHand.State == LimbState.Parrying)
    {
        var parryEvent = ResolveParry(rogue, GetIncomingHit(rogue));

        // Parry successful, but high stamina cost
        if (parryEvent.ParrySuccess)
        {
            // Right hand attacks simultaneously (independent cooldown)
            if (rightHand.CurrentCooldown == 0)
            {
                ExecuteAttack(rogue, rightHand);
                rightHand.CurrentCooldown = rightHand.BaseCooldown;
            }
        }
    }
}
```

**Master Rogue Combo**:
- Left hand: Parry warrior warhammer (85 stamina)
- Right hand: Simultaneous dagger attack (170 impulse)
- Left leg: Sidestep dodge (20 stamina, repositions behind warrior)
- Right hand: Second dagger attack (backstab, 2x damage)
- Focus flare: Shadow Step → teleport behind warrior → 3rd backstab

**Result**: Rogue parries devastating blow with one hand while attacking with other hand, then teleports for final strike (anime-level spectacle).

---

## Example High-Level Duels

### Master Rogue vs. Master Warrior

**Setup**:
- Rogue: 150 stamina, 90 dodge, 90 parry, dual daggers (170 impulse each)
- Warrior: 200 stamina, 90 armor, 90 shrug-off, warhammer (1155 impulse)

**Duel Timeline**:

1. **Tick 0**: Warrior swings warhammer (1155 impulse, 3s cooldown)
2. **Tick 5**: Rogue dodges (90% success, 20 stamina → 130 stamina)
3. **Tick 10**: Rogue counterattacks 2x (dual-wield, 340 total impulse)
4. **Tick 15**: Warrior parries both daggers (10 stamina each → 180 stamina)
5. **Tick 180**: Warrior swings again (cooldown complete)
6. **Tick 185**: Rogue dodges (20 stamina → 110 stamina)
7. **Tick 190**: Rogue counterattacks 2x (340 impulse)
8. **Tick 360**: Warrior swings (3rd swing)
9. **Tick 365**: Rogue dodges (20 stamina → 90 stamina)
10. **Tick 540**: Warrior swings (4th swing)
11. **Tick 545**: Rogue dodges (20 stamina → 70 stamina)
12. **Tick 720**: Warrior swings (5th swing)
13. **Tick 725**: Rogue parries (85 stamina → stamina EXHAUSTED at 5)
14. **Tick 900**: Warrior swings (6th swing)
15. **Tick 905**: Rogue CANNOT dodge (no stamina), hit connects
16. **Tick 905**: Rogue knocked back 1.66m, takes 200 damage (HP 300 → 100)
17. **Tick 910**: Rogue uses Focus Flare: Shadow Step (0.5 focus → teleports behind warrior)
18. **Tick 915**: Rogue backstabs (critical, 3x damage, 510 impulse)
19. **Tick 920**: Warrior HP 500 → 350
20. **Tick 1080**: Warrior swings (7th swing)
21. **Tick 1085**: Rogue dodges (stamina recovered to 40, costs 20 → 20 stamina)
22. **Tick 1260**: Warrior swings (8th swing)
23. **Tick 1265**: Rogue parries (85 stamina cost, but only 20 stamina → FAIL)
24. **Tick 1265**: Rogue hit, knocked back 1.66m, takes 200 damage (HP 100 → 0, DEAD)

**Result**: Warrior wins after 8 swings, rogue depleted stamina from constant dodging/parrying.

---

### Master Mage vs. Master Warrior

**Setup**:
- Mage: 150 mana, 90 teleport, shield 1.0 strength, fireball (400 impulse)
- Warrior: 200 stamina, 90 armor, 90 shrug-off, warhammer (1155 impulse)

**Duel Timeline**:

1. **Tick 0**: Warrior charges, swings warhammer (1155 impulse)
2. **Tick 5**: Mage shield absorbs (no damage), knocked back 2.5m
3. **Tick 10**: Mage casts fireball (400 impulse, 100 damage)
4. **Tick 15**: Warrior tanks fireball (HP 500 → 400), advances
5. **Tick 180**: Warrior swings (2nd swing)
6. **Tick 185**: Mage teleports 3m away (50 mana → 100 mana)
7. **Tick 190**: Mage casts fireball (100 damage)
8. **Tick 195**: Warrior HP 400 → 300
9. **Tick 360**: Warrior closes gap, swings (3rd swing)
10. **Tick 365**: Mage teleports 3m away (50 mana → 50 mana)
11. **Tick 370**: Mage casts fireball (100 damage)
12. **Tick 375**: Warrior HP 300 → 200
13. **Tick 540**: Warrior swings (4th swing)
14. **Tick 545**: Mage CANNOT teleport (only 50 mana, needs 50 for emergency)
15. **Tick 545**: Mage shield absorbs (strength 0.5 → 0.3), knocked back 2.5m
16. **Tick 550**: Mage uses Focus Flare: Time Stop (0.8 focus, freeze time 1s)
17. **Tick 550-610**: Warrior frozen, mage casts Arcane Blast (800 damage, 100 mana → 0 mana)
18. **Tick 610**: Warrior unfrozen, takes blast (HP 200 → 0, DEAD)

**Result**: Mage wins by kiting, teleporting, and using Time Stop finisher.

---

## Summary

The **Impulse and Knockback Combat** system creates:

1. **Physics-Driven Combat**: Every hit transfers impulse (Physical + Strength + Weapon), causing knockback and stamina drain
2. **Class Asymmetry**: Warriors hit hard (1155 impulse), rogues dodge/parry (high stamina cost), mages teleport/shield (mana-gated)
3. **Stamina Economy**: Parrying costs stamina proportional to impulse (85 stamina for warhammer parry), exhaustion leads to vulnerability
4. **Knockback Dynamics**: Heavy hits knock opponents 1-5m, creating space and repositioning
5. **Master Mitigation**: Skill progression reduces knockback (70%), stamina cost (70%), and enables focus flare-ups
6. **Focus Flare-Ups**: Enhanced abilities (Shadow Step, Destroyer Strike, Time Stop) consume focus for spectacular moves
7. **Anime-Style Duels**: Master-level fights feature dodges, teleports, knockback resistance, and dramatic finishers
8. **Space4X Boarding**: Same mechanics apply to crew combat (knockback into walls, stamina drain, no visuals)

**Next Steps**:
- Prototype knockback physics (distance, direction, wall collision)
- Balance stamina costs (ensure rogues can parry ~4-5 warrior swings before exhaustion)
- Design focus flare-up animations (shadow step, time stop, destroyer strike)
- Integrate with limb-based actions (parry with left, attack with right)
- Test high-level duels (ensure spectacular feel, not boring HP sponges)

---

**Related Documents**:
- [LimbBasedActionSystem.md](LimbBasedActionSystem.md) - Independent limb actions
- [EnvironmentalQuestsAndLootVectors.md](EnvironmentalQuestsAndLootVectors.md) - Quest system
- [PatternBible.md](../PatternBible.md) - Emergent patterns

**Design Lead**: [TBD]
**Technical Lead**: [TBD]
**Last Review**: 2025-11-30
