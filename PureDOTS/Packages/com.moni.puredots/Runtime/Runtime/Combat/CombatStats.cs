using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Base attributes component - experience modifiers that calculate derived stats.
    /// These are the foundation for combat calculations.
    /// </summary>
    public struct BaseAttributes : IComponentData
    {
        public byte Strength;      // 0-100 (physical power)
        public byte Finesse;       // 0-100 (skill, agility)
        public byte Will;          // 0-100 (mental fortitude)
        public byte Intelligence;  // 0-100 (for magic users)
    }
    
    /// <summary>
    /// Derived combat stats component - calculated from base attributes.
    /// These are the actual stats used in combat resolution.
    /// </summary>
    public struct CombatStats : IComponentData
    {
        // Derived stats (calculated from base attributes)
        public byte Attack;              // To-hit chance (Finesse × 0.7 + Strength × 0.3)
        public byte Defense;             // Dodge/block (Finesse × 0.6 + armor)
        public byte Morale;             // Yield threshold (Will × 0.7 + personality)
        public byte AttackSpeed;         // Attacks per round (Finesse × 0.8)
        public byte AttackDamage;        // Raw damage (Strength × 0.5 + weapon)
        public byte Accuracy;            // Hit precision (Finesse × 0.9)
        public byte CriticalChance;      // 0-100 (Finesse / 5 + weapon)
        
        public ushort Health;            // Max HP (Strength × 0.6 + Will × 0.4 + 50)
        public ushort CurrentHealth;     // Current HP (0 = death)
        public byte Stamina;            // Rounds before exhaustion (Strength / 10)
        public byte CurrentStamina;     // Current stamina
        
        // Magic stats (for magic users)
        public byte SpellPower;          // Magic damage (Will × 0.8 + Int × 0.2)
        public byte ManaPool;           // Max mana (Will × 0.5 + Int × 0.5)
        public byte CurrentMana;        // Current mana
        
        // Equipment modifiers (applied to derived stats)
        public Entity EquippedWeapon;    // Weapon entity (or Entity.Null)
        public Entity EquippedArmor;     // Armor entity (or Entity.Null)
        public Entity EquippedShield;    // Shield entity (or Entity.Null)
        
        // Combat state
        public ushort CombatExperience;  // Total combats survived
        public bool IsInCombat;          // Currently fighting
        public Entity CurrentOpponent;    // Who they're fighting (or Entity.Null)
    }
    
    /// <summary>
    /// Weapon component - attached to weapon entities.
    /// </summary>
    public struct Weapon : IComponentData
    {
        public enum WeaponType : byte
        {
            Sword,
            Dagger,
            Mace,
            Axe,
            Spear,
            Bow,
            Staff,
            Unarmed
        }
        
        public WeaponType Type;
        public byte BaseDamage;              // 5-50 damage
        public sbyte HitBonus;               // -10 to +15 hit chance
        public byte ArmorPenetration;        // 0-100 (% of armor ignored)
        public byte CriticalChanceBonus;     // 0-20 (% bonus crit)
        
        public ushort Durability;            // 0-1000 (breaks at 0)
        public ushort MaxDurability;
        public ushort Value;                 // Currency worth
        public FixedString64Bytes WeaponName; // "Legendary Blade", "Rusty Dagger"
    }
    
    /// <summary>
    /// Armor component - attached to armor entities.
    /// </summary>
    public struct Armor : IComponentData
    {
        public enum ArmorType : byte
        {
            None,
            Leather,
            Chainmail,
            Plate,
            Shield
        }
        
        public ArmorType Type;
        public byte ArmorValue;              // 5-50 armor
        public sbyte DodgePenalty;           // -15 to +0 dodge
        public sbyte StaminaDrain;           // -2 to +0 rounds
        
        // Effectiveness vs weapon types (0.0-1.0)
        public float EffectivenessVsSword;   // 0.7 for plate
        public float EffectivenessVsMace;    // 0.4 for plate
        public float EffectivenessVsArrow;   // 0.3 for plate
        
        public ushort Durability;
        public ushort MaxDurability;
        public ushort Value;
        public FixedString64Bytes ArmorName;
    }
    
    /// <summary>
    /// Active combat component - tracks ongoing fight.
    /// </summary>
    public struct ActiveCombat : IComponentData
    {
        public enum CombatType : byte
        {
            Duel,
            Brawl,
            Assassination,
            TrialByCombat
        }
        
        public enum CombatStance : byte
        {
            Aggressive,
            Defensive,
            Balanced,
            Reckless
        }
        
        public CombatType Type;
        public Entity Combatant1;
        public Entity Combatant2;
        public uint CombatStartTick;
        public byte CurrentRound;
        
        public CombatStance Combatant1Stance;
        public CombatStance Combatant2Stance;
        
        public byte Combatant1Damage;        // Damage dealt this round
        public byte Combatant2Damage;
        
        public bool IsDuelToFirstBlood;     // True if duel ends at first injury
        public bool IsDuelToYield;          // True if duel ends on yield
        public bool IsDuelToDeath;          // True if duel ends on death
        
        public Entity WitnessEntity;         // Judge/witness (for trials)
        public ushort WitnessCount;          // Spectators (for duels)
    }
    
    /// <summary>
    /// Injury component - tracks permanent injuries.
    /// </summary>
    public struct Injury : IBufferElementData
    {
        public enum InjuryType : byte
        {
            LostEye,
            CrippledArm,
            CrippledLeg,
            BrokenRibs,
            FacialScars,
            LostFingers,
            InternalBleeding,
            TraumaticBrainInjury
        }
        
        public InjuryType Type;
        public uint InjuryTick;              // When injury occurred
        public sbyte StatPenalty;            // -30 to -5 (stat reduction)
        public FixedString64Bytes Description; // "Lost left eye in duel with Baron"
    }
    
    /// <summary>
    /// Death saving throw component - active when HP reaches 0.
    /// </summary>
    public struct DeathSavingThrow : IComponentData
    {
        public byte SurvivalChance;         // Will × 0.5 (%)
        public bool AlliesPresent;          // +10% if true
        public bool MedicalTreatment;       // +20% if true
        public bool ExecutionAttempt;       // -100% if true (auto-death)
        
        public bool RollSuccessful;         // True if survived
        public bool PermanentInjuryRolled;  // True if injury assigned
    }
    
    /// <summary>
    /// Combat AI component - determines stance selection and behavior.
    /// </summary>
    public struct CombatAI : IComponentData
    {
        public sbyte Aggression;            // -50 (defensive) to +50 (reckless)
        public byte PreferredStance;        // Default stance (alignment-based)
        
        // Thresholds for AI decisions
        public byte FleeThresholdHP;        // Flee if HP < this (Craven = 50, Bold = 0)
        public byte YieldThresholdHP;       // Yield if HP < this (Forgiving = 40, Vengeful = 0)
        
        public bool PrefersNonLethal;       // True if Good alignment (spare enemies)
        public bool ExecutesPrisoners;       // True if Evil alignment (no mercy)
    }
}

