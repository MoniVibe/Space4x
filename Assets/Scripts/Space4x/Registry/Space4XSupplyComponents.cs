using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Type of supply resource.
    /// </summary>
    public enum SupplyType : byte
    {
        Fuel = 0,
        Ammunition = 1,
        Provisions = 2,
        LifeSupport = 3,
        RepairParts = 4
    }

    /// <summary>
    /// Activity level affecting supply consumption.
    /// </summary>
    public enum ActivityLevel : byte
    {
        /// <summary>
        /// Minimal consumption - docked or powered down.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Standard cruise consumption.
        /// </summary>
        Cruising = 1,

        /// <summary>
        /// Elevated consumption - mining, hauling.
        /// </summary>
        Working = 2,

        /// <summary>
        /// High consumption - combat operations.
        /// </summary>
        Combat = 3,

        /// <summary>
        /// Maximum consumption - emergency maneuvers.
        /// </summary>
        Emergency = 4
    }

    /// <summary>
    /// Current supply status for a fleet or vessel.
    /// </summary>
    public struct SupplyStatus : IComponentData
    {
        /// <summary>
        /// Current fuel level.
        /// </summary>
        public float Fuel;

        /// <summary>
        /// Maximum fuel capacity.
        /// </summary>
        public float FuelCapacity;

        /// <summary>
        /// Current ammunition level.
        /// </summary>
        public float Ammunition;

        /// <summary>
        /// Maximum ammunition capacity.
        /// </summary>
        public float AmmunitionCapacity;

        /// <summary>
        /// Current provisions (food, water).
        /// </summary>
        public float Provisions;

        /// <summary>
        /// Maximum provisions capacity.
        /// </summary>
        public float ProvisionsCapacity;

        /// <summary>
        /// Current life support consumables.
        /// </summary>
        public float LifeSupport;

        /// <summary>
        /// Maximum life support capacity.
        /// </summary>
        public float LifeSupportCapacity;

        /// <summary>
        /// Current repair parts.
        /// </summary>
        public float RepairParts;

        /// <summary>
        /// Maximum repair parts capacity.
        /// </summary>
        public float RepairPartsCapacity;

        /// <summary>
        /// Current activity level.
        /// </summary>
        public ActivityLevel Activity;

        /// <summary>
        /// Ticks since last resupply.
        /// </summary>
        public uint TicksSinceResupply;

        public float FuelRatio => FuelCapacity > 0 ? Fuel / FuelCapacity : 0;
        public float AmmoRatio => AmmunitionCapacity > 0 ? Ammunition / AmmunitionCapacity : 0;
        public float ProvisionsRatio => ProvisionsCapacity > 0 ? Provisions / ProvisionsCapacity : 0;
        public float LifeSupportRatio => LifeSupportCapacity > 0 ? LifeSupport / LifeSupportCapacity : 0;

        public static SupplyStatus Full(float fuelCap, float ammoCap, float provCap, float lsCap, float repairCap)
        {
            return new SupplyStatus
            {
                Fuel = fuelCap,
                FuelCapacity = fuelCap,
                Ammunition = ammoCap,
                AmmunitionCapacity = ammoCap,
                Provisions = provCap,
                ProvisionsCapacity = provCap,
                LifeSupport = lsCap,
                LifeSupportCapacity = lsCap,
                RepairParts = repairCap,
                RepairPartsCapacity = repairCap,
                Activity = ActivityLevel.Idle,
                TicksSinceResupply = 0
            };
        }

        public static SupplyStatus DefaultCarrier => Full(10000f, 5000f, 2000f, 1000f, 500f);
        public static SupplyStatus DefaultVessel => Full(1000f, 500f, 200f, 100f, 50f);
        public static SupplyStatus DefaultStrikeCraft => Full(100f, 50f, 0f, 10f, 0f);
    }

    /// <summary>
    /// Supply consumption rates per activity level.
    /// </summary>
    public struct SupplyConsumptionRates : IComponentData
    {
        /// <summary>
        /// Fuel consumption per tick when idle.
        /// </summary>
        public float FuelIdle;

        /// <summary>
        /// Fuel consumption per tick when cruising.
        /// </summary>
        public float FuelCruise;

        /// <summary>
        /// Fuel consumption per tick in combat.
        /// </summary>
        public float FuelCombat;

        /// <summary>
        /// Provisions consumption per tick (crew dependent).
        /// </summary>
        public float ProvisionsBase;

        /// <summary>
        /// Life support consumption per tick.
        /// </summary>
        public float LifeSupportBase;

        /// <summary>
        /// Ammunition consumption per combat tick.
        /// </summary>
        public float AmmoCombat;

        public static SupplyConsumptionRates DefaultCarrier => new SupplyConsumptionRates
        {
            FuelIdle = 0.1f,
            FuelCruise = 1f,
            FuelCombat = 3f,
            ProvisionsBase = 0.5f,
            LifeSupportBase = 0.2f,
            AmmoCombat = 2f
        };

        public static SupplyConsumptionRates DefaultVessel => new SupplyConsumptionRates
        {
            FuelIdle = 0.01f,
            FuelCruise = 0.1f,
            FuelCombat = 0.3f,
            ProvisionsBase = 0.05f,
            LifeSupportBase = 0.02f,
            AmmoCombat = 0.5f
        };
    }

    /// <summary>
    /// Tags an entity as a resupply source.
    /// </summary>
    public struct SupplySource : IComponentData
    {
        /// <summary>
        /// Type of source.
        /// </summary>
        public SupplySourceType SourceType;

        /// <summary>
        /// Available fuel to provide.
        /// </summary>
        public float AvailableFuel;

        /// <summary>
        /// Available ammunition.
        /// </summary>
        public float AvailableAmmo;

        /// <summary>
        /// Available provisions.
        /// </summary>
        public float AvailableProvisions;

        /// <summary>
        /// Transfer rate per tick.
        /// </summary>
        public float TransferRate;

        /// <summary>
        /// Whether source is currently available.
        /// </summary>
        public byte IsAvailable;
    }

    /// <summary>
    /// Type of supply source.
    /// </summary>
    public enum SupplySourceType : byte
    {
        /// <summary>
        /// Station or colony supply depot.
        /// </summary>
        Depot = 0,

        /// <summary>
        /// Mobile supply hauler.
        /// </summary>
        Hauler = 1,

        /// <summary>
        /// Gas giant for emergency fuel harvesting.
        /// </summary>
        GasGiant = 2,

        /// <summary>
        /// Planet for emergency provisions.
        /// </summary>
        Planet = 3,

        /// <summary>
        /// Asteroid for emergency mining.
        /// </summary>
        Asteroid = 4,

        /// <summary>
        /// Carrier resupplying subordinate craft.
        /// </summary>
        Carrier = 5
    }

    /// <summary>
    /// Active supply route between fleet and source.
    /// </summary>
    public struct SupplyRoute : IComponentData
    {
        /// <summary>
        /// Source entity.
        /// </summary>
        public Entity Source;

        /// <summary>
        /// Destination entity (fleet/vessel).
        /// </summary>
        public Entity Destination;

        /// <summary>
        /// Route status.
        /// </summary>
        public SupplyRouteStatus Status;

        /// <summary>
        /// Distance to source.
        /// </summary>
        public float Distance;

        /// <summary>
        /// Estimated ticks until resupply.
        /// </summary>
        public uint ETAResupply;

        /// <summary>
        /// Whether route is under threat.
        /// </summary>
        public byte IsThreatened;

        /// <summary>
        /// Tick when route was established.
        /// </summary>
        public uint EstablishedTick;
    }

    /// <summary>
    /// Status of a supply route.
    /// </summary>
    public enum SupplyRouteStatus : byte
    {
        None = 0,
        Planned = 1,
        Active = 2,
        InTransit = 3,
        Delivering = 4,
        Completed = 5,
        Disrupted = 6,
        Cancelled = 7
    }

    /// <summary>
    /// Emergency harvest operation state.
    /// </summary>
    public struct EmergencyHarvest : IComponentData
    {
        /// <summary>
        /// Type of harvest.
        /// </summary>
        public EmergencyHarvestType Type;

        /// <summary>
        /// Target body for harvesting.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Current harvest progress [0, 1].
        /// </summary>
        public half Progress;

        /// <summary>
        /// Harvest rate per tick.
        /// </summary>
        public float HarvestRate;

        /// <summary>
        /// Fuel harvested per cycle.
        /// </summary>
        public float FuelPerCycle;

        /// <summary>
        /// Provisions harvested per cycle.
        /// </summary>
        public float ProvisionsPerCycle;

        /// <summary>
        /// Risk of encounter during harvest [0, 1].
        /// </summary>
        public half EncounterRisk;

        /// <summary>
        /// Whether currently harvesting.
        /// </summary>
        public byte IsHarvesting;
    }

    /// <summary>
    /// Type of emergency harvest.
    /// </summary>
    public enum EmergencyHarvestType : byte
    {
        None = 0,
        GasGiantFuel = 1,
        PlanetaryProvisions = 2,
        AsteroidMaterials = 3,
        DerelictSalvage = 4
    }

    /// <summary>
    /// Supply shortage alert.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SupplyAlert : IBufferElementData
    {
        /// <summary>
        /// Type of supply that is low.
        /// </summary>
        public SupplyType Type;

        /// <summary>
        /// Severity level (0 = warning, 1 = critical, 2 = depleted).
        /// </summary>
        public byte Severity;

        /// <summary>
        /// Current ratio of this supply.
        /// </summary>
        public half CurrentRatio;

        /// <summary>
        /// Estimated ticks until depletion.
        /// </summary>
        public uint TicksUntilDepletion;

        /// <summary>
        /// Tick when alert was raised.
        /// </summary>
        public uint AlertTick;
    }

    /// <summary>
    /// Tag for entities in critical supply state.
    /// </summary>
    public struct SupplyCriticalTag : IComponentData { }

    /// <summary>
    /// Utility functions for supply calculations.
    /// </summary>
    public static class SupplyUtility
    {
        /// <summary>
        /// Calculates fuel consumption based on activity.
        /// </summary>
        public static float CalculateFuelConsumption(in SupplyConsumptionRates rates, ActivityLevel activity)
        {
            return activity switch
            {
                ActivityLevel.Idle => rates.FuelIdle,
                ActivityLevel.Cruising => rates.FuelCruise,
                ActivityLevel.Working => rates.FuelCruise * 1.5f,
                ActivityLevel.Combat => rates.FuelCombat,
                ActivityLevel.Emergency => rates.FuelCombat * 2f,
                _ => rates.FuelCruise
            };
        }

        /// <summary>
        /// Calculates ticks until supply depletion.
        /// </summary>
        public static uint CalculateTicksUntilDepletion(float current, float consumptionRate)
        {
            if (consumptionRate <= 0)
            {
                return uint.MaxValue;
            }
            return (uint)(current / consumptionRate);
        }

        /// <summary>
        /// Determines alert severity based on ratio.
        /// </summary>
        public static byte GetAlertSeverity(float ratio)
        {
            if (ratio <= 0)
            {
                return 2; // Depleted
            }
            if (ratio < 0.1f)
            {
                return 1; // Critical
            }
            if (ratio < 0.25f)
            {
                return 0; // Warning
            }
            return 255; // No alert
        }

        /// <summary>
        /// Calculates emergency harvest rate based on source type and tech level.
        /// </summary>
        public static float CalculateHarvestRate(EmergencyHarvestType type, byte techLevel)
        {
            float baseRate = type switch
            {
                EmergencyHarvestType.GasGiantFuel => 5f,
                EmergencyHarvestType.PlanetaryProvisions => 2f,
                EmergencyHarvestType.AsteroidMaterials => 3f,
                EmergencyHarvestType.DerelictSalvage => 10f,
                _ => 1f
            };

            return baseRate * (1f + techLevel * 0.2f);
        }

        /// <summary>
        /// Calculates morale penalty from low supplies.
        /// </summary>
        public static float CalculateSupplyMoralePenalty(in SupplyStatus status)
        {
            float penalty = 0f;

            // Low provisions is worst
            if (status.ProvisionsRatio < 0.5f)
            {
                penalty += (0.5f - status.ProvisionsRatio) * 0.4f;
            }

            // Low life support is critical
            if (status.LifeSupportRatio < 0.3f)
            {
                penalty += (0.3f - status.LifeSupportRatio) * 0.6f;
            }

            // Low fuel causes anxiety
            if (status.FuelRatio < 0.25f)
            {
                penalty += (0.25f - status.FuelRatio) * 0.2f;
            }

            return math.min(penalty, 1f);
        }
    }
}

