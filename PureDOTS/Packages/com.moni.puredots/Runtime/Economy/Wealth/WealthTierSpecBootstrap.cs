using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// Bootstraps the WealthTierSpec catalog singleton with default tiers.
    /// Creates a default catalog if none exists.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct WealthTierSpecBootstrapSystem : ISystem
    {
        private static BlobAssetReference<WealthTierSpecCatalogBlob> s_WealthCatalogBlob;

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
            using (var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WealthTierSpecCatalog>()))
            {
                if (query.TryGetSingleton(out WealthTierSpecCatalog existing))
                {
                    existingEntity = query.GetSingletonEntity();
                    hasExistingEntity = true;
                    if (!s_WealthCatalogBlob.IsCreated && existing.Catalog.IsCreated)
                    {
                        s_WealthCatalogBlob = existing.Catalog;
                    }
                    if (s_WealthCatalogBlob.IsCreated)
                    {
                        state.EntityManager.SetComponentData(existingEntity, new WealthTierSpecCatalog { Catalog = s_WealthCatalogBlob });
                        return;
                    }
                }
            }

            if (s_WealthCatalogBlob.IsCreated)
            {
                AssignCatalogToEntity(existingEntity, hasExistingEntity, ref state);
                return;
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WealthTierSpecCatalogBlob>();

            // Create default wealth tiers
            var tiers = new NativeList<WealthTierSpecBlob>(5, Allocator.Temp);
            
            tiers.Add(new WealthTierSpecBlob
            {
                TierName = new FixedString64Bytes("UltraPoor"),
                MinWealth = float.NegativeInfinity,
                MaxWealth = 0f,
                Title = new FixedString64Bytes("Pauper"),
                BaseRespect = 0.1f,
                BaseFear = 0.0f,
                BaseEnvy = 0.0f,
                CourtEligible = false
            });

            tiers.Add(new WealthTierSpecBlob
            {
                TierName = new FixedString64Bytes("Poor"),
                MinWealth = 0f,
                MaxWealth = 100f,
                Title = new FixedString64Bytes("Peasant"),
                BaseRespect = 0.3f,
                BaseFear = 0.0f,
                BaseEnvy = 0.0f,
                CourtEligible = false
            });

            tiers.Add(new WealthTierSpecBlob
            {
                TierName = new FixedString64Bytes("Mid"),
                MinWealth = 100f,
                MaxWealth = 500f,
                Title = new FixedString64Bytes("Comfortable"),
                BaseRespect = 0.5f,
                BaseFear = 0.1f,
                BaseEnvy = 0.2f,
                CourtEligible = false
            });

            tiers.Add(new WealthTierSpecBlob
            {
                TierName = new FixedString64Bytes("High"),
                MinWealth = 500f,
                MaxWealth = 2000f,
                Title = new FixedString64Bytes("Wealthy"),
                BaseRespect = 0.7f,
                BaseFear = 0.2f,
                BaseEnvy = 0.5f,
                CourtEligible = true
            });

            tiers.Add(new WealthTierSpecBlob
            {
                TierName = new FixedString64Bytes("UltraHigh"),
                MinWealth = 2000f,
                MaxWealth = float.PositiveInfinity,
                Title = new FixedString64Bytes("Oligarch"),
                BaseRespect = 0.9f,
                BaseFear = 0.4f,
                BaseEnvy = 0.8f,
                CourtEligible = true
            });

            var tiersArray = builder.Allocate(ref root.Tiers, tiers.Length);
            for (int i = 0; i < tiers.Length; i++)
            {
                tiersArray[i] = tiers[i];
            }

            tiers.Dispose();

            s_WealthCatalogBlob = builder.CreateBlobAssetReference<WealthTierSpecCatalogBlob>(Allocator.Persistent);
            var catalogComponent = new WealthTierSpecCatalog { Catalog = s_WealthCatalogBlob };

            if (hasExistingEntity && state.EntityManager.Exists(existingEntity))
            {
                state.EntityManager.SetComponentData(existingEntity, catalogComponent);
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(typeof(WealthTierSpecCatalog));
                state.EntityManager.SetComponentData(entity, catalogComponent);
            }
        }

        private static void DisposeCatalog(ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WealthTierSpecCatalog>());
            if (query.TryGetSingleton(out WealthTierSpecCatalog catalog))
            {
                var entity = query.GetSingletonEntity();
                catalog.Catalog = default;
                if (state.EntityManager.Exists(entity))
                {
                    state.EntityManager.SetComponentData(entity, catalog);
                }
            }

            if (s_WealthCatalogBlob.IsCreated)
            {
                s_WealthCatalogBlob.Dispose();
                s_WealthCatalogBlob = default;
            }
        }

        private static void AssignCatalogToEntity(Entity existingEntity, bool hasExistingEntity, ref SystemState state)
        {
            if (!s_WealthCatalogBlob.IsCreated)
            {
                return;
            }

            var catalogComponent = new WealthTierSpecCatalog { Catalog = s_WealthCatalogBlob };
            if (hasExistingEntity && state.EntityManager.Exists(existingEntity))
            {
                state.EntityManager.SetComponentData(existingEntity, catalogComponent);
            }
            else
            {
                var entity = state.EntityManager.CreateEntity(typeof(WealthTierSpecCatalog));
                state.EntityManager.SetComponentData(entity, catalogComponent);
            }
        }
    }
}
