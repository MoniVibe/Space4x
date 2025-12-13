#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Authoring.Motivation;
using PureDOTS.Runtime.Motivation;
using PureDOTS.Systems.Motivation;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Motivation
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Bootstrap system for Space4X motivation system.
    /// Creates Space4X-specific MotivationCatalog with crew/empire ambitions.
    /// </summary>
    public static class Space4XMotivationBootstrap
    {
        /// <summary>
        /// Initializes the motivation system with Space4X-specific catalogs.
        /// Call this from your game bootstrap code.
        /// </summary>
        public static void Initialize(EntityManager entityManager)
        {
            // Create example motivation specs for Space4X
            var specs = new[]
            {
                // Dream: Win next battle
                new MotivationSpec
                {
                    SpecId = 3001,
                    Layer = MotivationLayer.Dream,
                    Scope = MotivationScope.Individual,
                    Tag = MotivationTag.WinCombat,
                    BaseImportance = 150,
                    BaseInitiativeCost = 60,
                    MaxConcurrentHolders = 0,
                    RequiredLoyalty = 0,
                    MinCorruptPure = -100,
                    MinLawChaos = -100,
                    MinGoodEvil = -100
                },
                // Aspiration: Become admiral
                new MotivationSpec
                {
                    SpecId = 3002,
                    Layer = MotivationLayer.Aspiration,
                    Scope = MotivationScope.Individual,
                    Tag = MotivationTag.BecomeLegendary,
                    BaseImportance = 200,
                    BaseInitiativeCost = 90,
                    MaxConcurrentHolders = 0,
                    RequiredLoyalty = 50,
                    MinCorruptPure = -100,
                    MinLawChaos = -100,
                    MinGoodEvil = -100
                },
                // Ambition: Colonize system
                new MotivationSpec
                {
                    SpecId = 4001,
                    Layer = MotivationLayer.Ambition,
                    Scope = MotivationScope.Aggregate,
                    Tag = MotivationTag.ExploreOrColonize,
                    BaseImportance = 255,
                    BaseInitiativeCost = 120,
                    MaxConcurrentHolders = 0,
                    RequiredLoyalty = 100,
                    MinCorruptPure = -100,
                    MinLawChaos = -100,
                    MinGoodEvil = -100
                },
                // Ambition: Form covenant
                new MotivationSpec
                {
                    SpecId = 4002,
                    Layer = MotivationLayer.Ambition,
                    Scope = MotivationScope.Aggregate,
                    Tag = MotivationTag.SpreadIdeology,
                    BaseImportance = 255,
                    BaseInitiativeCost = 150,
                    MaxConcurrentHolders = 0,
                    RequiredLoyalty = 150,
                    MinCorruptPure = 20, // Leaning toward pure
                    MinLawChaos = 10, // Slightly lawful
                    MinGoodEvil = 10 // Slightly good
                },
                // Wish: Get assigned to cutting-edge carrier
                new MotivationSpec
                {
                    SpecId = 3003,
                    Layer = MotivationLayer.Wish,
                    Scope = MotivationScope.Individual,
                    Tag = MotivationTag.GainFame,
                    BaseImportance = 120,
                    BaseInitiativeCost = 40,
                    MaxConcurrentHolders = 0,
                    RequiredLoyalty = 0,
                    MinCorruptPure = -100,
                    MinLawChaos = -100,
                    MinGoodEvil = -100
                }
            };

            // Build catalog blob
            var catalogBlob = MotivationCatalogAsset.BuildBlobAssetFromSpecs(specs);

            // Initialize motivation system with our Space4X-specific catalog
            PureDOTS.Systems.Motivation.MotivationBootstrapHelper.InitializeMotivationSystem(
                entityManager,
                catalogBlob);

            Debug.Log("[Space4XMotivationBootstrap] Motivation system initialized with Space4X-specific catalog.");
        }
    }
}
#endif

