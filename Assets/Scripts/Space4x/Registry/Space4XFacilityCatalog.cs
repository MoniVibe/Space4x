using System;
using PureDOTS.Runtime.Economy.Production;
using UnityEngine;

namespace Space4X.Registry
{
    [CreateAssetMenu(fileName = "Space4XFacilityCatalog", menuName = "Space4X/Registry/Facility Catalog")]
    public sealed class Space4XFacilityCatalog : ScriptableObject
    {
        public const string ResourcePath = "Registry/Space4XFacilityCatalog";

        [SerializeField] private FacilityFamilyDefinition[] families = Array.Empty<FacilityFamilyDefinition>();
        [SerializeField] private FacilityHullDefinition[] hulls = Array.Empty<FacilityHullDefinition>();
        [SerializeField] private FacilityOrganDefinition[] organs = Array.Empty<FacilityOrganDefinition>();
        [SerializeField] private FacilityLimbDefinition[] limbs = Array.Empty<FacilityLimbDefinition>();
        [SerializeField] private FacilityProcessDefinition[] processes = Array.Empty<FacilityProcessDefinition>();
        [SerializeField] private FacilityModelDefinition[] models = Array.Empty<FacilityModelDefinition>();

        public FacilityFamilyDefinition[] Families => families;
        public FacilityHullDefinition[] Hulls => hulls;
        public FacilityOrganDefinition[] Organs => organs;
        public FacilityLimbDefinition[] Limbs => limbs;
        public FacilityProcessDefinition[] Processes => processes;
        public FacilityModelDefinition[] Models => models;

        public static Space4XFacilityCatalog LoadOrFallback()
        {
            var catalog = Resources.Load<Space4XFacilityCatalog>(ResourcePath);
            if (catalog == null)
            {
                catalog = CreateRuntimeFallback();
            }

            return catalog;
        }

        public static Space4XFacilityCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XFacilityCatalog>();
            catalog.ApplyRuntimeDefaults();
            return catalog;
        }

