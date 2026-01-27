using PureDOTS.Runtime.Motivation;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Motivation
{
    /// <summary>
    /// Bootstrap helper for initializing motivation system.
    /// Game-specific bootstrap code should call InitializeMotivationSystem with merged specs.
    /// </summary>
    public static class MotivationBootstrapHelper
    {
        /// <summary>
        /// Initializes motivation system by creating singletons with the provided catalog blob.
        /// Called from game-specific bootstrap code after merging catalogs.
        /// </summary>
        public static void InitializeMotivationSystem(
            EntityManager entityManager,
            BlobAssetReference<MotivationCatalog> catalogBlob)
        {
            // Check if already initialized
            if (HasSingleton<MotivationConfigState>(entityManager))
            {
                return; // Already initialized
            }

            // Create MotivationConfigState singleton
            var configEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(configEntity, new MotivationConfigState
            {
                Catalog = catalogBlob,
                TicksBetweenRefresh = 100u,
                DefaultDreamSlots = 3,
                DefaultAspirationSlots = 2,
                DefaultWishSlots = 2
            });

            // Create MotivationScoringConfig singleton (optional, uses defaults if missing)
            var scoringEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(scoringEntity, MotivationScoringConfig.Default);
        }

        /// <summary>
        /// Creates an empty catalog blob. Useful for fallback or testing.
        /// </summary>
        public static BlobAssetReference<MotivationCatalog> CreateEmptyCatalogBlob()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MotivationCatalog>();
            builder.Allocate(ref root.Specs, 0);
            var blob = builder.CreateBlobAssetReference<MotivationCatalog>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static bool HasSingleton<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }
    }
}

