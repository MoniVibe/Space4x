using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Type of weapon system.
    /// </summary>
    public enum WeaponType : byte
    {
        /// <summary>
        /// Energy beam - instant hit, shield-effective.
        /// </summary>
        Laser = 0,

        /// <summary>
        /// Physical projectile - armor-piercing.
        /// </summary>
        Kinetic = 1,

        /// <summary>
        /// Guided projectile - medium range, versatile.
        /// </summary>
        Missile = 2,

        /// <summary>
        /// Heavy ordnance - anti-capital, slow.
        /// </summary>
        Torpedo = 3,

        /// <summary>
        /// Point defense - anti-fighter/missile.
        /// </summary>
        PointDefense = 4,

        /// <summary>
        /// Area effect - damages multiple targets.
        /// </summary>
        Flak = 5,

        /// <summary>
        /// Disables systems without destroying.
        /// </summary>
        Ion = 6,

        /// <summary>
        /// Ignores shields, damages hull directly.
        /// </summary>
        Plasma = 7
    }

    /// <summary>
    /// Weapon mount size affecting stats.
    /// </summary>
    public enum WeaponSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        Capital = 3
    }

    [Flags]
    public enum WeaponFlags : byte
    {
        None = 0,
        AntiSubsystem = 1 << 0
    }

    /// <summary>
    /// Weapon system component.
    /// </summary>
    public struct Space4XWeapon : IComponentData
    {
        /// <summary>
        /// Weapon type.
        /// </summary>
        public WeaponType Type;

        /// <summary>
        /// Mount size.
        /// </summary>
        public WeaponSize Size;

        /// <summary>
        /// Special weapon flags.
        /// </summary>
        public WeaponFlags Flags;

        /// <summary>
        /// Base damage per hit.
        /// </summary>
        public float BaseDamage;

        /// <summary>
        /// Optimal engagement range.
        /// </summary>
        public float OptimalRange;

        /// <summary>
        /// Maximum engagement range.
        /// </summary>
        public float MaxRange;

        /// <summary>
        /// Base accuracy [0, 1].
        /// </summary>
        public half BaseAccuracy;

        /// <summary>
        /// Ticks between shots.
        /// </summary>
        public ushort CooldownTicks;

        /// <summary>
        /// Current cooldown remaining.
        /// </summary>
        public ushort CurrentCooldown;

        /// <summary>
        /// Ammunition per shot (0 = energy weapon).
        /// </summary>
        public byte AmmoPerShot;

        /// <summary>
        /// Shield damage modifier.
        /// </summary>
        public half ShieldModifier;

        /// <summary>
        /// Armor penetration value.
        /// </summary>
        public half ArmorPenetration;

        public static Space4XWeapon Laser(WeaponSize size) => new Space4XWeapon
        {
            Type = WeaponType.Laser,
            Size = size,
            Flags = WeaponFlags.None,
            BaseDamage = 10f * (1 + (int)size),
            OptimalRange = 300f + 100f * (int)size,
            MaxRange = 500f + 150f * (int)size,
            BaseAccuracy = (half)0.85f,
            CooldownTicks = (ushort)(10 + 5 * (int)size),
            CurrentCooldown = 0,
            AmmoPerShot = 0,
            ShieldModifier = (half)1.5f,
            ArmorPenetration = (half)0.3f
        };

        public static Space4XWeapon Kinetic(WeaponSize size) => new Space4XWeapon
        {
            Type = WeaponType.Kinetic,
            Size = size,
            Flags = WeaponFlags.None,
            BaseDamage = 15f * (1 + (int)size),
            OptimalRange = 200f + 80f * (int)size,
            MaxRange = 400f + 120f * (int)size,
            BaseAccuracy = (half)0.75f,
            CooldownTicks = (ushort)(8 + 4 * (int)size),
            CurrentCooldown = 0,
            AmmoPerShot = 1,
            ShieldModifier = (half)0.5f,
            ArmorPenetration = (half)0.8f
        };

        public static Space4XWeapon Missile(WeaponSize size) => new Space4XWeapon
        {
            Type = WeaponType.Missile,
            Size = size,
            Flags = WeaponFlags.None,
            BaseDamage = 25f * (1 + (int)size),
            OptimalRange = 400f + 150f * (int)size,
            MaxRange = 800f + 200f * (int)size,
            BaseAccuracy = (half)0.9f,
            CooldownTicks = (ushort)(30 + 10 * (int)size),
            CurrentCooldown = 0,
            AmmoPerShot = 1,
            ShieldModifier = (half)1.0f,
            ArmorPenetration = (half)0.6f
        };

        public static Space4XWeapon Torpedo(WeaponSize size) => new Space4XWeapon
        {
            Type = WeaponType.Torpedo,
            Size = size,
            Flags = WeaponFlags.None,
            BaseDamage = 100f * (1 + (int)size),
            OptimalRange = 200f + 100f * (int)size,
            MaxRange = 500f + 150f * (int)size,
            BaseAccuracy = (half)0.7f,
            CooldownTicks = (ushort)(60 + 20 * (int)size),
            CurrentCooldown = 0,
            AmmoPerShot = 1,
            ShieldModifier = (half)0.8f,
            ArmorPenetration = (half)1.2f
        };
    }

    /// <summary>
    /// Buffer for multiple weapon mounts.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct WeaponMount : IBufferElementData
    {
        public Space4XWeapon Weapon;
        public Entity CurrentTarget;
        public byte IsEnabled;
    }

    public struct Space4XWeaponTuningConfig : IComponentData
    {
        public float ProjectileSpeedMultiplier;

        public static Space4XWeaponTuningConfig Default => new Space4XWeaponTuningConfig
        {
            ProjectileSpeedMultiplier = 1f
        };
    }

    /// <summary>
    /// Shield type affecting resistances.
    /// </summary>
    public enum ShieldType : byte
    {
        Standard = 0,
        Hardened = 1,    // Better vs kinetic
        Dispersive = 2,  // Better vs energy
        Adaptive = 3     // Balanced, learns
    }

    /// <summary>
    /// Energy shield state.
    /// </summary>
    public struct Space4XShield : IComponentData
    {
        /// <summary>
        /// Shield type.
        /// </summary>
        public ShieldType Type;

        /// <summary>
        /// Current shield strength.
        /// </summary>
        public float Current;

        /// <summary>
        /// Maximum shield capacity.
        /// </summary>
        public float Maximum;

        /// <summary>
        /// Shield regeneration per tick.
        /// </summary>
        public float RechargeRate;

        /// <summary>
        /// Ticks until recharge starts after damage.
        /// </summary>
        public ushort RechargeDelay;

        /// <summary>
        /// Current delay countdown.
        /// </summary>
        public ushort CurrentDelay;

        /// <summary>
        /// Energy resistance modifier.
        /// </summary>
        public half EnergyResistance;

        /// <summary>
        /// Kinetic resistance modifier.
        /// </summary>
        public half KineticResistance;

        /// <summary>
        /// Explosive resistance modifier.
        /// </summary>
        public half ExplosiveResistance;

        public float Ratio => Maximum > 0 ? Current / Maximum : 0;

        public static Space4XShield Standard(float capacity) => new Space4XShield
        {
            Type = ShieldType.Standard,
            Current = capacity,
            Maximum = capacity,
            RechargeRate = capacity * 0.01f,
            RechargeDelay = 30,
            CurrentDelay = 0,
            EnergyResistance = (half)1.0f,
            KineticResistance = (half)1.0f,
            ExplosiveResistance = (half)1.0f
        };

        public static Space4XShield Hardened(float capacity) => new Space4XShield
        {
            Type = ShieldType.Hardened,
            Current = capacity,
            Maximum = capacity,
            RechargeRate = capacity * 0.008f,
            RechargeDelay = 40,
            CurrentDelay = 0,
            EnergyResistance = (half)0.8f,
            KineticResistance = (half)1.4f,
            ExplosiveResistance = (half)1.2f
        };

        public static Space4XShield Dispersive(float capacity) => new Space4XShield
        {
            Type = ShieldType.Dispersive,
            Current = capacity,
            Maximum = capacity,
            RechargeRate = capacity * 0.015f,
            RechargeDelay = 20,
            CurrentDelay = 0,
            EnergyResistance = (half)1.5f,
            KineticResistance = (half)0.7f,
            ExplosiveResistance = (half)0.9f
        };
    }

    /// <summary>
    /// Armor type affecting resistances.
    /// </summary>
    public enum ArmorType : byte
    {
        Standard = 0,
        Reactive = 1,    // Better vs explosive
        Ablative = 2,    // Better vs energy
        Composite = 3    // Balanced
    }

    /// <summary>
    /// Hull armor state.
    /// </summary>
    public struct Space4XArmor : IComponentData
    {
        /// <summary>
        /// Armor type.
        /// </summary>
        public ArmorType Type;

        /// <summary>
        /// Armor thickness (damage reduction).
        /// </summary>
        public float Thickness;

        /// <summary>
        /// Minimum penetration needed to damage hull.
        /// </summary>
        public half PenetrationThreshold;

        /// <summary>
        /// Energy resistance modifier.
        /// </summary>
        public half EnergyResistance;

        /// <summary>
        /// Kinetic resistance modifier.
        /// </summary>
        public half KineticResistance;

        /// <summary>
        /// Explosive resistance modifier.
        /// </summary>
        public half ExplosiveResistance;

        public static Space4XArmor Standard(float thickness) => new Space4XArmor
        {
            Type = ArmorType.Standard,
            Thickness = thickness,
            PenetrationThreshold = (half)0.3f,
            EnergyResistance = (half)1.0f,
            KineticResistance = (half)1.0f,
            ExplosiveResistance = (half)1.0f
        };

        public static Space4XArmor Reactive(float thickness) => new Space4XArmor
        {
            Type = ArmorType.Reactive,
            Thickness = thickness,
            PenetrationThreshold = (half)0.4f,
            EnergyResistance = (half)0.9f,
            KineticResistance = (half)0.8f,
            ExplosiveResistance = (half)1.5f
        };

        public static Space4XArmor Ablative(float thickness) => new Space4XArmor
        {
            Type = ArmorType.Ablative,
            Thickness = thickness,
            PenetrationThreshold = (half)0.25f,
            EnergyResistance = (half)1.4f,
            KineticResistance = (half)1.1f,
            ExplosiveResistance = (half)0.8f
        };
    }

    /// <summary>
    /// Combat engagement phase.
    /// </summary>
    public enum EngagementPhase : byte
    {
        None = 0,
        Approaching = 1,
        Engaged = 2,
        Retreating = 3,
        Destroyed = 4,
        Disabled = 5,
        Victorious = 6
    }

    /// <summary>
    /// Combat engagement context.
    /// </summary>
    public struct Space4XEngagement : IComponentData
    {
        /// <summary>
        /// Primary target entity.
        /// </summary>
        public Entity PrimaryTarget;

        /// <summary>
        /// Current engagement phase.
        /// </summary>
        public EngagementPhase Phase;

        /// <summary>
        /// Distance to primary target.
        /// </summary>
        public float TargetDistance;

        /// <summary>
        /// Ticks engaged in combat.
        /// </summary>
        public uint EngagementDuration;

        /// <summary>
        /// Total damage dealt this engagement.
        /// </summary>
        public float DamageDealt;

        /// <summary>
        /// Total damage received this engagement.
        /// </summary>
        public float DamageReceived;

        /// <summary>
        /// Formation bonus modifier [0, 1].
        /// </summary>
        public half FormationBonus;

        /// <summary>
        /// Evasion modifier from maneuvers.
        /// </summary>
        public half EvasionModifier;
    }

    /// <summary>
    /// Damage event for tracking combat results.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DamageEvent : IBufferElementData
    {
        /// <summary>
        /// Source of damage.
        /// </summary>
        public Entity Source;

        /// <summary>
        /// Weapon type that caused damage.
        /// </summary>
        public WeaponType WeaponType;

        /// <summary>
        /// Raw damage before mitigation.
        /// </summary>
        public float RawDamage;

        /// <summary>
        /// Damage absorbed by shields.
        /// </summary>
        public float ShieldDamage;

        /// <summary>
        /// Damage absorbed by armor.
        /// </summary>
        public float ArmorDamage;

        /// <summary>
        /// Damage applied to hull.
        /// </summary>
        public float HullDamage;

        /// <summary>
        /// Tick when damage occurred.
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Whether this was a critical hit.
        /// </summary>
        public byte IsCritical;
    }

    public enum SubsystemType : byte
    {
        Engines = 0,
        Weapons = 1
    }

    [Flags]
    public enum SubsystemFlags : byte
    {
        None = 0,
        Inherent = 1 << 0,
        Destroyed = 1 << 1
    }

    [InternalBufferCapacity(2)]
    public struct SubsystemHealth : IBufferElementData
    {
        public SubsystemType Type;
        public float Current;
        public float Max;
        public float RegenPerTick;
        public SubsystemFlags Flags;
    }

    [InternalBufferCapacity(2)]
    public struct SubsystemDisabled : IBufferElementData
    {
        public SubsystemType Type;
        public uint UntilTick;
    }

    [InternalBufferCapacity(8)]
    public struct DamageScarEvent : IBufferElementData
    {
        public int3 LocalPositionQ;
        public int3 NormalQ;
        public byte Intensity;
        public byte ScarType;
        public uint Tick;
    }

    public struct SubsystemTargetDirective : IComponentData
    {
        public SubsystemType TargetSubsystem;
    }

    /// <summary>
    /// Tag for entities currently in combat.
    /// </summary>
    public struct InCombatTag : IComponentData { }

    /// <summary>
    /// Combat math utilities (candidates for PureDOTS extraction).
    /// </summary>
    public static class CombatMath
    {
        /// <summary>
        /// Calculates hit probability based on accuracy, evasion, and range.
        /// </summary>
        public static float CalculateHitChance(float baseAccuracy, float evasion, float distance, float optimalRange, float maxRange)
        {
            // Range falloff
            float rangeFactor = 1f;
            if (distance > optimalRange)
            {
                float falloff = (distance - optimalRange) / (maxRange - optimalRange);
                rangeFactor = 1f - math.saturate(falloff) * 0.5f;
            }

            // Evasion reduction
            float evasionFactor = 1f - math.saturate(evasion);

            return math.saturate(baseAccuracy * rangeFactor * evasionFactor);
        }

        /// <summary>
        /// Calculates damage after shield mitigation.
        /// </summary>
        public static float CalculateShieldDamage(float rawDamage, float shieldModifier, float resistance)
        {
            return rawDamage * shieldModifier * (2f - resistance);
        }

        /// <summary>
        /// Calculates damage after armor mitigation.
        /// </summary>
        public static float CalculateArmorDamage(float damage, float armorThickness, float penetration, float resistance)
        {
            // Penetration check
            if (penetration < armorThickness * 0.5f)
            {
                return damage * 0.1f; // Glancing blow
            }

            float penetrationRatio = math.saturate(penetration / armorThickness);
            float reduction = (1f - penetrationRatio) * resistance;

            return damage * (1f - math.saturate(reduction));
        }

        /// <summary>
        /// Determines if a hit is critical.
        /// </summary>
        public static bool RollCritical(float baseCritChance, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            return random.NextFloat() < baseCritChance;
        }

        /// <summary>
        /// Calculates formation bonus based on cohesion and stance.
        /// </summary>
        public static float CalculateFormationBonus(float cohesion, VesselStanceMode stance)
        {
            float stanceMod = stance switch
            {
                VesselStanceMode.Aggressive => 0.8f,
                VesselStanceMode.Balanced => 1.0f,
                VesselStanceMode.Defensive => 1.2f,
                VesselStanceMode.Evasive => 0.6f,
                _ => 1.0f
            };

            return cohesion * stanceMod * 0.2f; // Max 20% bonus
        }

        /// <summary>
        /// Gets resistance modifier for weapon type.
        /// </summary>
        public static float GetWeaponResistance(WeaponType weaponType, in Space4XShield shield)
        {
            return weaponType switch
            {
                WeaponType.Laser => (float)shield.EnergyResistance,
                WeaponType.Ion => (float)shield.EnergyResistance,
                WeaponType.Kinetic => (float)shield.KineticResistance,
                WeaponType.PointDefense => (float)shield.KineticResistance,
                WeaponType.Missile => (float)shield.ExplosiveResistance,
                WeaponType.Torpedo => (float)shield.ExplosiveResistance,
                WeaponType.Flak => (float)shield.ExplosiveResistance,
                WeaponType.Plasma => 0.5f, // Plasma partially bypasses shields
                _ => 1.0f
            };
        }

        /// <summary>
        /// Gets armor resistance modifier for weapon type.
        /// </summary>
        public static float GetArmorResistance(WeaponType weaponType, in Space4XArmor armor)
        {
            return weaponType switch
            {
                WeaponType.Laser => (float)armor.EnergyResistance,
                WeaponType.Ion => (float)armor.EnergyResistance * 0.5f,
                WeaponType.Kinetic => (float)armor.KineticResistance,
                WeaponType.PointDefense => (float)armor.KineticResistance,
                WeaponType.Missile => (float)armor.ExplosiveResistance,
                WeaponType.Torpedo => (float)armor.ExplosiveResistance,
                WeaponType.Flak => (float)armor.ExplosiveResistance,
                WeaponType.Plasma => (float)armor.EnergyResistance * 0.7f,
                _ => 1.0f
            };
        }
    }
}
