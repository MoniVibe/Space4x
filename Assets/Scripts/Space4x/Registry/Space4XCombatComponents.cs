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
    /// High-level weapon family (damage channel focus).
    /// </summary>
    public enum WeaponFamily : byte
    {
        Unknown = 0,
        Energy = 1,
        Kinetic = 2,
        Explosive = 3
    }

    /// <summary>
    /// Delivery mechanism for a weapon payload.
    /// </summary>
    public enum WeaponDelivery : byte
    {
        Unknown = 0,
        Beam = 1,
        Slug = 2,
        Guided = 3,
        Bus = 4,
        Field = 5,
        Area = 6,
        Cloud = 7,
        Burst = 8
    }

    /// <summary>
    /// Normalized damage types for Space4X ship combat.
    /// </summary>
    public enum Space4XDamageType : byte
    {
        Unknown = 0,
        Energy = 1,
        Thermal = 2,
        EM = 3,
        Radiation = 4,
        Kinetic = 5,
        Explosive = 6,
        Caustic = 7
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
        /// Weapon family for coarse classification.
        /// </summary>
        public WeaponFamily Family;

        /// <summary>
        /// Damage type for resistances.
        /// </summary>
        public Space4XDamageType DamageType;

        /// <summary>
        /// Delivery mechanism (beam, guided, bus, etc.).
        /// </summary>
        public WeaponDelivery Delivery;

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
        /// Fire arc width in degrees (0 = use default or no constraint).
        /// </summary>
        public float FireArcDegrees;

        /// <summary>
        /// Base accuracy [0, 1].
        /// </summary>
        public half BaseAccuracy;

        /// <summary>
        /// Tracking rating [0, 1]. Higher = better at tracking fast targets.
        /// </summary>
        public half Tracking;

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
            Family = WeaponFamily.Energy,
            DamageType = Space4XDamageType.Energy,
            Delivery = WeaponDelivery.Beam,
            Flags = WeaponFlags.None,
            BaseDamage = 10f * (1 + (int)size),
            OptimalRange = 300f + 100f * (int)size,
            MaxRange = 500f + 150f * (int)size,
            BaseAccuracy = (half)0.85f,
            Tracking = (half)ResolveTracking(WeaponType.Laser, size),
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
            Family = WeaponFamily.Kinetic,
            DamageType = Space4XDamageType.Kinetic,
            Delivery = WeaponDelivery.Slug,
            Flags = WeaponFlags.None,
            BaseDamage = 15f * (1 + (int)size),
            OptimalRange = 200f + 80f * (int)size,
            MaxRange = 400f + 120f * (int)size,
            BaseAccuracy = (half)0.75f,
            Tracking = (half)ResolveTracking(WeaponType.Kinetic, size),
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
            Family = WeaponFamily.Explosive,
            DamageType = Space4XDamageType.Explosive,
            Delivery = WeaponDelivery.Guided,
            Flags = WeaponFlags.None,
            BaseDamage = 25f * (1 + (int)size),
            OptimalRange = 400f + 150f * (int)size,
            MaxRange = 800f + 200f * (int)size,
            BaseAccuracy = (half)0.9f,
            Tracking = (half)ResolveTracking(WeaponType.Missile, size),
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
            Family = WeaponFamily.Explosive,
            DamageType = Space4XDamageType.Explosive,
            Delivery = WeaponDelivery.Bus,
            Flags = WeaponFlags.None,
            BaseDamage = 100f * (1 + (int)size),
            OptimalRange = 200f + 100f * (int)size,
            MaxRange = 500f + 150f * (int)size,
            BaseAccuracy = (half)0.7f,
            Tracking = (half)ResolveTracking(WeaponType.Torpedo, size),
            CooldownTicks = (ushort)(60 + 20 * (int)size),
            CurrentCooldown = 0,
            AmmoPerShot = 1,
            ShieldModifier = (half)0.8f,
            ArmorPenetration = (half)1.2f
        };

        public static WeaponFamily ResolveFamily(WeaponType type)
        {
            return type switch
            {
                WeaponType.Laser => WeaponFamily.Energy,
                WeaponType.Ion => WeaponFamily.Energy,
                WeaponType.Plasma => WeaponFamily.Energy,
                WeaponType.Kinetic => WeaponFamily.Kinetic,
                WeaponType.PointDefense => WeaponFamily.Kinetic,
                WeaponType.Missile => WeaponFamily.Explosive,
                WeaponType.Torpedo => WeaponFamily.Explosive,
                WeaponType.Flak => WeaponFamily.Explosive,
                _ => WeaponFamily.Unknown
            };
        }

        public static WeaponDelivery ResolveDelivery(WeaponType type)
        {
            return type switch
            {
                WeaponType.Laser => WeaponDelivery.Beam,
                WeaponType.Ion => WeaponDelivery.Beam,
                WeaponType.Plasma => WeaponDelivery.Beam,
                WeaponType.Kinetic => WeaponDelivery.Slug,
                WeaponType.PointDefense => WeaponDelivery.Slug,
                WeaponType.Missile => WeaponDelivery.Guided,
                WeaponType.Torpedo => WeaponDelivery.Bus,
                WeaponType.Flak => WeaponDelivery.Area,
                _ => WeaponDelivery.Unknown
            };
        }

        public static Space4XDamageType ResolveDamageType(WeaponType type)
        {
            return type switch
            {
                WeaponType.Laser => Space4XDamageType.Energy,
                WeaponType.Ion => Space4XDamageType.EM,
                WeaponType.Plasma => Space4XDamageType.Thermal,
                WeaponType.Kinetic => Space4XDamageType.Kinetic,
                WeaponType.PointDefense => Space4XDamageType.Kinetic,
                WeaponType.Missile => Space4XDamageType.Explosive,
                WeaponType.Torpedo => Space4XDamageType.Explosive,
                WeaponType.Flak => Space4XDamageType.Explosive,
                _ => Space4XDamageType.Unknown
            };
        }

        public static Space4XDamageType ResolveDamageType(WeaponType type, Space4XDamageType overrideType)
        {
            if (overrideType != Space4XDamageType.Unknown)
            {
                return overrideType;
            }

            return ResolveDamageType(type);
        }

        public static float ResolveTracking(in Space4XWeapon weapon)
        {
            if (weapon.Tracking > 0f)
            {
                return weapon.Tracking;
            }

            return ResolveTracking(weapon.Type, weapon.Size);
        }

        public static float ResolveTracking(WeaponType type, WeaponSize size)
        {
            var baseTracking = type switch
            {
                WeaponType.PointDefense => 0.95f,
                WeaponType.Flak => 0.85f,
                WeaponType.Laser => 0.8f,
                WeaponType.Ion => 0.8f,
                WeaponType.Plasma => 0.75f,
                WeaponType.Kinetic => 0.65f,
                WeaponType.Missile => 0.55f,
                WeaponType.Torpedo => 0.4f,
                _ => 0.6f
            };

            var sizeScale = size switch
            {
                WeaponSize.Small => 1.1f,
                WeaponSize.Medium => 1.0f,
                WeaponSize.Large => 0.9f,
                WeaponSize.Capital => 0.8f,
                _ => 1f
            };

            return math.saturate(baseTracking * sizeScale);
        }
    }

    /// <summary>
    /// Optional weapon effects (status channels). Keep empty for MVP.
    /// </summary>
    public enum WeaponEffectType : byte
    {
        Unknown = 0,
        EMP = 1,
        ShieldSuppression = 2,
        ArmorBreach = 3,
        SensorBlind = 4,
        Heat = 5,
        Nanite = 6
    }

    [InternalBufferCapacity(2)]
    public struct WeaponEffectOp : IBufferElementData
    {
        public WeaponEffectType Type;
        public float Magnitude;
        public ushort DurationTicks;
    }

    /// <summary>
    /// Buffer for multiple weapon mounts.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct WeaponMount : IBufferElementData
    {
        public Space4XWeapon Weapon;
        public Entity CurrentTarget;
        public half FireArcCenterOffsetDeg;
        public byte IsEnabled;
        public uint ShotsFired;
        public uint ShotsHit;
        public Entity SourceModule;
        public half CoolingRating;
        public float Heat01;
        public float HeatCapacity;
        public float HeatDissipation;
        public float HeatPerShot;
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
        /// Thermal resistance modifier.
        /// </summary>
        public half ThermalResistance;

        /// <summary>
        /// Electromagnetic (EMP) resistance modifier.
        /// </summary>
        public half EMResistance;

        /// <summary>
        /// Radiation resistance modifier.
        /// </summary>
        public half RadiationResistance;

        /// <summary>
        /// Kinetic resistance modifier.
        /// </summary>
        public half KineticResistance;

        /// <summary>
        /// Explosive resistance modifier.
        /// </summary>
        public half ExplosiveResistance;

        /// <summary>
        /// Caustic resistance modifier.
        /// </summary>
        public half CausticResistance;

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
            ThermalResistance = (half)1.0f,
            EMResistance = (half)1.0f,
            RadiationResistance = (half)1.0f,
            KineticResistance = (half)1.0f,
            ExplosiveResistance = (half)1.0f,
            CausticResistance = (half)1.0f
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
            ThermalResistance = (half)0.8f,
            EMResistance = (half)0.75f,
            RadiationResistance = (half)0.8f,
            KineticResistance = (half)1.4f,
            ExplosiveResistance = (half)1.2f,
            CausticResistance = (half)0.85f
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
            ThermalResistance = (half)1.35f,
            EMResistance = (half)1.4f,
            RadiationResistance = (half)1.2f,
            KineticResistance = (half)0.7f,
            ExplosiveResistance = (half)0.9f,
            CausticResistance = (half)1.1f
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
        /// Thermal resistance modifier.
        /// </summary>
        public half ThermalResistance;

        /// <summary>
        /// Electromagnetic (EMP) resistance modifier.
        /// </summary>
        public half EMResistance;

        /// <summary>
        /// Radiation resistance modifier.
        /// </summary>
        public half RadiationResistance;

        /// <summary>
        /// Kinetic resistance modifier.
        /// </summary>
        public half KineticResistance;

        /// <summary>
        /// Explosive resistance modifier.
        /// </summary>
        public half ExplosiveResistance;

        /// <summary>
        /// Caustic resistance modifier.
        /// </summary>
        public half CausticResistance;

        public static Space4XArmor Standard(float thickness) => new Space4XArmor
        {
            Type = ArmorType.Standard,
            Thickness = thickness,
            PenetrationThreshold = (half)0.3f,
            EnergyResistance = (half)1.0f,
            ThermalResistance = (half)1.0f,
            EMResistance = (half)1.0f,
            RadiationResistance = (half)1.0f,
            KineticResistance = (half)1.0f,
            ExplosiveResistance = (half)1.0f,
            CausticResistance = (half)1.0f
        };

        public static Space4XArmor Reactive(float thickness) => new Space4XArmor
        {
            Type = ArmorType.Reactive,
            Thickness = thickness,
            PenetrationThreshold = (half)0.4f,
            EnergyResistance = (half)0.9f,
            ThermalResistance = (half)0.85f,
            EMResistance = (half)0.8f,
            RadiationResistance = (half)0.9f,
            KineticResistance = (half)0.8f,
            ExplosiveResistance = (half)1.5f,
            CausticResistance = (half)0.7f
        };

        public static Space4XArmor Ablative(float thickness) => new Space4XArmor
        {
            Type = ArmorType.Ablative,
            Thickness = thickness,
            PenetrationThreshold = (half)0.25f,
            EnergyResistance = (half)1.4f,
            ThermalResistance = (half)1.5f,
            EMResistance = (half)1.2f,
            RadiationResistance = (half)1.1f,
            KineticResistance = (half)1.1f,
            ExplosiveResistance = (half)0.8f,
            CausticResistance = (half)1.2f
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

    /// <summary>
    /// Aggregated combat telemetry snapshot (cumulative + delta).
    /// </summary>
    public struct Space4XCombatTelemetry : IComponentData
    {
        public uint LastProcessedTick;

        public uint TotalShotsFired;
        public uint TotalShotsHit;
        public uint TotalShotsMissed;

        public uint ShotsFiredDelta;
        public uint ShotsHitDelta;
        public uint ShotsMissedDelta;

        public float TotalDamageEnergy;
        public float TotalDamageThermal;
        public float TotalDamageEM;
        public float TotalDamageRadiation;
        public float TotalDamageCaustic;
        public float TotalDamageKinetic;
        public float TotalDamageExplosive;

        public float DamageEnergyDelta;
        public float DamageThermalDelta;
        public float DamageEMDelta;
        public float DamageRadiationDelta;
        public float DamageCausticDelta;
        public float DamageKineticDelta;
        public float DamageExplosiveDelta;
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
        public static float GetWeaponResistance(Space4XDamageType damageType, in Space4XShield shield)
        {
            return damageType switch
            {
                Space4XDamageType.Energy => ResolveResistance(shield.EnergyResistance, 1f),
                Space4XDamageType.Thermal => ResolveResistance(shield.ThermalResistance, shield.EnergyResistance),
                Space4XDamageType.EM => ResolveResistance(shield.EMResistance, shield.EnergyResistance),
                Space4XDamageType.Radiation => ResolveResistance(shield.RadiationResistance, shield.EnergyResistance),
                Space4XDamageType.Caustic => ResolveResistance(shield.CausticResistance, shield.ThermalResistance),
                Space4XDamageType.Kinetic => ResolveResistance(shield.KineticResistance, 1f),
                Space4XDamageType.Explosive => ResolveResistance(shield.ExplosiveResistance, 1f),
                _ => 1.0f
            };
        }

        /// <summary>
        /// Gets armor resistance modifier for weapon type.
        /// </summary>
        public static float GetArmorResistance(Space4XDamageType damageType, in Space4XArmor armor)
        {
            return damageType switch
            {
                Space4XDamageType.Energy => ResolveResistance(armor.EnergyResistance, 1f),
                Space4XDamageType.Thermal => ResolveResistance(armor.ThermalResistance, armor.EnergyResistance),
                Space4XDamageType.EM => ResolveResistance(armor.EMResistance, armor.EnergyResistance),
                Space4XDamageType.Radiation => ResolveResistance(armor.RadiationResistance, armor.EnergyResistance),
                Space4XDamageType.Caustic => ResolveResistance(armor.CausticResistance, armor.ThermalResistance),
                Space4XDamageType.Kinetic => ResolveResistance(armor.KineticResistance, 1f),
                Space4XDamageType.Explosive => ResolveResistance(armor.ExplosiveResistance, 1f),
                _ => 1.0f
            };
        }

        private static float ResolveResistance(float value, float fallback)
        {
            if (value > 0f)
            {
                return value;
            }

            if (fallback > 0f)
            {
                return fallback;
            }

            return 1f;
        }
    }
}
