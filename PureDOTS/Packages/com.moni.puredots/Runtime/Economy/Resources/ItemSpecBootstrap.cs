using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Resources
{
    /// <summary>
    /// Bootstraps the ItemSpec catalog singleton with default items.
    /// Creates a default catalog if none exists.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ItemSpecBootstrapSystem : ISystem
    {
        private static BlobAssetReference<ItemSpecCatalogBlob> s_ItemCatalogBlob;

        public void OnCreate(ref SystemState state)
        {
            EnsureCatalog(ref state);
            state.Enabled = false; // Only run once
        }

        public void OnUpdate(ref SystemState state)
        {
            // No-op after initial bootstrap
        }

        public void OnDestroy(ref SystemState state)
        {
            DisposeCatalog(ref state);
        }

        private static void EnsureCatalog(ref SystemState state)
        {
            Entity existingEntity = Entity.Null;
            var hasExistingEntity = false;
            using (var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemSpecCatalog>()))
            {
                if (query.TryGetSingleton(out ItemSpecCatalog existing))
                {
                    existingEntity = query.GetSingletonEntity();
                    hasExistingEntity = true;
                    if (!s_ItemCatalogBlob.IsCreated && existing.Catalog.IsCreated)
                    {
                        s_ItemCatalogBlob = existing.Catalog;
                    }
                    if (s_ItemCatalogBlob.IsCreated)
                    {
                        state.EntityManager.SetComponentData(existingEntity, new ItemSpecCatalog { Catalog = s_ItemCatalogBlob });
                        return;
                    }
                }
            }

            if (s_ItemCatalogBlob.IsCreated)
            {
                AssignCatalogToEntity(existingEntity, hasExistingEntity, ref state);
                return;
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ItemSpecCatalogBlob>();

            // Create default items
            var items = new NativeList<ItemSpecBlob>(32, Allocator.Temp);
            
            // Food items
            var grain = new FixedString64Bytes("grain");
            var grainName = new FixedString64Bytes("Grain");
            items.Add(new ItemSpecBlob
            {
                ItemId = grain,
                Name = grainName,
                Category = ItemCategory.Food,
                MassPerUnit = 0.5f,
                VolumePerUnit = 0.001f,
                StackSize = 1000,
                Tags = ItemTags.Food | ItemTags.BulkOnly | ItemTags.Perishable,
                BaseValue = 0.2f,
                IsPerishable = true,
                PerishRate = 0.01f,
                IsDurable = false,
                BaseDurability = 0f
            });

            var bread = new FixedString64Bytes("bread");
            var breadName = new FixedString64Bytes("Bread");
            items.Add(new ItemSpecBlob
            {
                ItemId = bread,
                Name = breadName,
                Category = ItemCategory.Food,
                MassPerUnit = 0.3f,
                VolumePerUnit = 0.0005f,
                StackSize = 20,
                Tags = ItemTags.Food | ItemTags.Perishable,
                BaseValue = 0.5f,
                IsPerishable = true,
                PerishRate = 0.05f,
                IsDurable = false,
                BaseDurability = 0f
            });

            // Raw materials
            var ironOre = new FixedString64Bytes("iron_ore");
            var ironOreName = new FixedString64Bytes("Iron Ore");
            items.Add(new ItemSpecBlob
            {
                ItemId = ironOre,
                Name = ironOreName,
                Category = ItemCategory.Raw,
                MassPerUnit = 5.0f,
                VolumePerUnit = 0.002f,
                StackSize = 100,
                Tags = ItemTags.BulkOnly,
                BaseValue = 1.0f,
                IsPerishable = false,
                PerishRate = 0f,
                IsDurable = true,
                BaseDurability = 1.0f
            });

            var wood = new FixedString64Bytes("wood");
            var woodName = new FixedString64Bytes("Wood");
            items.Add(new ItemSpecBlob
            {
                ItemId = wood,
                Name = woodName,
                Category = ItemCategory.Raw,
                MassPerUnit = 0.8f,
                VolumePerUnit = 0.001f,
                StackSize = 500,
                Tags = ItemTags.BulkOnly | ItemTags.Flammable,
                BaseValue = 0.3f,
                IsPerishable = false,
                PerishRate = 0f,
                IsDurable = true,
                BaseDurability = 1.0f
            });

            // Processed materials
            var ironIngot = new FixedString64Bytes("iron_ingot");
            var ironIngotName = new FixedString64Bytes("Iron Ingot");
            items.Add(new ItemSpecBlob
            {
                ItemId = ironIngot,
                Name = ironIngotName,
                Category = ItemCategory.Processed,
                MassPerUnit = 5.0f,
                VolumePerUnit = 0.001f,
                StackSize = 100,
                Tags = ItemTags.BulkOnly,
                BaseValue = 5.0f,
                IsPerishable = false,
                PerishRate = 0f,
                IsDurable = true,
                BaseDurability = 1.0f
            });

            // Tools
            var hammer = new FixedString64Bytes("hammer");
            var hammerName = new FixedString64Bytes("Hammer");
            items.Add(new ItemSpecBlob
            {
                ItemId = hammer,
                Name = hammerName,
                Category = ItemCategory.Tool,
                MassPerUnit = 2.0f,
                VolumePerUnit = 0.001f,
                StackSize = 1,
                Tags = ItemTags.Durable,
                BaseValue = 10.0f,
                IsPerishable = false,
                PerishRate = 0f,
                IsDurable = true,
                BaseDurability = 1.0f
            });

            // Weapons
            var sword = new FixedString64Bytes("sword");
            var swordName = new FixedString64Bytes("Sword");
            items.Add(new ItemSpecBlob
            {
                ItemId = sword,
                Name = swordName,
                Category = ItemCategory.Weapon,
                MassPerUnit = 2.5f,
                VolumePerUnit = 0.002f,
                StackSize = 1,
                Tags = ItemTags.Durable | ItemTags.MilitaryGrade,
                BaseValue = 50.0f,
                IsPerishable = false,
                PerishRate = 0f,
                IsDurable = true,
                BaseDurability = 1.0f
            });

            var itemsArray = builder.Allocate(ref root.Items, items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                itemsArray[i] = items[i];
            }

            items.Dispose();

            s_ItemCatalogBlob = builder.CreateBlobAssetReference<ItemSpecCatalogBlob>(Allocator.Persistent);
            var catalogComponent = new ItemSpecCatalog { Catalog = s_ItemCatalogBlob };

            if (hasExistingEntity && state.EntityManager.Exists(existingEntity))
            {
                state.EntityManager.SetComponentData(existingEntity, catalogComponent);
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(typeof(ItemSpecCatalog));
                state.EntityManager.SetComponentData(entity, catalogComponent);
            }
        }

        private static void DisposeCatalog(ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemSpecCatalog>());
            if (query.TryGetSingleton(out ItemSpecCatalog catalog))
            {
                var entity = query.GetSingletonEntity();
                catalog.Catalog = default;
                if (state.EntityManager.Exists(entity))
                {
                    state.EntityManager.SetComponentData(entity, catalog);
                }
            }

            if (s_ItemCatalogBlob.IsCreated)
            {
                s_ItemCatalogBlob.Dispose();
                s_ItemCatalogBlob = default;
            }
        }

        private static void AssignCatalogToEntity(Entity existingEntity, bool hasExistingEntity, ref SystemState state)
        {
            if (!s_ItemCatalogBlob.IsCreated)
            {
                return;
            }

            var catalogComponent = new ItemSpecCatalog { Catalog = s_ItemCatalogBlob };
            if (hasExistingEntity && state.EntityManager.Exists(existingEntity))
            {
                state.EntityManager.SetComponentData(existingEntity, catalogComponent);
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(typeof(ItemSpecCatalog));
                state.EntityManager.SetComponentData(entity, catalogComponent);
            }
        }
    }
}
