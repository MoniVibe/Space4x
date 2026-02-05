using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modules;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Bootstraps default module/hull catalogs and tuning if they don't exist from authoring.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ModuleCatalogBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ModuleCatalogSingleton>(out _))
            {
                CreateDefaultModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<HullCatalogSingleton>(out _))
            {
                CreateDefaultHullCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<RefitRepairTuningSingleton>(out _))
            {
                CreateDefaultTuning(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<EngineModuleCatalogSingleton>(out _))
            {
                CreateDefaultEngineModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<ShieldModuleCatalogSingleton>(out _))
            {
                CreateDefaultShieldModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<SensorModuleCatalogSingleton>(out _))
            {
                CreateDefaultSensorModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<ArmorModuleCatalogSingleton>(out _))
            {
                CreateDefaultArmorModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<WeaponModuleCatalogSingleton>(out _))
            {
                CreateDefaultWeaponModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<BridgeModuleCatalogSingleton>(out _))
            {
                CreateDefaultBridgeModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<CockpitModuleCatalogSingleton>(out _))
            {
                CreateDefaultCockpitModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<AmmoModuleCatalogSingleton>(out _))
            {
                CreateDefaultAmmoModuleCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<WeaponCatalogSingleton>(out _))
            {
                CreateDefaultWeaponCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<ProjectileCatalogSingleton>(out _))
            {
                CreateDefaultProjectileCatalog(ref state);
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (var catalogRef in SystemAPI.Query<RefRW<ModuleCatalogSingleton>>())
            {
                if (catalogRef.ValueRO.Catalog.IsCreated)
                {
                    catalogRef.ValueRO.Catalog.Dispose();
                    catalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var hullRef in SystemAPI.Query<RefRW<HullCatalogSingleton>>())
            {
                if (hullRef.ValueRO.Catalog.IsCreated)
                {
                    hullRef.ValueRO.Catalog.Dispose();
                    hullRef.ValueRW.Catalog = default;
                }
            }

            foreach (var tuningRef in SystemAPI.Query<RefRW<RefitRepairTuningSingleton>>())
            {
                if (tuningRef.ValueRO.Tuning.IsCreated)
                {
                    tuningRef.ValueRO.Tuning.Dispose();
                    tuningRef.ValueRW.Tuning = default;
                }
            }

            foreach (var engineCatalogRef in SystemAPI.Query<RefRW<EngineModuleCatalogSingleton>>())
            {
                if (engineCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    engineCatalogRef.ValueRO.Catalog.Dispose();
                    engineCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var shieldCatalogRef in SystemAPI.Query<RefRW<ShieldModuleCatalogSingleton>>())
            {
                if (shieldCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    shieldCatalogRef.ValueRO.Catalog.Dispose();
                    shieldCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var sensorCatalogRef in SystemAPI.Query<RefRW<SensorModuleCatalogSingleton>>())
            {
                if (sensorCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    sensorCatalogRef.ValueRO.Catalog.Dispose();
                    sensorCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var armorCatalogRef in SystemAPI.Query<RefRW<ArmorModuleCatalogSingleton>>())
            {
                if (armorCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    armorCatalogRef.ValueRO.Catalog.Dispose();
                    armorCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var weaponCatalogRef in SystemAPI.Query<RefRW<WeaponModuleCatalogSingleton>>())
            {
                if (weaponCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    weaponCatalogRef.ValueRO.Catalog.Dispose();
                    weaponCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var bridgeCatalogRef in SystemAPI.Query<RefRW<BridgeModuleCatalogSingleton>>())
            {
                if (bridgeCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    bridgeCatalogRef.ValueRO.Catalog.Dispose();
                    bridgeCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var cockpitCatalogRef in SystemAPI.Query<RefRW<CockpitModuleCatalogSingleton>>())
            {
                if (cockpitCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    cockpitCatalogRef.ValueRO.Catalog.Dispose();
                    cockpitCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var ammoCatalogRef in SystemAPI.Query<RefRW<AmmoModuleCatalogSingleton>>())
            {
                if (ammoCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    ammoCatalogRef.ValueRO.Catalog.Dispose();
                    ammoCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var weaponCatalogRef in SystemAPI.Query<RefRW<WeaponCatalogSingleton>>())
            {
                if (weaponCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    weaponCatalogRef.ValueRO.Catalog.Dispose();
                    weaponCatalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var projectileCatalogRef in SystemAPI.Query<RefRW<ProjectileCatalogSingleton>>())
            {
                if (projectileCatalogRef.ValueRO.Catalog.IsCreated)
                {
                    projectileCatalogRef.ValueRO.Catalog.Dispose();
                    projectileCatalogRef.ValueRW.Catalog = default;
                }
            }
        }

        private void CreateDefaultModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<ModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 17);

            moduleArray[0] = new ModuleSpec { Id = new FixedString64Bytes("reactor-mk1"), Class = ModuleClass.Reactor, RequiredMount = MountType.Core, RequiredSize = MountSize.S, MassTons = 40f, PowerDrawMW = -120f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 2, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[1] = new ModuleSpec { Id = new FixedString64Bytes("engine-mk1"), Class = ModuleClass.Engine, RequiredMount = MountType.Engine, RequiredSize = MountSize.S, MassTons = 20f, PowerDrawMW = 30f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 2, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[2] = new ModuleSpec { Id = new FixedString64Bytes("laser-s-1"), Class = ModuleClass.Laser, RequiredMount = MountType.Weapon, RequiredSize = MountSize.S, MassTons = 8f, PowerDrawMW = 15f, OffenseRating = 3, DefenseRating = 0, UtilityRating = 0, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[3] = new ModuleSpec { Id = new FixedString64Bytes("pd-s-1"), Class = ModuleClass.PointDefense, RequiredMount = MountType.Weapon, RequiredSize = MountSize.S, MassTons = 6f, PowerDrawMW = 8f, OffenseRating = 1, DefenseRating = 2, UtilityRating = 0, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[4] = new ModuleSpec { Id = new FixedString64Bytes("missile-s-1"), Class = ModuleClass.Missile, RequiredMount = MountType.Weapon, RequiredSize = MountSize.S, MassTons = 9f, PowerDrawMW = 12f, OffenseRating = 4, DefenseRating = 0, UtilityRating = 0, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[5] = new ModuleSpec { Id = new FixedString64Bytes("shield-s-1"), Class = ModuleClass.Shield, RequiredMount = MountType.Defense, RequiredSize = MountSize.S, MassTons = 18f, PowerDrawMW = 35f, OffenseRating = 0, DefenseRating = 4, UtilityRating = 0, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[6] = new ModuleSpec { Id = new FixedString64Bytes("armor-s-1"), Class = ModuleClass.Armor, RequiredMount = MountType.Defense, RequiredSize = MountSize.S, MassTons = 14f, PowerDrawMW = 0f, OffenseRating = 0, DefenseRating = 3, UtilityRating = 0, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[7] = new ModuleSpec { Id = new FixedString64Bytes("hangar-s-1"), Class = ModuleClass.Hangar, RequiredMount = MountType.Hangar, RequiredSize = MountSize.S, MassTons = 60f, PowerDrawMW = 80f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 3, DefaultEfficiency = 1f, Function = ModuleFunction.HangarCapacity, FunctionCapacity = 4f, FunctionDescription = new FixedString64Bytes("Small hangar bay") };
            moduleArray[8] = new ModuleSpec { Id = new FixedString64Bytes("repair-s-1"), Class = ModuleClass.RepairDrones, RequiredMount = MountType.Utility, RequiredSize = MountSize.S, MassTons = 10f, PowerDrawMW = 20f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 4, DefaultEfficiency = 1f, Function = ModuleFunction.RepairFacility, FunctionCapacity = 1f, FunctionDescription = new FixedString64Bytes("Repair drone bay") };
            moduleArray[9] = new ModuleSpec { Id = new FixedString64Bytes("scanner-s-1"), Class = ModuleClass.Scanner, RequiredMount = MountType.Utility, RequiredSize = MountSize.S, MassTons = 5f, PowerDrawMW = 5f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 2, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[10] = new ModuleSpec { Id = new FixedString64Bytes("reactor-mk2"), Class = ModuleClass.Reactor, RequiredMount = MountType.Core, RequiredSize = MountSize.M, MassTons = 65f, PowerDrawMW = -200f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 3, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[11] = new ModuleSpec { Id = new FixedString64Bytes("engine-mk2"), Class = ModuleClass.Engine, RequiredMount = MountType.Engine, RequiredSize = MountSize.M, MassTons = 32f, PowerDrawMW = 50f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 3, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[12] = new ModuleSpec { Id = new FixedString64Bytes("missile-m-1"), Class = ModuleClass.Missile, RequiredMount = MountType.Weapon, RequiredSize = MountSize.M, MassTons = 16f, PowerDrawMW = 20f, OffenseRating = 7, DefenseRating = 0, UtilityRating = 0, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[13] = new ModuleSpec { Id = new FixedString64Bytes("shield-m-1"), Class = ModuleClass.Shield, RequiredMount = MountType.Defense, RequiredSize = MountSize.M, MassTons = 22f, PowerDrawMW = 50f, OffenseRating = 0, DefenseRating = 6, UtilityRating = 0, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("") };
            moduleArray[14] = new ModuleSpec { Id = new FixedString64Bytes("bridge-mk1"), Class = ModuleClass.Bridge, RequiredMount = MountType.Utility, RequiredSize = MountSize.S, MassTons = 12f, PowerDrawMW = 10f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 4, DefaultEfficiency = 1f, Function = ModuleFunction.Command, FunctionCapacity = 1f, FunctionDescription = new FixedString64Bytes("Bridge command deck") };
            moduleArray[15] = new ModuleSpec { Id = new FixedString64Bytes("cockpit-mk1"), Class = ModuleClass.Cockpit, RequiredMount = MountType.Utility, RequiredSize = MountSize.S, MassTons = 6f, PowerDrawMW = 6f, OffenseRating = 0, DefenseRating = 0, UtilityRating = 2, DefaultEfficiency = 1f, Function = ModuleFunction.Command, FunctionCapacity = 1f, FunctionDescription = new FixedString64Bytes("Pilot cockpit") };
            moduleArray[16] = new ModuleSpec { Id = new FixedString64Bytes("ammo-bay-s-1"), Class = ModuleClass.Ammunition, RequiredMount = MountType.Utility, RequiredSize = MountSize.S, MassTons = 12f, PowerDrawMW = 2f, OffenseRating = 0, DefenseRating = 1, UtilityRating = 1, DefaultEfficiency = 1f, Function = ModuleFunction.None, FunctionCapacity = 0f, FunctionDescription = new FixedString64Bytes("Ammo bay") };

            var blobAsset = builder.CreateBlobAssetReference<ModuleCatalogBlob>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new ModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultHullCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<HullCatalogBlob>();
            var hullArray = builder.Allocate(ref catalogBlob.Hulls, 2);

            var sparrowSlots = builder.Allocate(ref hullArray[0].Slots, 7);
            sparrowSlots[0] = new HullSlot { Type = MountType.Core, Size = MountSize.S };
            sparrowSlots[1] = new HullSlot { Type = MountType.Engine, Size = MountSize.S };
            sparrowSlots[2] = new HullSlot { Type = MountType.Hangar, Size = MountSize.S };
            sparrowSlots[3] = new HullSlot { Type = MountType.Weapon, Size = MountSize.S };
            sparrowSlots[4] = new HullSlot { Type = MountType.Weapon, Size = MountSize.S };
            sparrowSlots[5] = new HullSlot { Type = MountType.Defense, Size = MountSize.S };
            sparrowSlots[6] = new HullSlot { Type = MountType.Utility, Size = MountSize.S };
            hullArray[0] = new HullSpec 
            { 
                Id = new FixedString64Bytes("lcv-sparrow"), 
                BaseMassTons = 300f, 
                FieldRefitAllowed = true,
                Category = HullCategory.Escort,
                HangarCapacity = 0f,
                PresentationArchetype = new FixedString64Bytes("escort"),
                DefaultStyleTokens = new StyleTokens { Palette = 0, Roughness = 128, Pattern = 0 }
            };

            var muleSlots = builder.Allocate(ref hullArray[1].Slots, 10);
            muleSlots[0] = new HullSlot { Type = MountType.Core, Size = MountSize.M };
            muleSlots[1] = new HullSlot { Type = MountType.Engine, Size = MountSize.M };
            muleSlots[2] = new HullSlot { Type = MountType.Hangar, Size = MountSize.M };
            muleSlots[3] = new HullSlot { Type = MountType.Hangar, Size = MountSize.M };
            muleSlots[4] = new HullSlot { Type = MountType.Weapon, Size = MountSize.M };
            muleSlots[5] = new HullSlot { Type = MountType.Weapon, Size = MountSize.M };
            muleSlots[6] = new HullSlot { Type = MountType.Defense, Size = MountSize.M };
            muleSlots[7] = new HullSlot { Type = MountType.Defense, Size = MountSize.M };
            muleSlots[8] = new HullSlot { Type = MountType.Utility, Size = MountSize.M };
            muleSlots[9] = new HullSlot { Type = MountType.Utility, Size = MountSize.M };
            hullArray[1] = new HullSpec 
            { 
                Id = new FixedString64Bytes("cv-mule"), 
                BaseMassTons = 700f, 
                FieldRefitAllowed = false,
                Category = HullCategory.Carrier,
                HangarCapacity = 8f, // 2x M hangar slots
                PresentationArchetype = new FixedString64Bytes("carrier"),
                DefaultStyleTokens = new StyleTokens { Palette = 0, Roughness = 128, Pattern = 0 }
            };

            var blobAsset = builder.CreateBlobAssetReference<HullCatalogBlob>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new HullCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultTuning(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var tuningBlob = ref builder.ConstructRoot<RefitRepairTuning>();
            tuningBlob = new RefitRepairTuning
            {
                BaseRefitSeconds = 60f,
                MassSecPerTon = 1.5f,
                SizeMultS = 1f,
                SizeMultM = 1.6f,
                SizeMultL = 2.4f,
                StationTimeMult = 1f,
                FieldTimeMult = 1.5f,
                GlobalFieldRefitEnabled = true,
                RepairRateEffPerSecStation = 0.01f,
                RepairRateEffPerSecField = 0.005f,
                RewirePenaltySeconds = 20f
            };

            var blobAsset = builder.CreateBlobAssetReference<RefitRepairTuning>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new RefitRepairTuningSingleton { Tuning = blobAsset });
        }

        private void CreateDefaultEngineModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<EngineModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 2);

            moduleArray[0] = new EngineModuleSpec
            {
                ModuleId = new FixedString64Bytes("engine-mk1"),
                EngineClass = EngineClass.Civilian,
                FuelType = EngineFuelType.Chemical,
                IntakeType = EngineIntakeType.None,
                VectoringMode = EngineVectoringMode.Vectored,
                TechLevel = 0f,
                Quality = 0f,
                ThrustScalar = 0f,
                TurnScalar = 0f,
                ResponseRating = 0f,
                EfficiencyRating = 0f,
                BoostRating = 0f,
                VectoringRating = 0f
            };

            moduleArray[1] = new EngineModuleSpec
            {
                ModuleId = new FixedString64Bytes("engine-mk2"),
                EngineClass = EngineClass.Military,
                FuelType = EngineFuelType.Fusion,
                IntakeType = EngineIntakeType.ReactorFeed,
                VectoringMode = EngineVectoringMode.Vectored,
                TechLevel = 0f,
                Quality = 0f,
                ThrustScalar = 0f,
                TurnScalar = 0f,
                ResponseRating = 0f,
                EfficiencyRating = 0f,
                BoostRating = 0f,
                VectoringRating = 0f
            };

            var blobAsset = builder.CreateBlobAssetReference<EngineModuleCatalogBlob>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new EngineModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultShieldModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<ShieldModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 2);

            moduleArray[0] = new ShieldModuleSpec
            {
                ModuleId = new FixedString64Bytes("shield-s-1"),
                Capacity = 220f,
                RechargePerSecond = 6f,
                RegenDelaySeconds = 3f,
                ArcDegrees = 360f,
                KineticResist = 1f,
                EnergyResist = 1f,
                ThermalResist = 1f,
                EMResist = 1f,
                RadiationResist = 1f,
                ExplosiveResist = 1f
            };

            moduleArray[1] = new ShieldModuleSpec
            {
                ModuleId = new FixedString64Bytes("shield-m-1"),
                Capacity = 520f,
                RechargePerSecond = 12f,
                RegenDelaySeconds = 3.5f,
                ArcDegrees = 360f,
                KineticResist = 1f,
                EnergyResist = 1f,
                ThermalResist = 1f,
                EMResist = 1f,
                RadiationResist = 1f,
                ExplosiveResist = 1f
            };

            var blobAsset = builder.CreateBlobAssetReference<ShieldModuleCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new ShieldModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultSensorModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<SensorModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 1);

            moduleArray[0] = new SensorModuleSpec
            {
                ModuleId = new FixedString64Bytes("scanner-s-1"),
                Range = 420f,
                RefreshSeconds = 0.25f,
                Resolution = 0.7f,
                JamResistance = 0.35f,
                PassiveSignature = 0.2f
            };

            var blobAsset = builder.CreateBlobAssetReference<SensorModuleCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new SensorModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultArmorModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<ArmorModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 1);

            moduleArray[0] = new ArmorModuleSpec
            {
                ModuleId = new FixedString64Bytes("armor-s-1"),
                HullBonus = 22f,
                DamageReduction = 0.25f,
                KineticResist = 1f,
                EnergyResist = 1f,
                ThermalResist = 1f,
                EMResist = 1f,
                RadiationResist = 1f,
                ExplosiveResist = 1f,
                RepairRateMultiplier = 1f
            };

            var blobAsset = builder.CreateBlobAssetReference<ArmorModuleCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new ArmorModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultWeaponModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<WeaponModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 4);

            moduleArray[0] = new WeaponModuleSpec
            {
                ModuleId = new FixedString64Bytes("laser-s-1"),
                WeaponId = new FixedString64Bytes("laser-s-1"),
                FireArcDegrees = 180f,
                FireArcOffsetDeg = 0f,
                AccuracyBonus = 0f,
                TrackingBonus = 0.05f
            };

            moduleArray[1] = new WeaponModuleSpec
            {
                ModuleId = new FixedString64Bytes("pd-s-1"),
                WeaponId = new FixedString64Bytes("pd-s-1"),
                FireArcDegrees = 240f,
                FireArcOffsetDeg = 0f,
                AccuracyBonus = 0.05f,
                TrackingBonus = 0.15f
            };

            moduleArray[2] = new WeaponModuleSpec
            {
                ModuleId = new FixedString64Bytes("missile-s-1"),
                WeaponId = new FixedString64Bytes("missile-s-1"),
                FireArcDegrees = 140f,
                FireArcOffsetDeg = 0f,
                AccuracyBonus = 0f,
                TrackingBonus = 0.05f
            };

            moduleArray[3] = new WeaponModuleSpec
            {
                ModuleId = new FixedString64Bytes("missile-m-1"),
                WeaponId = new FixedString64Bytes("missile-m-1"),
                FireArcDegrees = 140f,
                FireArcOffsetDeg = 0f,
                AccuracyBonus = 0f,
                TrackingBonus = 0.05f
            };

            var blobAsset = builder.CreateBlobAssetReference<WeaponModuleCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new WeaponModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultBridgeModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<BridgeModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 1);

            moduleArray[0] = new BridgeModuleSpec
            {
                ModuleId = new FixedString64Bytes("bridge-mk1"),
                TechLevel = 0f
            };

            var blobAsset = builder.CreateBlobAssetReference<BridgeModuleCatalogBlob>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new BridgeModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultCockpitModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<CockpitModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 1);

            moduleArray[0] = new CockpitModuleSpec
            {
                ModuleId = new FixedString64Bytes("cockpit-mk1"),
                NavigationCohesion = 0f
            };

            var blobAsset = builder.CreateBlobAssetReference<CockpitModuleCatalogBlob>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new CockpitModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultAmmoModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<AmmoModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 1);

            moduleArray[0] = new AmmoModuleSpec
            {
                ModuleId = new FixedString64Bytes("ammo-bay-s-1"),
                AmmoCapacity = 0f
            };

            var blobAsset = builder.CreateBlobAssetReference<AmmoModuleCatalogBlob>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new AmmoModuleCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultWeaponCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<WeaponCatalogBlob>();
            var weaponArray = builder.Allocate(ref catalogBlob.Weapons, 4);

            weaponArray[0] = new WeaponSpec
            {
                Id = new FixedString64Bytes("laser-s-1"),
                Class = WeaponClass.Laser,
                FireRate = 2f,
                BurstCount = 1,
                SpreadDeg = 0f,
                EnergyCost = 5f,
                HeatCost = 1f,
                LeadBias = 0.6f,
                DamageType = Space4XDamageType.Energy,
                ProjectileId = new FixedString32Bytes("laser-bolt-s-1")
            };

            weaponArray[1] = new WeaponSpec
            {
                Id = new FixedString64Bytes("pd-s-1"),
                Class = WeaponClass.Kinetic,
                FireRate = 5f,
                BurstCount = 1,
                SpreadDeg = 1.5f,
                EnergyCost = 2f,
                HeatCost = 0.5f,
                LeadBias = 0.7f,
                DamageType = Space4XDamageType.Kinetic,
                ProjectileId = new FixedString32Bytes("pd-round-s-1")
            };

            weaponArray[2] = new WeaponSpec
            {
                Id = new FixedString64Bytes("missile-s-1"),
                Class = WeaponClass.Missile,
                FireRate = 0.8f,
                BurstCount = 1,
                SpreadDeg = 0f,
                EnergyCost = 3f,
                HeatCost = 1f,
                LeadBias = 0.35f,
                DamageType = Space4XDamageType.Explosive,
                ProjectileId = new FixedString32Bytes("missile-s-1")
            };

            weaponArray[3] = new WeaponSpec
            {
                Id = new FixedString64Bytes("missile-m-1"),
                Class = WeaponClass.Missile,
                FireRate = 0.6f,
                BurstCount = 1,
                SpreadDeg = 0f,
                EnergyCost = 4f,
                HeatCost = 1.2f,
                LeadBias = 0.3f,
                DamageType = Space4XDamageType.Explosive,
                ProjectileId = new FixedString32Bytes("missile-m-1")
            };

            var blobAsset = builder.CreateBlobAssetReference<WeaponCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new WeaponCatalogSingleton { Catalog = blobAsset });
        }

        private void CreateDefaultProjectileCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<ProjectileCatalogBlob>();
            var projectileArray = builder.Allocate(ref catalogBlob.Projectiles, 4);

            projectileArray[0] = new ProjectileSpec
            {
                Id = new FixedString64Bytes("laser-bolt-s-1"),
                Kind = ProjectileKind.BeamTick,
                Speed = 0f,
                Lifetime = 0.2f,
                Gravity = 0f,
                TurnRateDeg = 0f,
                SeekRadius = 0f,
                Pierce = 0f,
                ChainRange = 0f,
                AoERadius = 0f,
                Damage = new DamageModel { Energy = 12f, Kinetic = 0f, Explosive = 0f },
                DamageType = Space4XDamageType.Energy
            };

            projectileArray[1] = new ProjectileSpec
            {
                Id = new FixedString64Bytes("pd-round-s-1"),
                Kind = ProjectileKind.Ballistic,
                Speed = 350f,
                Lifetime = 2.5f,
                Gravity = 0f,
                TurnRateDeg = 0f,
                SeekRadius = 0f,
                Pierce = 0f,
                ChainRange = 0f,
                AoERadius = 0f,
                Damage = new DamageModel { Energy = 0f, Kinetic = 8f, Explosive = 0f },
                DamageType = Space4XDamageType.Kinetic
            };

            projectileArray[2] = new ProjectileSpec
            {
                Id = new FixedString64Bytes("missile-s-1"),
                Kind = ProjectileKind.Missile,
                Speed = 160f,
                Lifetime = 6f,
                Gravity = 0f,
                TurnRateDeg = 45f,
                SeekRadius = 300f,
                Pierce = 0f,
                ChainRange = 0f,
                AoERadius = 8f,
                Damage = new DamageModel { Energy = 0f, Kinetic = 0f, Explosive = 30f },
                DamageType = Space4XDamageType.Explosive
            };

            projectileArray[3] = new ProjectileSpec
            {
                Id = new FixedString64Bytes("missile-m-1"),
                Kind = ProjectileKind.Missile,
                Speed = 140f,
                Lifetime = 7.5f,
                Gravity = 0f,
                TurnRateDeg = 35f,
                SeekRadius = 320f,
                Pierce = 0f,
                ChainRange = 0f,
                AoERadius = 12f,
                Damage = new DamageModel { Energy = 0f, Kinetic = 0f, Explosive = 45f },
                DamageType = Space4XDamageType.Explosive
            };

            var blobAsset = builder.CreateBlobAssetReference<ProjectileCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new ProjectileCatalogSingleton { Catalog = blobAsset });
        }
    }
}
