using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum NonCapitalCraftKind : byte
    {
        StrikeCraft = 0,
        MiningVessel = 1,
        Hauler = 2,
        Drone = 3,
        Utility = 4
    }

    public enum NonCapitalCraftMassClass : byte
    {
        Drone = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3,
        Superheavy = 4
    }

    public enum NonCapitalCraftControlMode : byte
    {
        Piloted = 0,
        RemoteDrone = 1,
        AutonomousDrone = 2
    }

    /// <summary>
    /// Shared profile for non-capital craft (strike craft, miners, haulers, drones).
    /// </summary>
    public struct NonCapitalCraftProfile : IComponentData
    {
        public NonCapitalCraftKind Kind;
        public NonCapitalCraftMassClass MassClass;
        public NonCapitalCraftControlMode ControlMode;
        public float MaxMassTons;
        public float BaseMassTons;
        public float BaseHull;
        public float BaseArmor;
    }

    /// <summary>
    /// Fixed internals and their integrity for craft-level customization.
    /// </summary>
    public struct CraftInternalState : IComponentData
    {
        public float CoreIntegrity;
        public float EngineIntegrity;
        public float AvionicsIntegrity;
        public float LifeSupportIntegrity;
        public float InternalMassTons;
        public float BaseHeatLoad;
        public float HeatDissipation;
        public float HeatCapacity;

        public static CraftInternalState ForMassClass(NonCapitalCraftMassClass massClass)
        {
            return massClass switch
            {
                NonCapitalCraftMassClass.Drone => new CraftInternalState
                {
                    CoreIntegrity = 0.7f,
                    EngineIntegrity = 0.7f,
                    AvionicsIntegrity = 0.75f,
                    LifeSupportIntegrity = 0.9f,
                    InternalMassTons = 6f,
                    BaseHeatLoad = 0.25f,
                    HeatDissipation = 1.1f,
                    HeatCapacity = 1.6f
                },
                NonCapitalCraftMassClass.Light => new CraftInternalState
                {
                    CoreIntegrity = 0.75f,
                    EngineIntegrity = 0.75f,
                    AvionicsIntegrity = 0.8f,
                    LifeSupportIntegrity = 0.85f,
                    InternalMassTons = 10f,
                    BaseHeatLoad = 0.35f,
                    HeatDissipation = 1.4f,
                    HeatCapacity = 2f
                },
                NonCapitalCraftMassClass.Medium => new CraftInternalState
                {
                    CoreIntegrity = 0.8f,
                    EngineIntegrity = 0.78f,
                    AvionicsIntegrity = 0.82f,
                    LifeSupportIntegrity = 0.82f,
                    InternalMassTons = 16f,
                    BaseHeatLoad = 0.45f,
                    HeatDissipation = 1.8f,
                    HeatCapacity = 2.5f
                },
                NonCapitalCraftMassClass.Heavy => new CraftInternalState
                {
                    CoreIntegrity = 0.84f,
                    EngineIntegrity = 0.8f,
                    AvionicsIntegrity = 0.84f,
                    LifeSupportIntegrity = 0.8f,
                    InternalMassTons = 24f,
                    BaseHeatLoad = 0.55f,
                    HeatDissipation = 2.2f,
                    HeatCapacity = 3.2f
                },
                _ => new CraftInternalState
                {
                    CoreIntegrity = 0.88f,
                    EngineIntegrity = 0.82f,
                    AvionicsIntegrity = 0.86f,
                    LifeSupportIntegrity = 0.78f,
                    InternalMassTons = 34f,
                    BaseHeatLoad = 0.7f,
                    HeatDissipation = 2.6f,
                    HeatCapacity = 4f
                }
            };
        }
    }

    public enum CraftModuleCategory : byte
    {
        Propulsion = 0,
        Weapon = 1,
        Defense = 2,
        Utility = 3,
        Sensor = 4,
        ElectronicWarfare = 5,
        MiningTool = 6,
        Cargo = 7,
        Support = 8
    }

    [InternalBufferCapacity(6)]
    public struct CraftModuleSlot : IBufferElementData
    {
        public byte SlotId;
        public CraftModuleCategory Category;
        public byte SizeBudget;
        public byte IsFixed;
    }

    [InternalBufferCapacity(8)]
    public struct CraftModuleInstance : IBufferElementData
    {
        public byte SlotId;
        public CraftModuleCategory Category;
        public FixedString64Bytes ModuleId;
        public float MassTons;
        public float HeatLoad;
        public float HeatDissipation;
        public float ThrustBonus;
        public float ArmorBonus;
        public float HullBonus;
        public float MiningYieldBonus;
        public float CargoBonus;
        public float TransferBonus;
        public float EvasionBonus;
        public float Reliability;
    }

    [System.Flags]
    public enum CraftBuildViolation : ushort
    {
        None = 0,
        OverMass = 1 << 0,
        MissingPropulsion = 1 << 1,
        MissingControlCore = 1 << 2,
        HeatDeficit = 1 << 3,
        SlotMismatch = 1 << 4
    }

    public struct CraftCustomizationPolicy : IComponentData
    {
        public byte AllowUnsafeBuilds;
        public float OverMassSpeedPenaltyPerRatio;
        public float OverMassTurnPenaltyPerRatio;
        public float HeatPenaltyScale;
        public float HeatDeficitTolerance;

        public static CraftCustomizationPolicy Default => new CraftCustomizationPolicy
        {
            AllowUnsafeBuilds = 0,
            OverMassSpeedPenaltyPerRatio = 0.75f,
            OverMassTurnPenaltyPerRatio = 0.6f,
            HeatPenaltyScale = 0.7f,
            HeatDeficitTolerance = 0.08f
        };
    }

    /// <summary>
    /// Derived loadout/runtime values used by movement/mining/hauler systems.
    /// </summary>
    public struct CraftLoadoutAggregate : IComponentData
    {
        public float TotalMassTons;
        public float MassUtilization;
        public float OverMassTons;
        public float CoreIntegrity;
        public float HeatLoad;
        public float HeatDissipation;
        public float HeatBalance;
        public float EffectiveSpeedMultiplier;
        public float EffectiveTurnMultiplier;
        public float MiningYieldMultiplier;
        public float CargoMultiplier;
        public float TransferMultiplier;
        public float EvasionMultiplier;
        public float BonusHull;
        public float BonusArmor;
        public CraftBuildViolation Violations;
    }

    /// <summary>
    /// Captures pre-customization base stats so projection stays deterministic and non-cumulative.
    /// </summary>
    public struct CraftPerformanceBaseline : IComponentData
    {
        public float VesselBaseSpeed;
        public float MiningBaseSpeed;
        public float MiningBaseEfficiency;
        public float MiningBaseCargoCapacity;
        public byte VesselBaseCaptured;
        public byte MiningBaseCaptured;
    }

    public static class Space4XCraftCustomizationUtility
    {
        public static NonCapitalCraftMassClass ResolveMassClassFromCap(float maxMassTons)
        {
            if (maxMassTons <= 25f)
            {
                return NonCapitalCraftMassClass.Drone;
            }

            if (maxMassTons <= 50f)
            {
                return NonCapitalCraftMassClass.Light;
            }

            if (maxMassTons <= 90f)
            {
                return NonCapitalCraftMassClass.Medium;
            }

            if (maxMassTons <= 150f)
            {
                return NonCapitalCraftMassClass.Heavy;
            }

            return NonCapitalCraftMassClass.Superheavy;
        }

        public static float ResolveMassClassEvasionBase(NonCapitalCraftMassClass massClass)
        {
            return massClass switch
            {
                NonCapitalCraftMassClass.Drone => 1.2f,
                NonCapitalCraftMassClass.Light => 1.1f,
                NonCapitalCraftMassClass.Medium => 1f,
                NonCapitalCraftMassClass.Heavy => 0.9f,
                _ => 0.8f
            };
        }

        public static NonCapitalCraftProfile CreateDefaultProfile(
            NonCapitalCraftKind kind,
            NonCapitalCraftControlMode controlMode,
            float maxMassTons)
        {
            var clampedMass = math.max(8f, maxMassTons);
            var massClass = ResolveMassClassFromCap(clampedMass);
            var hull = clampedMass * 4.5f;
            var armor = clampedMass * 3.5f;

            return new NonCapitalCraftProfile
            {
                Kind = kind,
                MassClass = massClass,
                ControlMode = controlMode,
                MaxMassTons = clampedMass,
                BaseMassTons = clampedMass * 0.45f,
                BaseHull = hull,
                BaseArmor = armor
            };
        }
    }
}
