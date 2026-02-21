using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Alignment;
using PureDOTS.Runtime.Economy.Production;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Space4X.UI
{
    [Serializable]
    public struct Space4XManufacturingPreview
    {
        public string ManufacturerId;
        public string ManufacturerName;
        public float AssemblyPurity;
        public float XenoAffinity;
        public string ManufacturerSummary;
        public string OutlookSummary;
        public string[] ModuleOrigins;
        public string[] Consumables;
        public string[] CrewRoster;

        public static Space4XManufacturingPreview Empty => new Space4XManufacturingPreview
        {
            ManufacturerId = "unknown",
            ManufacturerName = "Unknown",
            AssemblyPurity = 0.5f,
            XenoAffinity = 0.5f,
            ManufacturerSummary = "Manufacturer: Unknown",
            OutlookSummary = "Outlook: Unspecified",
            ModuleOrigins = Array.Empty<string>(),
            Consumables = Array.Empty<string>(),
            CrewRoster = Array.Empty<string>()
        };
    }

    [CreateAssetMenu(fileName = "Space4XManufacturingCatalog", menuName = "Space4X/UI/Manufacturing Catalog")]
    public sealed class Space4XManufacturingCatalog : ScriptableObject
    {
        public const string ResourcePath = "UI/Space4XManufacturingCatalog";

        [SerializeField] private ManufacturerDefinition[] manufacturers = Array.Empty<ManufacturerDefinition>();
        [SerializeField] private OrganDefinition[] organs = Array.Empty<OrganDefinition>();
        [SerializeField] private ModuleFamilyDefinition[] moduleFamilies = Array.Empty<ModuleFamilyDefinition>();
        [SerializeField] private ConsumableDefinition[] consumables = Array.Empty<ConsumableDefinition>();
        [SerializeField] private CrewRoleDefinition[] crewRoles = Array.Empty<CrewRoleDefinition>();

        public ManufacturerDefinition[] Manufacturers => manufacturers;
        public OrganDefinition[] Organs => organs;
        public ModuleFamilyDefinition[] ModuleFamilies => moduleFamilies;
        public ConsumableDefinition[] Consumables => consumables;
        public CrewRoleDefinition[] CrewRoles => crewRoles;

        public Space4XManufacturingPreview CreatePreview(string presetId, string[] moduleIds, int difficulty, uint seed)
        {
            if (manufacturers == null || manufacturers.Length == 0)
            {
                return Space4XManufacturingPreview.Empty;
            }

            var rng = CreatePreviewRandom(presetId, difficulty, seed);
            var primaryManufacturer = ResolveManufacturer(ref rng);
            var assemblyPurity = ResolveAssemblyPurity(primaryManufacturer);
            var xenoAffinity = ResolveXenoAffinity(primaryManufacturer);

            if (IsExterminatorProfile(primaryManufacturer))
            {
                assemblyPurity = Mathf.Max(assemblyPurity, 0.92f);
                xenoAffinity = Mathf.Max(xenoAffinity, 0.95f);
            }

            var manufacturerName = ResolveManufacturerName(primaryManufacturer.Id);
            var manufacturerSummary = $"Manufacturer: {manufacturerName} (Purity {assemblyPurity:0.00}, Xeno {xenoAffinity:0.00})";
            var outlookSummary = BuildOutlookSummary(primaryManufacturer);
            var moduleOrigins = BuildModuleOrigins(ref rng, primaryManufacturer, assemblyPurity, xenoAffinity, moduleIds);
            var consumableDrops = BuildConsumables(ref rng, difficulty);
            var crewRoster = BuildCrewRoster(ref rng);

            return new Space4XManufacturingPreview
            {
                ManufacturerId = primaryManufacturer.Id ?? string.Empty,
                ManufacturerName = manufacturerName,
                AssemblyPurity = assemblyPurity,
                XenoAffinity = xenoAffinity,
                ManufacturerSummary = manufacturerSummary,
                OutlookSummary = outlookSummary,
                ModuleOrigins = moduleOrigins,
                Consumables = consumableDrops,
                CrewRoster = crewRoster
            };
        }

        public static Space4XManufacturingCatalog LoadOrFallback()
        {
            var catalog = Resources.Load<Space4XManufacturingCatalog>(ResourcePath);
            if (catalog == null || catalog.manufacturers == null || catalog.manufacturers.Length == 0)
            {
                catalog = CreateRuntimeFallback();
            }

            return catalog;
        }

        public static Space4XManufacturingCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XManufacturingCatalog>();
            catalog.ApplyRuntimeDefaults();
            return catalog;
        }

        public void ApplyRuntimeDefaults()
        {
            manufacturers = new[]
            {
                new ManufacturerDefinition
                {
                    Id = "aegis_forge",
                    DisplayName = "Aegis Forge",
                    Description = "Lawful defensive shipworks with tight QA loops.",
                    AssemblyPurityBase = 0.82f,
                    XenoAffinityBase = 0.65f,
                    Alignment = new ManufacturingAlignment { Moral = 0.2f, Order = 0.7f, Purity = 0.35f },
                    Behavior = ManufacturingContractDefaults.NeutralBehavior,
                    OutlookAxes = new[]
                    {
                        new ManufacturingAxisValue { Axis = EthicAxis.Military, Value = 0.6f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Authority, Value = 0.4f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Tolerance, Value = -0.3f }
                    },
                    RaceId = -1,
                    CultureId = -1
                },
                new ManufacturerDefinition
                {
                    Id = "orion_coilworks",
                    DisplayName = "Orion Coilworks",
                    Description = "Materialist and collaborative; known for efficient power systems.",
                    AssemblyPurityBase = 0.68f,
                    XenoAffinityBase = 0.45f,
                    Alignment = new ManufacturingAlignment { Moral = 0.1f, Order = 0.2f, Purity = 0.1f },
                    Behavior = ManufacturingContractDefaults.NeutralBehavior,
                    OutlookAxes = new[]
                    {
                        new ManufacturingAxisValue { Axis = EthicAxis.Economic, Value = 0.7f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Tolerance, Value = 0.6f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Expansion, Value = 0.2f }
                    },
                    RaceId = -1,
                    CultureId = -1
                },
                new ManufacturerDefinition
                {
                    Id = "vantrel_syndicate",
                    DisplayName = "Vantrel Syndicate",
                    Description = "Chaotic syndicate; fast turnaround, uneven reliability.",
                    AssemblyPurityBase = 0.58f,
                    XenoAffinityBase = 0.35f,
                    Alignment = new ManufacturingAlignment { Moral = -0.2f, Order = -0.4f, Purity = -0.55f },
                    Behavior = new ManufacturingBehaviorProfile
                    {
                        Compliance = 0.35f,
                        Caution = 0.35f,
                        FormationAdherence = 0.4f,
                        RiskTolerance = 0.7f,
                        Aggression = 0.55f,
                        Patience = 0.3f
                    },
                    OutlookAxes = new[]
                    {
                        new ManufacturingAxisValue { Axis = EthicAxis.Authority, Value = 0.2f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Economic, Value = 0.5f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Tolerance, Value = 0.1f }
                    },
                    RaceId = -1,
                    CultureId = -1
                },
                new ManufacturerDefinition
                {
                    Id = "khaldus_exterminator_forge",
                    DisplayName = "Khaldus Exterminator Forge",
                    Description = "Fanatic militarist xenophobe; refuses foreign sourcing.",
                    AssemblyPurityBase = 0.9f,
                    XenoAffinityBase = 0.9f,
                    Alignment = new ManufacturingAlignment { Moral = -0.3f, Order = 0.8f, Purity = 0.1f },
                    Behavior = new ManufacturingBehaviorProfile
                    {
                        Compliance = 0.8f,
                        Caution = 0.4f,
                        FormationAdherence = 0.75f,
                        RiskTolerance = 0.4f,
                        Aggression = 0.85f,
                        Patience = 0.35f
                    },
                    OutlookAxes = new[]
                    {
                        new ManufacturingAxisValue { Axis = EthicAxis.Military, Value = 0.95f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Tolerance, Value = -0.9f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Authority, Value = 0.6f }
                    },
                    RaceId = -1,
                    CultureId = -1
                },
                new ManufacturerDefinition
                {
                    Id = "lumen_covenant",
                    DisplayName = "Lumen Covenant",
                    Description = "Spiritual artisans; slow, refined, and balanced.",
                    AssemblyPurityBase = 0.76f,
                    XenoAffinityBase = 0.55f,
                    Alignment = new ManufacturingAlignment { Moral = 0.45f, Order = 0.3f, Purity = 0.55f },
                    Behavior = ManufacturingContractDefaults.NeutralBehavior,
                    OutlookAxes = new[]
                    {
                        new ManufacturingAxisValue { Axis = EthicAxis.Economic, Value = -0.6f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Military, Value = -0.3f },
                        new ManufacturingAxisValue { Axis = EthicAxis.Tolerance, Value = 0.2f }
                    },
                    RaceId = -1,
                    CultureId = -1
                }
            };

            organs = new[]
            {
                new OrganDefinition { Id = "core.aegis.m1", DisplayName = "Aegis Core Mk1", SlotType = "core", ManufacturerId = "aegis_forge", Quality = 0.7f, Efficiency = 0.65f, Precision = 0.4f, Stability = 0.7f, Cooling = 0.5f, PowerDraw = 0.55f, Reliability = 0.8f },
                new OrganDefinition { Id = "regulator.aegis.m1", DisplayName = "Aegis Regulator", SlotType = "regulator", ManufacturerId = "aegis_forge", Quality = 0.68f, Efficiency = 0.6f, Precision = 0.45f, Stability = 0.7f, Cooling = 0.45f, PowerDraw = 0.5f, Reliability = 0.78f },
                new OrganDefinition { Id = "cooling.aegis.m1", DisplayName = "Aegis Cooling Loop", SlotType = "cooling", ManufacturerId = "aegis_forge", Quality = 0.72f, Efficiency = 0.55f, Precision = 0.35f, Stability = 0.6f, Cooling = 0.8f, PowerDraw = 0.45f, Reliability = 0.75f },
                new OrganDefinition { Id = "plating.aegis.m1", DisplayName = "Aegis Plating", SlotType = "plating", ManufacturerId = "aegis_forge", Quality = 0.74f, Efficiency = 0.4f, Precision = 0.2f, Stability = 0.85f, Cooling = 0.3f, PowerDraw = 0.4f, Reliability = 0.8f },
                new OrganDefinition { Id = "mount.aegis.m1", DisplayName = "Aegis Mount Brace", SlotType = "mount", ManufacturerId = "aegis_forge", Quality = 0.66f, Efficiency = 0.35f, Precision = 0.3f, Stability = 0.75f, Cooling = 0.2f, PowerDraw = 0.35f, Reliability = 0.7f },
                new OrganDefinition { Id = "core.orion.m1", DisplayName = "Orion Flux Core", SlotType = "core", ManufacturerId = "orion_coilworks", Quality = 0.7f, Efficiency = 0.75f, Precision = 0.4f, Stability = 0.6f, Cooling = 0.55f, PowerDraw = 0.45f, Reliability = 0.7f },
                new OrganDefinition { Id = "cooling.orion.m1", DisplayName = "Orion Coil Radiator", SlotType = "cooling", ManufacturerId = "orion_coilworks", Quality = 0.65f, Efficiency = 0.6f, Precision = 0.3f, Stability = 0.5f, Cooling = 0.85f, PowerDraw = 0.35f, Reliability = 0.65f },
                new OrganDefinition { Id = "drive.orion.m1", DisplayName = "Orion Drive Stack", SlotType = "drive", ManufacturerId = "orion_coilworks", Quality = 0.68f, Efficiency = 0.7f, Precision = 0.4f, Stability = 0.6f, Cooling = 0.5f, PowerDraw = 0.6f, Reliability = 0.7f },
                new OrganDefinition { Id = "injector.orion.m1", DisplayName = "Orion Injector", SlotType = "injector", ManufacturerId = "orion_coilworks", Quality = 0.62f, Efficiency = 0.65f, Precision = 0.35f, Stability = 0.55f, Cooling = 0.45f, PowerDraw = 0.55f, Reliability = 0.65f },
                new OrganDefinition { Id = "sensor.orion.m1", DisplayName = "Orion Sensor Suite", SlotType = "sensor", ManufacturerId = "orion_coilworks", Quality = 0.66f, Efficiency = 0.6f, Precision = 0.7f, Stability = 0.55f, Cooling = 0.4f, PowerDraw = 0.5f, Reliability = 0.6f },
                new OrganDefinition { Id = "chamber.vantrel.m1", DisplayName = "Vantrel Chamber", SlotType = "chamber", ManufacturerId = "vantrel_syndicate", Quality = 0.58f, Efficiency = 0.45f, Precision = 0.5f, Stability = 0.45f, Cooling = 0.4f, PowerDraw = 0.6f, Reliability = 0.5f },
                new OrganDefinition { Id = "targeting.vantrel.m1", DisplayName = "Vantrel Targeting Array", SlotType = "targeting", ManufacturerId = "vantrel_syndicate", Quality = 0.55f, Efficiency = 0.5f, Precision = 0.65f, Stability = 0.4f, Cooling = 0.35f, PowerDraw = 0.55f, Reliability = 0.5f },
                new OrganDefinition { Id = "processor.vantrel.m1", DisplayName = "Vantrel Logic Core", SlotType = "processor", ManufacturerId = "vantrel_syndicate", Quality = 0.52f, Efficiency = 0.5f, Precision = 0.6f, Stability = 0.35f, Cooling = 0.3f, PowerDraw = 0.55f, Reliability = 0.45f },
                new OrganDefinition { Id = "cooling.vantrel.m1", DisplayName = "Vantrel Thermal Splice", SlotType = "cooling", ManufacturerId = "vantrel_syndicate", Quality = 0.5f, Efficiency = 0.45f, Precision = 0.25f, Stability = 0.4f, Cooling = 0.7f, PowerDraw = 0.5f, Reliability = 0.4f },
                new OrganDefinition { Id = "chamber.khaldus.m1", DisplayName = "Khaldus Shredder Chamber", SlotType = "chamber", ManufacturerId = "khaldus_exterminator_forge", Quality = 0.82f, Efficiency = 0.55f, Precision = 0.55f, Stability = 0.75f, Cooling = 0.6f, PowerDraw = 0.7f, Reliability = 0.8f },
                new OrganDefinition { Id = "mount.khaldus.m1", DisplayName = "Khaldus Mount Lattice", SlotType = "mount", ManufacturerId = "khaldus_exterminator_forge", Quality = 0.8f, Efficiency = 0.4f, Precision = 0.35f, Stability = 0.8f, Cooling = 0.3f, PowerDraw = 0.5f, Reliability = 0.78f },
                new OrganDefinition { Id = "plating.khaldus.m1", DisplayName = "Khaldus Ceramite Plating", SlotType = "plating", ManufacturerId = "khaldus_exterminator_forge", Quality = 0.85f, Efficiency = 0.3f, Precision = 0.25f, Stability = 0.9f, Cooling = 0.25f, PowerDraw = 0.55f, Reliability = 0.82f },
                new OrganDefinition { Id = "targeting.khaldus.m1", DisplayName = "Khaldus Targeting Spine", SlotType = "targeting", ManufacturerId = "khaldus_exterminator_forge", Quality = 0.78f, Efficiency = 0.45f, Precision = 0.7f, Stability = 0.65f, Cooling = 0.4f, PowerDraw = 0.6f, Reliability = 0.75f },
                new OrganDefinition { Id = "emitter.lumen.m1", DisplayName = "Lumen Shield Emitter", SlotType = "emitter", ManufacturerId = "lumen_covenant", Quality = 0.72f, Efficiency = 0.6f, Precision = 0.55f, Stability = 0.7f, Cooling = 0.5f, PowerDraw = 0.55f, Reliability = 0.7f },
                new OrganDefinition { Id = "sensor.lumen.m1", DisplayName = "Lumen Insight Array", SlotType = "sensor", ManufacturerId = "lumen_covenant", Quality = 0.7f, Efficiency = 0.55f, Precision = 0.8f, Stability = 0.6f, Cooling = 0.45f, PowerDraw = 0.5f, Reliability = 0.65f },
                new OrganDefinition { Id = "processor.lumen.m1", DisplayName = "Lumen Logic Prism", SlotType = "processor", ManufacturerId = "lumen_covenant", Quality = 0.68f, Efficiency = 0.55f, Precision = 0.65f, Stability = 0.6f, Cooling = 0.4f, PowerDraw = 0.5f, Reliability = 0.62f }
            };

            moduleFamilies = new[]
            {
                new ModuleFamilyDefinition
                {
                    Id = "core",
                    DisplayName = "Core Systems",
                    Description = "Reactor and power backbone.",
                    OrganSlots = new[]
                    {
                        new ModuleOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "regulator", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "cooling", Count = 1 }
                    },
                    CustomMadeDefault = false
                },
                new ModuleFamilyDefinition
                {
                    Id = "engine",
                    DisplayName = "Propulsion Systems",
                    Description = "Engine drive assemblies.",
                    OrganSlots = new[]
                    {
                        new ModuleOrganSlotDefinition { SlotType = "drive", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "injector", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "cooling", Count = 1 }
                    },
                    CustomMadeDefault = false
                },
                new ModuleFamilyDefinition
                {
                    Id = "weapon",
                    DisplayName = "Weapon Systems",
                    Description = "Primary weapon assemblies.",
                    OrganSlots = new[]
                    {
                        new ModuleOrganSlotDefinition { SlotType = "chamber", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "cooling", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "targeting", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "mount", Count = 1 }
                    },
                    CustomMadeDefault = true
                },
                new ModuleFamilyDefinition
                {
                    Id = "defense",
                    DisplayName = "Defense Systems",
                    Description = "Shielding and armor packages.",
                    OrganSlots = new[]
                    {
                        new ModuleOrganSlotDefinition { SlotType = "emitter", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "plating", Count = 1 }
                    },
                    CustomMadeDefault = false
                },
                new ModuleFamilyDefinition
                {
                    Id = "utility",
                    DisplayName = "Utility Systems",
                    Description = "Sensors and auxiliary support.",
                    OrganSlots = new[]
                    {
                        new ModuleOrganSlotDefinition { SlotType = "sensor", Count = 1 },
                        new ModuleOrganSlotDefinition { SlotType = "processor", Count = 1 }
                    },
                    CustomMadeDefault = true
                }
            };

            consumables = new[]
            {
                new ConsumableDefinition { Id = "repair_nanogel", DisplayName = "Repair Nanogel", Category = "repair", Charges = 2, ManufacturerId = "orion_coilworks", Quality = 0.7f },
                new ConsumableDefinition { Id = "shield_patch", DisplayName = "Shield Patch", Category = "defense", Charges = 1, ManufacturerId = "lumen_covenant", Quality = 0.68f },
                new ConsumableDefinition { Id = "burst_capacitor", DisplayName = "Burst Capacitor", Category = "power", Charges = 1, ManufacturerId = "orion_coilworks", Quality = 0.65f },
                new ConsumableDefinition { Id = "decoy_flare", DisplayName = "Decoy Flare", Category = "utility", Charges = 2, ManufacturerId = "vantrel_syndicate", Quality = 0.55f },
                new ConsumableDefinition { Id = "signal_jammer", DisplayName = "Signal Jammer", Category = "utility", Charges = 1, ManufacturerId = "aegis_forge", Quality = 0.62f }
            };

            crewRoles = new[]
            {
                new CrewRoleDefinition { Id = "captain_vale", DisplayName = "Captain Vale", Role = "Captain", Traits = new[] { "Tactical", "Stoic" }, Alignment = ManufacturingContractDefaults.NeutralAlignment, OutlookAxes = Array.Empty<ManufacturingAxisValue>(), Behavior = ManufacturingContractDefaults.NeutralBehavior, RaceId = -1, CultureId = -1 },
                new CrewRoleDefinition { Id = "captain_juno", DisplayName = "Captain Juno", Role = "Captain", Traits = new[] { "Bold", "Diplomatic" }, Alignment = ManufacturingContractDefaults.NeutralAlignment, OutlookAxes = Array.Empty<ManufacturingAxisValue>(), Behavior = ManufacturingContractDefaults.NeutralBehavior, RaceId = -1, CultureId = -1 },
                new CrewRoleDefinition { Id = "officer_kade", DisplayName = "Officer Kade", Role = "Officer", Traits = new[] { "Weapons", "Aggressive" }, Alignment = ManufacturingContractDefaults.NeutralAlignment, OutlookAxes = Array.Empty<ManufacturingAxisValue>(), Behavior = ManufacturingContractDefaults.NeutralBehavior, RaceId = -1, CultureId = -1 },
                new CrewRoleDefinition { Id = "officer_lyra", DisplayName = "Officer Lyra", Role = "Officer", Traits = new[] { "Engineering", "Patient" }, Alignment = ManufacturingContractDefaults.NeutralAlignment, OutlookAxes = Array.Empty<ManufacturingAxisValue>(), Behavior = ManufacturingContractDefaults.NeutralBehavior, RaceId = -1, CultureId = -1 },
                new CrewRoleDefinition { Id = "pilot_sable", DisplayName = "Pilot Sable", Role = "Pilot", Traits = new[] { "Ace", "Evasive" }, Alignment = ManufacturingContractDefaults.NeutralAlignment, OutlookAxes = Array.Empty<ManufacturingAxisValue>(), Behavior = ManufacturingContractDefaults.NeutralBehavior, RaceId = -1, CultureId = -1 },
                new CrewRoleDefinition { Id = "pilot_dax", DisplayName = "Pilot Dax", Role = "Pilot", Traits = new[] { "Interceptor", "Focused" }, Alignment = ManufacturingContractDefaults.NeutralAlignment, OutlookAxes = Array.Empty<ManufacturingAxisValue>(), Behavior = ManufacturingContractDefaults.NeutralBehavior, RaceId = -1, CultureId = -1 }
            };
        }

        private static Random CreatePreviewRandom(string presetId, int difficulty, uint seed)
        {
            var safeSeed = seed == 0u ? 1u : seed;
            var hash = ComputeStableHash32(presetId);
            var mixed = math.hash(new uint4(safeSeed, hash, (uint)math.max(1, difficulty), 0x9e3779b9u));
            if (mixed == 0u)
                mixed = 1u;
            return new Random(mixed);
        }

        private ManufacturerDefinition ResolveManufacturer(ref Random rng)
        {
            if (manufacturers == null || manufacturers.Length == 0)
                return default;

            var index = rng.NextInt(0, manufacturers.Length);
            return manufacturers[index];
        }

        private float ResolveAssemblyPurity(in ManufacturerDefinition manufacturer)
        {
            var basePurity = manufacturer.AssemblyPurityBase <= 0f ? 0.7f : manufacturer.AssemblyPurityBase;
            var lawfulness = Mathf.Clamp01(0.5f * (1f + manufacturer.Alignment.Order));
            var chaos = Mathf.Clamp01(0.5f * (1f - manufacturer.Alignment.Order));
            var purity = Mathf.Clamp01(0.5f * (1f + manufacturer.Alignment.Purity));
            var corruption = Mathf.Clamp01(0.5f * (1f - manufacturer.Alignment.Purity));
            return Mathf.Clamp01(basePurity + lawfulness * 0.15f - chaos * 0.12f + purity * 0.08f - corruption * 0.06f);
        }

        private float ResolveXenoAffinity(in ManufacturerDefinition manufacturer)
        {
            var baseAffinity = manufacturer.XenoAffinityBase <= 0f ? 0.6f : manufacturer.XenoAffinityBase;
            var tolerance = ResolveAxisValue(manufacturer, EthicAxis.Tolerance);
            return Mathf.Clamp01(baseAffinity + (-tolerance) * 0.2f);
        }

        private static float ResolveAxisValue(in ManufacturerDefinition manufacturer, EthicAxis axis)
        {
            if (manufacturer.OutlookAxes == null)
                return 0f;

            for (var i = 0; i < manufacturer.OutlookAxes.Length; i++)
            {
                if (manufacturer.OutlookAxes[i].Axis == axis)
                    return manufacturer.OutlookAxes[i].Value;
            }

            return 0f;
        }

        private bool IsExterminatorProfile(in ManufacturerDefinition manufacturer)
        {
            var military = ResolveAxisValue(manufacturer, EthicAxis.Military);
            var tolerance = ResolveAxisValue(manufacturer, EthicAxis.Tolerance);
            return Mathf.Abs(military) >= ManufacturingContractDefaults.FanaticThreshold &&
                   military > 0.7f &&
                   Mathf.Abs(tolerance) >= ManufacturingContractDefaults.FanaticThreshold &&
                   tolerance < -0.7f;
        }

        private string BuildOutlookSummary(in ManufacturerDefinition manufacturer)
        {
            var axes = manufacturer.OutlookAxes;
            if (axes == null || axes.Length == 0)
                return "Outlook: Unspecified";

            ManufacturingAxisValue primary = default;
            ManufacturingAxisValue secondary = default;
            var primaryAbs = -1f;
            var secondaryAbs = -1f;

            for (var i = 0; i < axes.Length; i++)
            {
                var absValue = Mathf.Abs(axes[i].Value);
                if (absValue > primaryAbs)
                {
                    secondary = primary;
                    secondaryAbs = primaryAbs;
                    primary = axes[i];
                    primaryAbs = absValue;
                }
                else if (absValue > secondaryAbs)
                {
                    secondary = axes[i];
                    secondaryAbs = absValue;
                }
            }

            var isFanatic = primaryAbs >= ManufacturingContractDefaults.FanaticThreshold &&
                            secondaryAbs <= ManufacturingContractDefaults.FanaticSecondaryThreshold;
            var labels = new List<string>(2)
            {
                $"{(isFanatic ? "Fanatic " : string.Empty)}{AxisToLabel(primary.Axis, primary.Value)}"
            };

            if (secondaryAbs >= 0.2f)
            {
                labels.Add(AxisToLabel(secondary.Axis, secondary.Value));
            }

            return $"Outlook: {string.Join(", ", labels)}";
        }

        private string AxisToLabel(EthicAxis axis, float value)
        {
            switch (axis)
            {
                case EthicAxis.Authority:
                    return value >= 0f ? "Authoritarian" : "Egalitarian";
                case EthicAxis.Military:
                    return value >= 0f ? "Militarist" : "Pacifist";
                case EthicAxis.Economic:
                    return value >= 0f ? "Materialist" : "Spiritualist";
                case EthicAxis.Tolerance:
                    return value >= 0f ? "Xenophile" : "Xenophobe";
                case EthicAxis.Expansion:
                    return value >= 0f ? "Expansionist" : "Isolationist";
                default:
                    return "Neutral";
            }
        }

        private string[] BuildModuleOrigins(
            ref Random rng,
            in ManufacturerDefinition primary,
            float assemblyPurity,
            float xenoAffinity,
            string[] moduleIds)
        {
            if (moduleIds == null || moduleIds.Length == 0)
                return Array.Empty<string>();

            var origins = new List<string>(moduleIds.Length);
            for (var i = 0; i < moduleIds.Length; i++)
            {
                var moduleId = moduleIds[i] ?? string.Empty;
                var family = ResolveModuleFamily(moduleId);
                var customMade = family.CustomMadeDefault || rng.NextFloat() < 0.2f;
                var moduleManufacturerId = ResolveModuleManufacturerId(ref rng, primary.Id, customMade);
                var moduleManufacturerName = ResolveManufacturerName(moduleManufacturerId);
                var localPurity = customMade ? Mathf.Clamp01(assemblyPurity - 0.25f) : assemblyPurity;
                var organSummary = BuildOrganSummary(ref rng, family, moduleManufacturerId, localPurity, xenoAffinity);
                var entry = $"{moduleId} | {moduleManufacturerName}";
                if (!string.IsNullOrWhiteSpace(organSummary))
                {
                    entry = $"{entry} | {organSummary}";
                }

                origins.Add(entry);
            }

            return origins.ToArray();
        }

        private ModuleFamilyDefinition ResolveModuleFamily(string moduleId)
        {
            var familyId = ResolveFamilyIdFromModuleId(moduleId);
            if (moduleFamilies != null)
            {
                for (var i = 0; i < moduleFamilies.Length; i++)
                {
                    if (string.Equals(moduleFamilies[i].Id, familyId, StringComparison.OrdinalIgnoreCase))
                        return moduleFamilies[i];
                }
            }

            if (moduleFamilies != null && moduleFamilies.Length > 0)
                return moduleFamilies[0];

            return new ModuleFamilyDefinition
            {
                Id = "utility",
                DisplayName = "Utility",
                Description = "Fallback module family.",
                OrganSlots = new[] { new ModuleOrganSlotDefinition { SlotType = "processor", Count = 1 } },
                CustomMadeDefault = false
            };
        }

        private string ResolveFamilyIdFromModuleId(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return "utility";

            var lower = moduleId.ToLowerInvariant();
            if (lower.Contains("reactor") || lower.Contains("core"))
                return "core";
            if (lower.Contains("engine"))
                return "engine";
            if (lower.Contains("laser") || lower.Contains("missile") || lower.Contains("pd") || lower.Contains("kinetic"))
                return "weapon";
            if (lower.Contains("shield") || lower.Contains("armor"))
                return "defense";
            if (lower.Contains("scanner") || lower.Contains("repair") || lower.Contains("hangar") || lower.Contains("tractor"))
                return "utility";

            return "utility";
        }

        private string BuildOrganSummary(
            ref Random rng,
            ModuleFamilyDefinition family,
            string moduleManufacturerId,
            float assemblyPurity,
            float xenoAffinity)
        {
            if (family.OrganSlots == null || family.OrganSlots.Length == 0 || organs == null || organs.Length == 0)
                return string.Empty;

            var organLines = new List<string>();
            for (var i = 0; i < family.OrganSlots.Length; i++)
            {
                var slot = family.OrganSlots[i];
                var count = Mathf.Max(1, slot.Count);
                for (var index = 0; index < count; index++)
                {
                    var organ = ResolveOrganForSlot(
                        ref rng,
                        slot.SlotType ?? string.Empty,
                        moduleManufacturerId,
                        assemblyPurity,
                        xenoAffinity,
                        out var organManufacturerName);
                    var slotLabel = count > 1 ? $"{slot.SlotType} {index + 1}" : slot.SlotType;
                    var organLabel = string.IsNullOrWhiteSpace(organ.DisplayName) ? "Unknown" : organ.DisplayName;
                    organLines.Add($"{slotLabel}: {organLabel} ({organManufacturerName})");
                }
            }

            return string.Join(", ", organLines);
        }

        private OrganDefinition ResolveOrganForSlot(
            ref Random rng,
            string slotType,
            string moduleManufacturerId,
            float assemblyPurity,
            float xenoAffinity,
            out string organManufacturerName)
        {
            var externalChance = Mathf.Clamp01(1f - assemblyPurity);
            externalChance = Mathf.Lerp(externalChance, externalChance * 0.25f, xenoAffinity);
            var useExternal = rng.NextFloat() < externalChance;
            var chosenManufacturerId = useExternal
                ? ResolveAlternateManufacturerId(ref rng, moduleManufacturerId)
                : moduleManufacturerId;

            if (TryPickOrgan(slotType, chosenManufacturerId, ref rng, out var organ))
            {
                organManufacturerName = ResolveManufacturerName(organ.ManufacturerId);
                return organ;
            }

            if (TryPickOrgan(slotType, string.Empty, ref rng, out organ))
            {
                organManufacturerName = ResolveManufacturerName(organ.ManufacturerId);
                return organ;
            }

            organManufacturerName = ResolveManufacturerName(moduleManufacturerId);
            return default;
        }

        private bool TryPickOrgan(string slotType, string manufacturerId, ref Random rng, out OrganDefinition organ)
        {
            organ = default;
            if (organs == null || organs.Length == 0)
                return false;

            var candidates = new List<int>();
            for (var i = 0; i < organs.Length; i++)
            {
                var candidate = organs[i];
                if (!string.Equals(candidate.SlotType, slotType, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(manufacturerId) &&
                    !string.Equals(candidate.ManufacturerId, manufacturerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                candidates.Add(i);
            }

            if (candidates.Count == 0)
                return false;

            var index = candidates.Count == 1 ? 0 : rng.NextInt(0, candidates.Count);
            organ = organs[candidates[index]];
            return true;
        }

        private string ResolveModuleManufacturerId(ref Random rng, string primaryManufacturerId, bool customMade)
        {
            if (!customMade || manufacturers == null || manufacturers.Length <= 1)
                return primaryManufacturerId;

            if (rng.NextFloat() >= 0.35f)
                return primaryManufacturerId;

            return ResolveAlternateManufacturerId(ref rng, primaryManufacturerId);
        }

        private string ResolveAlternateManufacturerId(ref Random rng, string primaryManufacturerId)
        {
            if (manufacturers == null || manufacturers.Length <= 1)
                return primaryManufacturerId;

            var candidates = new List<int>();
            for (var i = 0; i < manufacturers.Length; i++)
            {
                if (!string.Equals(manufacturers[i].Id, primaryManufacturerId, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
                return primaryManufacturerId;

            var index = candidates.Count == 1 ? 0 : rng.NextInt(0, candidates.Count);
            return manufacturers[candidates[index]].Id;
        }

        private string ResolveManufacturerName(string manufacturerId)
        {
            if (manufacturers == null || manufacturers.Length == 0)
                return "Unknown";

            for (var i = 0; i < manufacturers.Length; i++)
            {
                if (string.Equals(manufacturers[i].Id, manufacturerId, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(manufacturers[i].DisplayName)
                        ? manufacturers[i].Id
                        : manufacturers[i].DisplayName;
                }
            }

            return string.IsNullOrWhiteSpace(manufacturerId) ? "Unknown" : manufacturerId;
        }

        private string[] BuildConsumables(ref Random rng, int difficulty)
        {
            if (consumables == null || consumables.Length == 0)
                return Array.Empty<string>();

            var count = Mathf.Clamp(2 + difficulty / 3, 2, 4);
            var results = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var roll = rng.NextInt(0, consumables.Length);
                var item = consumables[roll];
                var manufacturerName = ResolveManufacturerName(item.ManufacturerId);
                results.Add($"{item.DisplayName} x{math.max(1, item.Charges)} ({manufacturerName})");
            }

            return results.ToArray();
        }

        private string[] BuildCrewRoster(ref Random rng)
        {
            if (crewRoles == null || crewRoles.Length == 0)
                return Array.Empty<string>();

            var crew = new List<string>();
            AddCrewByRole("Captain", 1, ref rng, crew);
            AddCrewByRole("Officer", 2, ref rng, crew);
            AddCrewByRole("Pilot", 2, ref rng, crew);
            return crew.ToArray();
        }

        private void AddCrewByRole(string role, int count, ref Random rng, List<string> crew)
        {
            if (count <= 0 || crewRoles == null || crewRoles.Length == 0)
                return;

            var candidates = new List<int>();
            for (var i = 0; i < crewRoles.Length; i++)
            {
                if (string.Equals(crewRoles[i].Role, role, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                for (var i = 0; i < count; i++)
                {
                    var fallback = crewRoles[rng.NextInt(0, crewRoles.Length)];
                    crew.Add(FormatCrewEntry(fallback));
                }
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var index = candidates.Count == 1 ? 0 : rng.NextInt(0, candidates.Count);
                crew.Add(FormatCrewEntry(crewRoles[candidates[index]]));
            }
        }

        private static string FormatCrewEntry(in CrewRoleDefinition role)
        {
            var traits = role.Traits == null || role.Traits.Length == 0
                ? "No traits"
                : string.Join(", ", role.Traits);
            return $"{role.DisplayName} ({role.Role}) - {traits}";
        }

        private static uint ComputeStableHash32(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                var hash = offset;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }

                return hash;
            }
        }
    }
}
