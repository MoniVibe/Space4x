using PureDOTS.Runtime.Components;
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
        }

        private void CreateDefaultModuleCatalog(ref SystemState state)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<ModuleCatalogBlob>();
            var moduleArray = builder.Allocate(ref catalogBlob.Modules, 14);

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
    }
}