        public void ApplyRuntimeDefaults()
        {
            families = new[]
            {
                new FacilityFamilyDefinition
                {
                    Id = "refinery",
                    DisplayName = "Refinery",
                    Description = "Distillation and chemical processing plants.",
                    FacilityClass = "refinery"
                },
                new FacilityFamilyDefinition
                {
                    Id = "desalination",
                    DisplayName = "Desalination",
                    Description = "Water purification and desalination stacks.",
                    FacilityClass = "refinery"
                },
                new FacilityFamilyDefinition
                {
                    Id = "hydroponics",
                    DisplayName = "Hydroponics",
                    Description = "Food growth and algae processing bays.",
                    FacilityClass = "bioprocessor"
                },
                new FacilityFamilyDefinition
                {
                    Id = "bioprocessor",
                    DisplayName = "Bioprocessor",
                    Description = "Biomass and printed meat fabrication.",
                    FacilityClass = "bioprocessor"
                },
                new FacilityFamilyDefinition
                {
                    Id = "salvage",
                    DisplayName = "Salvage",
                    Description = "Derelict recycling and scrap refinement.",
                    FacilityClass = "recycler"
                },
                new FacilityFamilyDefinition
                {
                    Id = "scooper",
                    DisplayName = "Gas Scooper",
                    Description = "Atmospheric intake and fuel distillation.",
                    FacilityClass = "scooper"
                },
                new FacilityFamilyDefinition
                {
                    Id = "shipyard",
                    DisplayName = "Shipyard",
                    Description = "Hull assembly and drydock operations.",
                    FacilityClass = "shipyard"
                },
                new FacilityFamilyDefinition
                {
                    Id = "module_fabrication",
                    DisplayName = "Module Fabrication",
                    Description = "Module assembly and fitting lines.",
                    FacilityClass = "module_facility"
                },
                new FacilityFamilyDefinition
                {
                    Id = "research_lab",
                    DisplayName = "Research Lab",
                    Description = "Scientific research and prototype development.",
                    FacilityClass = "research"
                },
                new FacilityFamilyDefinition
                {
                    Id = "habitation",
                    DisplayName = "Habitation",
                    Description = "Housing blocks and life support stacks.",
                    FacilityClass = "habitation"
                }
            };
            hulls = new[]
            {
                new FacilityHullDefinition
                {
                    Id = "hull.desalination.core",
                    DisplayName = "Desalination Core",
                    Description = "Compact desalination hull with utility bays.",
                    FacilityFamilyId = "desalination",
                    ManufacturerId = "aegis_forge",
                    BaseMassTons = 220f,
                    BaseIntegrity = 260f,
                    BaseQuality01 = 0.62f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 2, MaxMassTons = 90f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 2, MaxMassTons = 60f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 70f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.hydroponics.core",
                    DisplayName = "Hydroponics Core",
                    Description = "Expanded hydroponics hull for algae growth.",
                    FacilityFamilyId = "hydroponics",
                    ManufacturerId = "lumen_covenant",
                    BaseMassTons = 240f,
                    BaseIntegrity = 240f,
                    BaseQuality01 = 0.64f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 2, MaxMassTons = 85f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 2, MaxMassTons = 65f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 70f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.bioprocessor.core",
                    DisplayName = "Bioprocessor Core",
                    Description = "Biotech hull suited for printed meat labs.",
                    FacilityFamilyId = "bioprocessor",
                    ManufacturerId = "orion_coilworks",
                    BaseMassTons = 260f,
                    BaseIntegrity = 260f,
                    BaseQuality01 = 0.66f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 2, MaxMassTons = 95f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 2, MaxMassTons = 70f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 80f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.salvage.core",
                    DisplayName = "Salvage Core",
                    Description = "Recycling hull with heavy processing bays.",
                    FacilityFamilyId = "salvage",
                    ManufacturerId = "vantrel_syndicate",
                    BaseMassTons = 280f,
                    BaseIntegrity = 300f,
                    BaseQuality01 = 0.58f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 2, MaxMassTons = 110f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 1, MaxMassTons = 70f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 90f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.scooper.core",
                    DisplayName = "Scooper Core",
                    Description = "Atmospheric intake hull for gas harvesting.",
                    FacilityFamilyId = "scooper",
                    ManufacturerId = "orion_coilworks",
                    BaseMassTons = 260f,
                    BaseIntegrity = 240f,
                    BaseQuality01 = 0.62f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 1, MaxMassTons = 90f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 2, MaxMassTons = 70f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 80f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.shipyard.core",
                    DisplayName = "Orbital Drydock Core",
                    Description = "Massive hull for shipyard assembly bays.",
                    FacilityFamilyId = "shipyard",
                    ManufacturerId = "aegis_forge",
                    BaseMassTons = 480f,
                    BaseIntegrity = 520f,
                    BaseQuality01 = 0.6f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 3, MaxMassTons = 160f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 2, MaxMassTons = 110f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 2, MaxMassTons = 140f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.module_fab.core",
                    DisplayName = "Module Fabrication Core",
                    Description = "Assembly hull for module lines.",
                    FacilityFamilyId = "module_fabrication",
                    ManufacturerId = "orion_coilworks",
                    BaseMassTons = 320f,
                    BaseIntegrity = 300f,
                    BaseQuality01 = 0.65f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 2, MaxMassTons = 110f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 2, MaxMassTons = 80f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 90f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.research.core",
                    DisplayName = "Research Core",
                    Description = "Laboratory hull with sensor arrays.",
                    FacilityFamilyId = "research_lab",
                    ManufacturerId = "lumen_covenant",
                    BaseMassTons = 260f,
                    BaseIntegrity = 230f,
                    BaseQuality01 = 0.68f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Production, Count = 1, MaxMassTons = 90f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 2, MaxMassTons = 85f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 80f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                },
                new FacilityHullDefinition
                {
                    Id = "hull.habitation.core",
                    DisplayName = "Habitation Core",
                    Description = "Habitat ring hull for population support.",
                    FacilityFamilyId = "habitation",
                    ManufacturerId = "lumen_covenant",
                    BaseMassTons = 340f,
                    BaseIntegrity = 280f,
                    BaseQuality01 = 0.64f,
                    AttachmentSlots = new[]
                    {
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Utility, Count = 3, MaxMassTons = 100f },
                        new FacilityAttachmentSlotDefinition { SlotType = FacilityAttachmentSlotTypeIds.Power, Count = 1, MaxMassTons = 90f }
                    },
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "control", Count = 1 }
                    }
                }
            };
            organs = new[]
            {
                new FacilityOrganDefinition { Id = "pump.aegis.m1", DisplayName = "Aegis Pump M1", SlotType = "pump", ManufacturerId = "aegis_forge", Quality = 0.72f, Efficiency = 0.7f, Throughput = 0.65f, Stability = 0.7f, PowerDraw = 0.5f, Reliability = 0.78f },
                new FacilityOrganDefinition { Id = "filter.aegis.m1", DisplayName = "Aegis Filter Stack", SlotType = "filter", ManufacturerId = "aegis_forge", Quality = 0.7f, Efficiency = 0.65f, Throughput = 0.6f, Stability = 0.7f, PowerDraw = 0.45f, Reliability = 0.75f },
                new FacilityOrganDefinition { Id = "distiller.orion.m1", DisplayName = "Orion Distiller", SlotType = "distiller", ManufacturerId = "orion_coilworks", Quality = 0.68f, Efficiency = 0.7f, Throughput = 0.6f, Stability = 0.65f, PowerDraw = 0.5f, Reliability = 0.7f },
                new FacilityOrganDefinition { Id = "bioreactor.lumen.m1", DisplayName = "Lumen Bioreactor", SlotType = "bioreactor", ManufacturerId = "lumen_covenant", Quality = 0.7f, Efficiency = 0.6f, Throughput = 0.55f, Stability = 0.75f, PowerDraw = 0.55f, Reliability = 0.72f },
                new FacilityOrganDefinition { Id = "lighting.lumen.m1", DisplayName = "Lumen Growth Lighting", SlotType = "lighting", ManufacturerId = "lumen_covenant", Quality = 0.66f, Efficiency = 0.55f, Throughput = 0.6f, Stability = 0.65f, PowerDraw = 0.5f, Reliability = 0.7f },
                new FacilityOrganDefinition { Id = "nutrient.orion.m1", DisplayName = "Orion Nutrient Mixer", SlotType = "nutrient", ManufacturerId = "orion_coilworks", Quality = 0.64f, Efficiency = 0.6f, Throughput = 0.55f, Stability = 0.6f, PowerDraw = 0.45f, Reliability = 0.68f },
                new FacilityOrganDefinition { Id = "printer.orion.m1", DisplayName = "Orion Bio-Printer", SlotType = "printer", ManufacturerId = "orion_coilworks", Quality = 0.7f, Efficiency = 0.65f, Throughput = 0.55f, Stability = 0.6f, PowerDraw = 0.6f, Reliability = 0.7f },
                new FacilityOrganDefinition { Id = "cooling.aegis.m1", DisplayName = "Aegis Cooling Loop", SlotType = "cooling", ManufacturerId = "aegis_forge", Quality = 0.7f, Efficiency = 0.6f, Throughput = 0.55f, Stability = 0.7f, PowerDraw = 0.45f, Reliability = 0.75f },
                new FacilityOrganDefinition { Id = "crusher.vantrel.m1", DisplayName = "Vantrel Crusher", SlotType = "crusher", ManufacturerId = "vantrel_syndicate", Quality = 0.58f, Efficiency = 0.55f, Throughput = 0.7f, Stability = 0.5f, PowerDraw = 0.55f, Reliability = 0.55f },
                new FacilityOrganDefinition { Id = "separator.vantrel.m1", DisplayName = "Vantrel Separator", SlotType = "separator", ManufacturerId = "vantrel_syndicate", Quality = 0.6f, Efficiency = 0.6f, Throughput = 0.6f, Stability = 0.55f, PowerDraw = 0.5f, Reliability = 0.58f },
                new FacilityOrganDefinition { Id = "smelter.aegis.m1", DisplayName = "Aegis Smelter Core", SlotType = "smelter", ManufacturerId = "aegis_forge", Quality = 0.7f, Efficiency = 0.65f, Throughput = 0.6f, Stability = 0.7f, PowerDraw = 0.6f, Reliability = 0.75f },
                new FacilityOrganDefinition { Id = "intake.orion.m1", DisplayName = "Orion Intake Array", SlotType = "intake", ManufacturerId = "orion_coilworks", Quality = 0.66f, Efficiency = 0.6f, Throughput = 0.65f, Stability = 0.6f, PowerDraw = 0.55f, Reliability = 0.68f },
                new FacilityOrganDefinition { Id = "compressor.orion.m1", DisplayName = "Orion Compressor", SlotType = "compressor", ManufacturerId = "orion_coilworks", Quality = 0.64f, Efficiency = 0.6f, Throughput = 0.6f, Stability = 0.6f, PowerDraw = 0.55f, Reliability = 0.66f },
                new FacilityOrganDefinition { Id = "scrubber.aegis.m1", DisplayName = "Aegis Scrubber", SlotType = "scrubber", ManufacturerId = "aegis_forge", Quality = 0.68f, Efficiency = 0.6f, Throughput = 0.55f, Stability = 0.65f, PowerDraw = 0.5f, Reliability = 0.7f },
                new FacilityOrganDefinition { Id = "gantry.aegis.m1", DisplayName = "Aegis Assembly Gantry", SlotType = "gantry", ManufacturerId = "aegis_forge", Quality = 0.7f, Efficiency = 0.65f, Throughput = 0.7f, Stability = 0.7f, PowerDraw = 0.7f, Reliability = 0.76f },
                new FacilityOrganDefinition { Id = "assembler.aegis.m1", DisplayName = "Aegis Assembly Core", SlotType = "assembler", ManufacturerId = "aegis_forge", Quality = 0.68f, Efficiency = 0.66f, Throughput = 0.66f, Stability = 0.68f, PowerDraw = 0.6f, Reliability = 0.74f },
                new FacilityOrganDefinition { Id = "calibration.orion.m1", DisplayName = "Orion Calibration Rig", SlotType = "calibration", ManufacturerId = "orion_coilworks", Quality = 0.67f, Efficiency = 0.64f, Throughput = 0.6f, Stability = 0.7f, PowerDraw = 0.5f, Reliability = 0.72f },
                new FacilityOrganDefinition { Id = "nanoforge.orion.m1", DisplayName = "Orion Nano Forge", SlotType = "nanoforge", ManufacturerId = "orion_coilworks", Quality = 0.7f, Efficiency = 0.66f, Throughput = 0.62f, Stability = 0.68f, PowerDraw = 0.65f, Reliability = 0.7f },
                new FacilityOrganDefinition { Id = "labcore.lumen.m1", DisplayName = "Lumen Lab Core", SlotType = "lab_core", ManufacturerId = "lumen_covenant", Quality = 0.72f, Efficiency = 0.7f, Throughput = 0.6f, Stability = 0.75f, PowerDraw = 0.6f, Reliability = 0.78f },
                new FacilityOrganDefinition { Id = "analyzer.lumen.m1", DisplayName = "Lumen Analyzer", SlotType = "analyzer", ManufacturerId = "lumen_covenant", Quality = 0.7f, Efficiency = 0.68f, Throughput = 0.58f, Stability = 0.72f, PowerDraw = 0.55f, Reliability = 0.76f },
                new FacilityOrganDefinition { Id = "datagrid.lumen.m1", DisplayName = "Lumen Data Grid", SlotType = "datagrid", ManufacturerId = "lumen_covenant", Quality = 0.69f, Efficiency = 0.66f, Throughput = 0.6f, Stability = 0.7f, PowerDraw = 0.5f, Reliability = 0.74f },
                new FacilityOrganDefinition { Id = "life_support.lumen.m1", DisplayName = "Lumen Life Support", SlotType = "life_support", ManufacturerId = "lumen_covenant", Quality = 0.66f, Efficiency = 0.62f, Throughput = 0.58f, Stability = 0.7f, PowerDraw = 0.5f, Reliability = 0.72f },
                new FacilityOrganDefinition { Id = "recycler.aegis.m1", DisplayName = "Aegis Habitat Recycler", SlotType = "recycler", ManufacturerId = "aegis_forge", Quality = 0.64f, Efficiency = 0.6f, Throughput = 0.55f, Stability = 0.65f, PowerDraw = 0.45f, Reliability = 0.7f },
                new FacilityOrganDefinition { Id = "hydro.orion.m1", DisplayName = "Orion Hydro Module", SlotType = "hydro", ManufacturerId = "orion_coilworks", Quality = 0.63f, Efficiency = 0.6f, Throughput = 0.55f, Stability = 0.6f, PowerDraw = 0.45f, Reliability = 0.68f }
            };
            limbs = new[]
            {
                new FacilityLimbDefinition
                {
                    Id = "limb.desalination_stack",
                    DisplayName = "Desalination Stack",
                    Description = "Converts brine and chemical byproducts into water.",
                    LimbType = FacilityLimbTypeIds.Production,
                    ManufacturerId = "aegis_forge",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "pump", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "filter", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "distiller", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.desalinate_brine" },
                    Tags = new[] { "water", "byproduct", "desalination" },
                    ProcessSlots = 1,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.6f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 70f,
                    Quality01 = 0.62f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.algae_vat",
                    DisplayName = "Algae Vat",
                    Description = "Grows algae for food and nutrient byproducts.",
                    LimbType = FacilityLimbTypeIds.Production,
                    ManufacturerId = "lumen_covenant",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "bioreactor", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "lighting", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "nutrient", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.algae_food" },
                    Tags = new[] { "food", "algae" },
                    ProcessSlots = 1,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.55f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 68f,
                    Quality01 = 0.64f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.printed_meat_forge",
                    DisplayName = "Printed Meat Forge",
                    Description = "Bio-printing unit for synthetic food output.",
                    LimbType = FacilityLimbTypeIds.Production,
                    ManufacturerId = "orion_coilworks",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "printer", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "bioreactor", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "cooling", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.printed_meat" },
                    Tags = new[] { "food", "biotech" },
                    ProcessSlots = 1,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1.05f,
                    QualityMultiplier = 1.05f,
                    PowerDraw = 0.7f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 78f,
                    Quality01 = 0.66f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.scrap_refinery",
                    DisplayName = "Scrap Refinery",
                    Description = "Refines wreckage into supplies and raw fuel.",
                    LimbType = FacilityLimbTypeIds.Production,
                    ManufacturerId = "vantrel_syndicate",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "crusher", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "separator", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "smelter", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.scrap_to_supplies" },
                    Tags = new[] { "recycling", "salvage" },
                    ProcessSlots = 1,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1.1f,
                    QualityMultiplier = 0.95f,
                    PowerDraw = 0.65f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 92f,
                    Quality01 = 0.58f,
                    CustomMadeDefault = true
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.gas_scooper",
                    DisplayName = "Gas Scooper",
                    Description = "Scoops and refines atmospheric gas into fuel.",
                    LimbType = FacilityLimbTypeIds.Production,
                    ManufacturerId = "orion_coilworks",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "intake", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "compressor", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "scrubber", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.raw_gas_to_fuel" },
                    Tags = new[] { "fuel", "scooping" },
                    ProcessSlots = 1,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.6f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 80f,
                    Quality01 = 0.63f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.shipyard_bay",
                    DisplayName = "Shipyard Bay",
                    Description = "Heavy assembly bay for hull construction.",
                    LimbType = FacilityLimbTypeIds.Production,
                    ManufacturerId = "aegis_forge",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "gantry", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "assembler", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "calibration", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.shipyard_lcv_sparrow", "proc.shipyard_cv_mule" },
                    Tags = new[] { "shipyard", "hull" },
                    ProcessSlots = 2,
                    ParallelChainSlots = 1,
                    ThroughputMultiplier = 1.15f,
                    QualityMultiplier = 1.05f,
                    PowerDraw = 0.9f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 120f,
                    Quality01 = 0.62f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.module_fab_line",
                    DisplayName = "Module Fabrication Line",
                    Description = "Assembly line for core ship modules.",
                    LimbType = FacilityLimbTypeIds.Production,
                    ManufacturerId = "orion_coilworks",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "nanoforge", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "assembler", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "calibration", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.module_engine_mk1", "proc.module_shield_s1", "proc.module_laser_s1" },
                    Tags = new[] { "module", "assembly" },
                    ProcessSlots = 2,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1.05f,
                    QualityMultiplier = 1.02f,
                    PowerDraw = 0.75f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 90f,
                    Quality01 = 0.64f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.research_lab",
                    DisplayName = "Research Lab",
                    Description = "Scientific lab for research output.",
                    LimbType = FacilityLimbTypeIds.Training,
                    ManufacturerId = "lumen_covenant",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "lab_core", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "analyzer", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "datagrid", Count = 1 }
                    },
                    SupportedProcessIds = new[] { "proc.research_packet" },
                    Tags = new[] { "research", "lab" },
                    ProcessSlots = 1,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1.05f,
                    QualityMultiplier = 1.1f,
                    PowerDraw = 0.6f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 70f,
                    Quality01 = 0.68f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.habitation_ring",
                    DisplayName = "Habitation Ring",
                    Description = "Habitat limb providing crew housing.",
                    LimbType = FacilityLimbTypeIds.Cargo,
                    ManufacturerId = "lumen_covenant",
                    OrganSlots = new[]
                    {
                        new FacilityOrganSlotDefinition { SlotType = "life_support", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "recycler", Count = 1 },
                        new FacilityOrganSlotDefinition { SlotType = "hydro", Count = 1 }
                    },
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "habitation", "housing" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.5f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0.6f,
                    MassTons = 105f,
                    Quality01 = 0.64f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.power_booster",
                    DisplayName = "Power Booster",
                    Description = "Capacitors and backup cells for facility power.",
                    LimbType = FacilityLimbTypeIds.Power,
                    ManufacturerId = "aegis_forge",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "power", "capacitor" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.2f,
                    PowerCapacityBonus = 0.35f,
                    CargoCapacityBonus = 0f,
                    MassTons = 55f,
                    Quality01 = 0.62f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.cargo_buffer",
                    DisplayName = "Cargo Buffer",
                    Description = "Buffer bays for staging inputs and outputs.",
                    LimbType = FacilityLimbTypeIds.Cargo,
                    ManufacturerId = "orion_coilworks",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "cargo", "buffer" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.1f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0.4f,
                    MassTons = 60f,
                    Quality01 = 0.6f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.automation_line",
                    DisplayName = "Automation Line",
                    Description = "Assembly line automation for higher throughput.",
                    LimbType = FacilityLimbTypeIds.Automation,
                    ManufacturerId = "aegis_forge",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "automation", "assembly" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1.15f,
                    QualityMultiplier = 0.98f,
                    PowerDraw = 0.4f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 70f,
                    Quality01 = 0.61f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.facility_head",
                    DisplayName = "Facility Head",
                    Description = "Adds parallel production management capacity.",
                    LimbType = FacilityLimbTypeIds.Head,
                    ManufacturerId = "lumen_covenant",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "head", "parallel" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 1,
                    ThroughputMultiplier = 1.05f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.3f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 50f,
                    Quality01 = 0.65f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.training_suite",
                    DisplayName = "Training Suite",
                    Description = "Staff training limb for proficiency gains.",
                    LimbType = FacilityLimbTypeIds.Training,
                    ManufacturerId = "lumen_covenant",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "training", "staff" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.2f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 45f,
                    Quality01 = 0.63f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.relations_office",
                    DisplayName = "Relations Office",
                    Description = "External relations and diplomacy wing.",
                    LimbType = FacilityLimbTypeIds.Relations,
                    ManufacturerId = "orion_coilworks",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "relations", "trade" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.15f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 42f,
                    Quality01 = 0.6f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.legal_battery",
                    DisplayName = "Legal Battery",
                    Description = "Compliance and legal retainers module.",
                    LimbType = FacilityLimbTypeIds.Legal,
                    ManufacturerId = "aegis_forge",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "legal", "compliance" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1f,
                    QualityMultiplier = 1f,
                    PowerDraw = 0.12f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 40f,
                    Quality01 = 0.62f,
                    CustomMadeDefault = false
                },
                new FacilityLimbDefinition
                {
                    Id = "limb.executive_office",
                    DisplayName = "Executive Office",
                    Description = "Board and executive offices for strategy.",
                    LimbType = FacilityLimbTypeIds.Executive,
                    ManufacturerId = "lumen_covenant",
                    OrganSlots = Array.Empty<FacilityOrganSlotDefinition>(),
                    SupportedProcessIds = Array.Empty<string>(),
                    Tags = new[] { "executive", "board" },
                    ProcessSlots = 0,
                    ParallelChainSlots = 0,
                    ThroughputMultiplier = 1.02f,
                    QualityMultiplier = 1.02f,
                    PowerDraw = 0.2f,
                    PowerCapacityBonus = 0f,
                    CargoCapacityBonus = 0f,
                    MassTons = 48f,
                    Quality01 = 0.66f,
                    CustomMadeDefault = false
                }
            };
            processes = new[]
            {
                new FacilityProcessDefinition
                {
                    Id = "proc.desalinate_brine",
                    DisplayName = "Desalinate Brine",
                    Description = "Recover water from brine and chemical byproducts.",
                    Stage = ProductionStage.Refining,
                    AllowedLimbIds = new[] { "limb.desalination_stack" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_byproduct_brine", Quantity = 10f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_water", Quantity = 8f, QualityFloor01 = 0.4f, IsByproduct = false },
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_salt", Quantity = 2f, QualityFloor01 = 0.3f, IsByproduct = true }
                    },
                    BaseTimeSeconds = 14f,
                    PowerCost = 1.1f,
                    LaborCost = 0.8f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.algae_food",
                    DisplayName = "Algae Food",
                    Description = "Convert organic slurry into edible biomass.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.algae_vat" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_organic_slurry", Quantity = 6f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_food", Quantity = 5f, QualityFloor01 = 0.45f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 16f,
                    PowerCost = 0.9f,
                    LaborCost = 0.9f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.printed_meat",
                    DisplayName = "Printed Meat",
                    Description = "Bio-print nutrient packs into food rations.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.printed_meat_forge" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_biomass", Quantity = 8f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_food", Quantity = 6f, QualityFloor01 = 0.5f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 18f,
                    PowerCost = 1.2f,
                    LaborCost = 1.0f,
                    MinTechTier = 2
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.scrap_to_supplies",
                    DisplayName = "Scrap to Supplies",
                    Description = "Refine wreckage into supplies and reusable feedstock.",
                    Stage = ProductionStage.Refining,
                    AllowedLimbIds = new[] { "limb.scrap_refinery" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_scrap", Quantity = 12f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_supplies", Quantity = 6f, QualityFloor01 = 0.4f, IsByproduct = false },
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_salvage_sludge", Quantity = 2f, QualityFloor01 = 0.3f, IsByproduct = true }
                    },
                    BaseTimeSeconds = 20f,
                    PowerCost = 1.1f,
                    LaborCost = 0.9f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.raw_gas_to_fuel",
                    DisplayName = "Raw Gas to Fuel",
                    Description = "Distill scooped gas into usable fuel.",
                    Stage = ProductionStage.Refining,
                    AllowedLimbIds = new[] { "limb.gas_scooper" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_gas_raw", Quantity = 10f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_fuel", Quantity = 5f, QualityFloor01 = 0.4f, IsByproduct = false },
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_volatile_residue", Quantity = 2f, QualityFloor01 = 0.3f, IsByproduct = true }
                    },
                    BaseTimeSeconds = 18f,
                    PowerCost = 1.0f,
                    LaborCost = 0.8f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.shipyard_lcv_sparrow",
                    DisplayName = "Assemble LCV Sparrow",
                    Description = "Assemble a light courier hull.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.shipyard_bay" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_parts", Quantity = 30f, MinPurity01 = 0f, MinQuality01 = 0f },
                        new FacilityProcessInputDefinition { ResourceId = "space4x_alloy", Quantity = 40f, MinPurity01 = 0f, MinQuality01 = 0f },
                        new FacilityProcessInputDefinition { ResourceId = "space4x_ingot", Quantity = 20f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "lcv-sparrow", Quantity = 1f, QualityFloor01 = 0.5f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 40f,
                    PowerCost = 2.2f,
                    LaborCost = 1.2f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.shipyard_cv_mule",
                    DisplayName = "Assemble CV Mule",
                    Description = "Assemble a carrier hull.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.shipyard_bay" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_parts", Quantity = 60f, MinPurity01 = 0f, MinQuality01 = 0f },
                        new FacilityProcessInputDefinition { ResourceId = "space4x_alloy", Quantity = 80f, MinPurity01 = 0f, MinQuality01 = 0f },
                        new FacilityProcessInputDefinition { ResourceId = "space4x_ingot", Quantity = 40f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "cv-mule", Quantity = 1f, QualityFloor01 = 0.55f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 60f,
                    PowerCost = 2.6f,
                    LaborCost = 1.4f,
                    MinTechTier = 2
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.module_engine_mk1",
                    DisplayName = "Assemble Engine Mk1",
                    Description = "Assemble a basic engine module.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.module_fab_line" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_parts", Quantity = 6f, MinPurity01 = 0f, MinQuality01 = 0f },
                        new FacilityProcessInputDefinition { ResourceId = "space4x_alloy", Quantity = 8f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "engine-mk1", Quantity = 1f, QualityFloor01 = 0.5f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 14f,
                    PowerCost = 1.1f,
                    LaborCost = 1.0f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.module_shield_s1",
                    DisplayName = "Assemble Shield S1",
                    Description = "Assemble a small shield module.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.module_fab_line" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_parts", Quantity = 5f, MinPurity01 = 0f, MinQuality01 = 0f },
                        new FacilityProcessInputDefinition { ResourceId = "space4x_alloy", Quantity = 7f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "shield-s-1", Quantity = 1f, QualityFloor01 = 0.5f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 13f,
                    PowerCost = 1.05f,
                    LaborCost = 1.0f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.module_laser_s1",
                    DisplayName = "Assemble Laser S1",
                    Description = "Assemble a small laser module.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.module_fab_line" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_parts", Quantity = 5f, MinPurity01 = 0f, MinQuality01 = 0f },
                        new FacilityProcessInputDefinition { ResourceId = "space4x_alloy", Quantity = 6f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "laser-s-1", Quantity = 1f, QualityFloor01 = 0.5f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 12f,
                    PowerCost = 1.0f,
                    LaborCost = 0.9f,
                    MinTechTier = 1
                },
                new FacilityProcessDefinition
                {
                    Id = "proc.research_packet",
                    DisplayName = "Research Packet",
                    Description = "Convert supplies into research output.",
                    Stage = ProductionStage.Crafting,
                    AllowedLimbIds = new[] { "limb.research_lab" },
                    Inputs = new[]
                    {
                        new FacilityProcessInputDefinition { ResourceId = "space4x_supplies", Quantity = 12f, MinPurity01 = 0f, MinQuality01 = 0f }
                    },
                    Outputs = new[]
                    {
                        new FacilityProcessOutputDefinition { ResourceId = "space4x_research", Quantity = 5f, QualityFloor01 = 0.5f, IsByproduct = false }
                    },
                    BaseTimeSeconds = 10f,
                    PowerCost = 0.8f,
                    LaborCost = 0.9f,
                    MinTechTier = 1
                }
            };
            models = new[]
            {
                new FacilityModelDefinition
                {
                    Id = "facility.desalination_plant",
                    DisplayName = "Desalination Plant",
                    Description = "Water recovery facility for colonies and stations.",
                    FacilityFamilyId = "desalination",
                    ManufacturerId = "aegis_forge",
                    HullId = "hull.desalination.core",
                    BlueprintId = "bp_facility_desalination",
                    DefaultLimbIds = new[]
                    {
                        "limb.desalination_stack",
                        "limb.power_booster",
                        "limb.cargo_buffer"
                    },
                    DefaultProcessIds = new[] { "proc.desalinate_brine" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 4800f,
                        PermitCostCredits = 400f,
                        ConstructionTimeSeconds = 220f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 24f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_alloy", UnitsRequired = 18f }
                        },
                        PayrollBudgetPerSecond = 0.4f,
                        MaintenanceBudgetPerSecond = 0.18f,
                        EmployerTaxRate01 = 0.08f,
                        EmployeeTaxWithholding01 = 0.06f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "technician", MinCount = 2, MaxCount = 4, WagePerSecond = 0.02f, SkillRequirement01 = 0.4f },
                            new FacilityStaffRoleDefinition { RoleId = "operator", MinCount = 1, MaxCount = 2, WagePerSecond = 0.03f, SkillRequirement01 = 0.5f }
                        },
                        PayrollVariance01 = 0.15f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.algae_farm",
                    DisplayName = "Algae Farm",
                    Description = "Hydroponic algae farm for colony food supply.",
                    FacilityFamilyId = "hydroponics",
                    ManufacturerId = "lumen_covenant",
                    HullId = "hull.hydroponics.core",
                    BlueprintId = "bp_facility_algae_farm",
                    DefaultLimbIds = new[]
                    {
                        "limb.algae_vat",
                        "limb.automation_line",
                        "limb.training_suite"
                    },
                    DefaultProcessIds = new[] { "proc.algae_food" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 5200f,
                        PermitCostCredits = 450f,
                        ConstructionTimeSeconds = 240f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 26f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_supplies", UnitsRequired = 12f }
                        },
                        PayrollBudgetPerSecond = 0.45f,
                        MaintenanceBudgetPerSecond = 0.2f,
                        EmployerTaxRate01 = 0.08f,
                        EmployeeTaxWithholding01 = 0.06f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "biotech", MinCount = 2, MaxCount = 4, WagePerSecond = 0.025f, SkillRequirement01 = 0.55f },
                            new FacilityStaffRoleDefinition { RoleId = "operator", MinCount = 1, MaxCount = 2, WagePerSecond = 0.03f, SkillRequirement01 = 0.5f }
                        },
                        PayrollVariance01 = 0.18f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.printed_meat_lab",
                    DisplayName = "Printed Meat Lab",
                    Description = "Synthetic food lab for high output rations.",
                    FacilityFamilyId = "bioprocessor",
                    ManufacturerId = "orion_coilworks",
                    HullId = "hull.bioprocessor.core",
                    BlueprintId = "bp_facility_printed_meat",
                    DefaultLimbIds = new[]
                    {
                        "limb.printed_meat_forge",
                        "limb.facility_head",
                        "limb.executive_office"
                    },
                    DefaultProcessIds = new[] { "proc.printed_meat" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 7600f,
                        PermitCostCredits = 620f,
                        ConstructionTimeSeconds = 320f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 32f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_alloy", UnitsRequired = 16f }
                        },
                        PayrollBudgetPerSecond = 0.6f,
                        MaintenanceBudgetPerSecond = 0.28f,
                        EmployerTaxRate01 = 0.09f,
                        EmployeeTaxWithholding01 = 0.07f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "biotech", MinCount = 2, MaxCount = 3, WagePerSecond = 0.03f, SkillRequirement01 = 0.6f },
                            new FacilityStaffRoleDefinition { RoleId = "engineer", MinCount = 1, MaxCount = 2, WagePerSecond = 0.035f, SkillRequirement01 = 0.6f }
                        },
                        PayrollVariance01 = 0.2f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.scrap_recycler",
                    DisplayName = "Scrap Recycler",
                    Description = "Derelict recycler for supplies and feedstock.",
                    FacilityFamilyId = "salvage",
                    ManufacturerId = "vantrel_syndicate",
                    HullId = "hull.salvage.core",
                    BlueprintId = "bp_facility_scrap_recycler",
                    DefaultLimbIds = new[]
                    {
                        "limb.scrap_refinery",
                        "limb.cargo_buffer",
                        "limb.legal_battery"
                    },
                    DefaultProcessIds = new[] { "proc.scrap_to_supplies" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 4200f,
                        PermitCostCredits = 320f,
                        ConstructionTimeSeconds = 200f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 22f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_supplies", UnitsRequired = 10f }
                        },
                        PayrollBudgetPerSecond = 0.35f,
                        MaintenanceBudgetPerSecond = 0.16f,
                        EmployerTaxRate01 = 0.07f,
                        EmployeeTaxWithholding01 = 0.05f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "operator", MinCount = 2, MaxCount = 4, WagePerSecond = 0.02f, SkillRequirement01 = 0.45f }
                        },
                        PayrollVariance01 = 0.2f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.gas_scooper",
                    DisplayName = "Gas Scooper",
                    Description = "Atmospheric intake facility for raw fuel.",
                    FacilityFamilyId = "scooper",
                    ManufacturerId = "orion_coilworks",
                    HullId = "hull.scooper.core",
                    BlueprintId = "bp_facility_gas_scooper",
                    DefaultLimbIds = new[]
                    {
                        "limb.gas_scooper",
                        "limb.power_booster",
                        "limb.relations_office"
                    },
                    DefaultProcessIds = new[] { "proc.raw_gas_to_fuel" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 5400f,
                        PermitCostCredits = 480f,
                        ConstructionTimeSeconds = 260f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 28f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_alloy", UnitsRequired = 14f }
                        },
                        PayrollBudgetPerSecond = 0.4f,
                        MaintenanceBudgetPerSecond = 0.2f,
                        EmployerTaxRate01 = 0.08f,
                        EmployeeTaxWithholding01 = 0.06f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "technician", MinCount = 2, MaxCount = 3, WagePerSecond = 0.02f, SkillRequirement01 = 0.45f },
                            new FacilityStaffRoleDefinition { RoleId = "operator", MinCount = 1, MaxCount = 2, WagePerSecond = 0.03f, SkillRequirement01 = 0.5f }
                        },
                        PayrollVariance01 = 0.15f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.shipyard_drydock",
                    DisplayName = "Orbital Shipyard",
                    Description = "Drydock facility for hull assembly.",
                    FacilityFamilyId = "shipyard",
                    ManufacturerId = "aegis_forge",
                    HullId = "hull.shipyard.core",
                    BlueprintId = "bp_facility_shipyard",
                    DefaultLimbIds = new[]
                    {
                        "limb.shipyard_bay",
                        "limb.power_booster",
                        "limb.cargo_buffer",
                        "limb.facility_head"
                    },
                    DefaultProcessIds = new[] { "proc.shipyard_lcv_sparrow", "proc.shipyard_cv_mule" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 12500f,
                        PermitCostCredits = 1200f,
                        ConstructionTimeSeconds = 520f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 80f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_alloy", UnitsRequired = 60f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_ingot", UnitsRequired = 40f }
                        },
                        PayrollBudgetPerSecond = 0.8f,
                        MaintenanceBudgetPerSecond = 0.45f,
                        EmployerTaxRate01 = 0.1f,
                        EmployeeTaxWithholding01 = 0.08f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "engineer", MinCount = 3, MaxCount = 6, WagePerSecond = 0.05f, SkillRequirement01 = 0.6f },
                            new FacilityStaffRoleDefinition { RoleId = "operator", MinCount = 2, MaxCount = 4, WagePerSecond = 0.04f, SkillRequirement01 = 0.55f }
                        },
                        PayrollVariance01 = 0.2f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.module_fab",
                    DisplayName = "Module Fabrication Bay",
                    Description = "Module assembly facility for core ship systems.",
                    FacilityFamilyId = "module_fabrication",
                    ManufacturerId = "orion_coilworks",
                    HullId = "hull.module_fab.core",
                    BlueprintId = "bp_facility_module_fab",
                    DefaultLimbIds = new[]
                    {
                        "limb.module_fab_line",
                        "limb.automation_line",
                        "limb.power_booster"
                    },
                    DefaultProcessIds = new[] { "proc.module_engine_mk1", "proc.module_shield_s1", "proc.module_laser_s1" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 7200f,
                        PermitCostCredits = 640f,
                        ConstructionTimeSeconds = 320f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 36f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_alloy", UnitsRequired = 20f }
                        },
                        PayrollBudgetPerSecond = 0.5f,
                        MaintenanceBudgetPerSecond = 0.24f,
                        EmployerTaxRate01 = 0.08f,
                        EmployeeTaxWithholding01 = 0.06f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "engineer", MinCount = 2, MaxCount = 4, WagePerSecond = 0.04f, SkillRequirement01 = 0.55f },
                            new FacilityStaffRoleDefinition { RoleId = "operator", MinCount = 2, MaxCount = 4, WagePerSecond = 0.03f, SkillRequirement01 = 0.5f }
                        },
                        PayrollVariance01 = 0.18f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.research_lab",
                    DisplayName = "Research Lab",
                    Description = "Research facility for knowledge production.",
                    FacilityFamilyId = "research_lab",
                    ManufacturerId = "lumen_covenant",
                    HullId = "hull.research.core",
                    BlueprintId = "bp_facility_research",
                    DefaultLimbIds = new[]
                    {
                        "limb.research_lab",
                        "limb.training_suite",
                        "limb.relations_office"
                    },
                    DefaultProcessIds = new[] { "proc.research_packet" },
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 6800f,
                        PermitCostCredits = 600f,
                        ConstructionTimeSeconds = 300f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 28f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_supplies", UnitsRequired = 14f }
                        },
                        PayrollBudgetPerSecond = 0.48f,
                        MaintenanceBudgetPerSecond = 0.22f,
                        EmployerTaxRate01 = 0.08f,
                        EmployeeTaxWithholding01 = 0.06f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "researcher", MinCount = 3, MaxCount = 5, WagePerSecond = 0.045f, SkillRequirement01 = 0.6f },
                            new FacilityStaffRoleDefinition { RoleId = "technician", MinCount = 1, MaxCount = 2, WagePerSecond = 0.03f, SkillRequirement01 = 0.45f }
                        },
                        PayrollVariance01 = 0.2f
                    }
                },
                new FacilityModelDefinition
                {
                    Id = "facility.habitation_block",
                    DisplayName = "Habitation Block",
                    Description = "Housing and life support facility.",
                    FacilityFamilyId = "habitation",
                    ManufacturerId = "lumen_covenant",
                    HullId = "hull.habitation.core",
                    BlueprintId = "bp_facility_habitation",
                    DefaultLimbIds = new[]
                    {
                        "limb.habitation_ring",
                        "limb.power_booster",
                        "limb.legal_battery"
                    },
                    DefaultProcessIds = Array.Empty<string>(),
                    DefaultStaffingProfileId = "staffing.standard_3x8",
                    Investment = new FacilityInvestmentDefinition
                    {
                        InitialCapitalCredits = 5600f,
                        PermitCostCredits = 520f,
                        ConstructionTimeSeconds = 260f,
                        ResourceCosts = new[]
                        {
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 30f },
                            new FacilityConstructionCostDefinition { ResourceId = "space4x_supplies", UnitsRequired = 16f }
                        },
                        PayrollBudgetPerSecond = 0.32f,
                        MaintenanceBudgetPerSecond = 0.18f,
                        EmployerTaxRate01 = 0.07f,
                        EmployeeTaxWithholding01 = 0.05f
                    },
                    Staffing = new FacilityStaffingDefinition
                    {
                        Roles = new[]
                        {
                            new FacilityStaffRoleDefinition { RoleId = "steward", MinCount = 2, MaxCount = 4, WagePerSecond = 0.02f, SkillRequirement01 = 0.4f }
                        },
                        PayrollVariance01 = 0.15f
                    }
                }
            };
        }
    }
}
