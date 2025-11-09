using Unity.Mathematics;
using UnityEngine;
using Space4X.Authoring;
using Space4X.Registry;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Spatial;

namespace Space4X.Editor
{
    /// <summary>
    /// Editor tool to create a scene with carriers and asteroids.
    /// Run via Menu: Tools/Space4X/Create Carrier & Asteroid Scene
    /// </summary>
    public static class CreateCarrierAsteroidScene
    {
        [UnityEditor.MenuItem("Tools/Space4X/Create Carrier & Asteroid Scene")]
        public static void CreateScene()
        {
            // Create root GameObject for the setup
            GameObject root = new GameObject("MiningDemoSetup");
            
            // Add required components for PureDOTS
            var configAuthoring = root.AddComponent<PureDotsConfigAuthoring>();
            var spatialAuthoring = root.AddComponent<SpatialPartitionAuthoring>();
            var miningDemo = root.AddComponent<Space4XMiningDemoAuthoring>();
            
            // Configure PureDOTS config reference
            var configAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/Space4X/Config/PureDotsRuntimeConfig.asset");
            if (configAsset != null)
            {
                var configField = typeof(PureDotsConfigAuthoring).GetField("config",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                configField?.SetValue(configAuthoring, configAsset);
            }
            
            // Configure Spatial Partition
            var spatialProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset");
            if (spatialProfile != null)
            {
                var profileField = typeof(SpatialPartitionAuthoring).GetField("profile",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                profileField?.SetValue(spatialAuthoring, spatialProfile);
            }
            
            // Configure Carriers
            var carriers = new Space4XMiningDemoAuthoring.CarrierDefinition[]
            {
                new Space4XMiningDemoAuthoring.CarrierDefinition
                {
                    CarrierId = "CARRIER-1",
                    Speed = 5f,
                    PatrolCenter = new float3(0f, 0f, 0f),
                    PatrolRadius = 50f,
                    WaitTime = 2f,
                    Position = new float3(0f, 0f, 0f)
                },
                new Space4XMiningDemoAuthoring.CarrierDefinition
                {
                    CarrierId = "CARRIER-2",
                    Speed = 5f,
                    PatrolCenter = new float3(30f, 0f, 30f),
                    PatrolRadius = 40f,
                    WaitTime = 2f,
                    Position = new float3(30f, 0f, 30f)
                }
            };
            
            // Configure Asteroids
            var asteroids = new Space4XMiningDemoAuthoring.AsteroidDefinition[]
            {
                // Minerals asteroids
                new Space4XMiningDemoAuthoring.AsteroidDefinition
                {
                    AsteroidId = "ASTEROID-MINERALS-1",
                    ResourceType = ResourceType.Minerals,
                    ResourceAmount = 500f,
                    MaxResourceAmount = 500f,
                    MiningRate = 10f,
                    Position = new float3(20f, 0f, 0f)
                },
                new Space4XMiningDemoAuthoring.AsteroidDefinition
                {
                    AsteroidId = "ASTEROID-MINERALS-2",
                    ResourceType = ResourceType.Minerals,
                    ResourceAmount = 500f,
                    MaxResourceAmount = 500f,
                    MiningRate = 10f,
                    Position = new float3(-20f, 0f, 0f)
                },
                // Rare Metals asteroids
                new Space4XMiningDemoAuthoring.AsteroidDefinition
                {
                    AsteroidId = "ASTEROID-RARE-1",
                    ResourceType = ResourceType.RareMetals,
                    ResourceAmount = 300f,
                    MaxResourceAmount = 300f,
                    MiningRate = 5f,
                    Position = new float3(0f, 0f, 25f)
                },
                // Energy Crystals asteroids
                new Space4XMiningDemoAuthoring.AsteroidDefinition
                {
                    AsteroidId = "ASTEROID-ENERGY-1",
                    ResourceType = ResourceType.EnergyCrystals,
                    ResourceAmount = 200f,
                    MaxResourceAmount = 200f,
                    MiningRate = 3f,
                    Position = new float3(25f, 0f, -15f)
                },
                // Organic Matter asteroids
                new Space4XMiningDemoAuthoring.AsteroidDefinition
                {
                    AsteroidId = "ASTEROID-ORGANIC-1",
                    ResourceType = ResourceType.OrganicMatter,
                    ResourceAmount = 400f,
                    MaxResourceAmount = 400f,
                    MiningRate = 8f,
                    Position = new float3(-25f, 0f, -15f)
                }
            };
            
            // Use reflection to set private fields
            var carriersField = typeof(Space4XMiningDemoAuthoring).GetField("carriers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            carriersField?.SetValue(miningDemo, carriers);
            
            var asteroidsField = typeof(Space4XMiningDemoAuthoring).GetField("asteroids",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            asteroidsField?.SetValue(miningDemo, asteroids);
            
            // Set visual settings to defaults
            var visualsField = typeof(Space4XMiningDemoAuthoring).GetField("visuals",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            visualsField?.SetValue(miningDemo, Space4XMiningDemoAuthoring.MiningVisualSettings.CreateDefault());
            
            // Create MiningVisualManifest for visual representation
            GameObject visualManifest = GameObject.Find("MiningVisualManifest");
            if (visualManifest == null)
            {
                visualManifest = new GameObject("MiningVisualManifest");
                visualManifest.AddComponent<PureDOTS.Authoring.MiningVisualManifestAuthoring>();
            }
            
            Debug.Log("✓ Created MiningDemoSetup with 2 carriers and 5 asteroids");
            Debug.Log("  Carriers: CARRIER-1 (center), CARRIER-2 (offset)");
            Debug.Log("  Asteroids: 2 Minerals, 1 RareMetals, 1 EnergyCrystals, 1 OrganicMatter");
            
            UnityEditor.Selection.activeGameObject = root;
        }
    }
}

