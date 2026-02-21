using System;
using PureDOTS.Runtime.Economy.Production;
using UnityEngine;

namespace Space4X.Registry
{
    [CreateAssetMenu(fileName = "Space4XShipModelCatalog", menuName = "Space4X/Registry/Ship Model Catalog")]
    public sealed class Space4XShipModelCatalog : ScriptableObject
    {
        public const string ResourcePath = "Registry/Space4XShipModelCatalog";

        [SerializeField] private ShipChassisDefinition[] chassis = Array.Empty<ShipChassisDefinition>();
        [SerializeField] private HullSegmentDefinition[] segments = Array.Empty<HullSegmentDefinition>();
        [SerializeField] private ShipModelDefinition[] models = Array.Empty<ShipModelDefinition>();

        public ShipChassisDefinition[] Chassis => chassis;
        public HullSegmentDefinition[] Segments => segments;
        public ShipModelDefinition[] Models => models;

        public static Space4XShipModelCatalog LoadOrFallback()
        {
            var catalog = Resources.Load<Space4XShipModelCatalog>(ResourcePath);
            if (catalog == null)
            {
                catalog = CreateRuntimeFallback();
            }

            return catalog;
        }

        public static Space4XShipModelCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XShipModelCatalog>();
            catalog.ApplyRuntimeDefaults();
            return catalog;
        }

        public void ApplyRuntimeDefaults()
        {
            chassis = new[]
            {
                new ShipChassisDefinition
                {
                    Id = "lcv-sparrow",
                    DisplayName = "LCV Sparrow",
                    Description = "Light courier chassis suited for quick refits.",
                    ManufacturerId = "orion_coilworks",
                    Role = "Light Courier",
                    SegmentSlotCount = 3,
                    ModuleSocketCount = 4,
                    BaseMassTons = 140f,
                    BaseIntegrity = 120f
                },
                new ShipChassisDefinition
                {
                    Id = "cv-mule",
                    DisplayName = "CV Mule",
                    Description = "Heavy cargo chassis with modular bays.",
                    ManufacturerId = "aegis_forge",
                    Role = "Carrier",
                    SegmentSlotCount = 5,
                    ModuleSocketCount = 6,
                    BaseMassTons = 320f,
                    BaseIntegrity = 260f
                }
            };

            segments = new[]
            {
                new HullSegmentDefinition
                {
                    Id = "hull_bastion_ring",
                    DisplayName = "Bastion Ring",
                    SegmentType = "ring",
                    ManufacturerId = "aegis_forge",
                    ModuleSocketCount = 2,
                    MassTons = 48f,
                    IntegrityBonus = 55f,
                    TurnRateMultiplier = 0.9f,
                    AccelerationMultiplier = 0.95f,
                    DecelerationMultiplier = 0.95f,
                    MaxSpeedMultiplier = 0.9f
                },
                new HullSegmentDefinition
                {
                    Id = "hull_raider_spine",
                    DisplayName = "Raider Spine",
                    SegmentType = "spine",
                    ManufacturerId = "vantrel_syndicate",
                    ModuleSocketCount = 1,
                    MassTons = 34f,
                    IntegrityBonus = 35f,
                    TurnRateMultiplier = 1.05f,
                    AccelerationMultiplier = 1.1f,
                    DecelerationMultiplier = 1.05f,
                    MaxSpeedMultiplier = 1.1f
                },
                new HullSegmentDefinition
                {
                    Id = "hull_lumen_keel",
                    DisplayName = "Lumen Keel",
                    SegmentType = "keel",
                    ManufacturerId = "lumen_covenant",
                    ModuleSocketCount = 1,
                    MassTons = 38f,
                    IntegrityBonus = 42f,
                    TurnRateMultiplier = 1.0f,
                    AccelerationMultiplier = 1.0f,
                    DecelerationMultiplier = 1.0f,
                    MaxSpeedMultiplier = 1.0f
                }
            };

            models = new[]
            {
                new ShipModelDefinition
                {
                    Id = "model.lcv.sparrow",
                    DisplayName = "LCV Sparrow",
                    Description = "Starter courier layout with light modular bays.",
                    ChassisId = "lcv-sparrow",
                    ManufacturerId = "orion_coilworks",
                    BlueprintId = "bp_ship_lcv_sparrow",
                    DefaultSegmentIds = new[]
                    {
                        "hull_raider_spine",
                        "hull_lumen_keel"
                    },
                    DefaultModuleIds = new[]
                    {
                        "reactor-mk1",
                        "engine-mk1",
                        "laser-s-1"
                    },
                    DefaultStaffingProfileId = "staffing.standard_3x8"
                },
                new ShipModelDefinition
                {
                    Id = "model.cv.mule",
                    DisplayName = "CV Mule",
                    Description = "Heavy carrier layout built for modular expansion.",
                    ChassisId = "cv-mule",
                    ManufacturerId = "aegis_forge",
                    BlueprintId = "bp_ship_cv_mule",
                    DefaultSegmentIds = new[]
                    {
                        "hull_bastion_ring",
                        "hull_raider_spine",
                        "hull_lumen_keel"
                    },
                    DefaultModuleIds = new[]
                    {
                        "reactor-mk2",
                        "engine-mk2",
                        "hangar-s-1",
                        "shield-m-1"
                    },
                    DefaultStaffingProfileId = "staffing.standard_3x8"
                }
            };
        }
    }
}
