using System;
using SystemEnv = System.Environment;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Village;
#if SPACE4X_AVAILABLE
using Space4X.Mining;
#endif
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.Runtime.MonoBehaviours
{
    /// <summary>
    /// Legacy scenario HUD that displays entity counts and resource values.
    /// Reads ECS data directly from World.DefaultGameObjectInjectionWorld.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Runtime.MonoBehaviours", null, "DemoDebugHUD")]
    public class ScenarioDebugHUD : MonoBehaviour
    {
        private const string LegacyScenarioEnvVar = "PURE_DOTS_LEGACY_SCENARIO";

        [Header("Display Settings")]
        public bool showOverlay = true;
        public bool showGizmos = true;
        public Vector2 padding = new Vector2(10f, 10f);
        public float gizmoSphereRadius = 2f;

        private World _world;
        private EntityManager _entityManager;
        private bool _worldInitialized;

        private void Awake()
        {
            if (!IsLegacyProfileEnabled())
            {
                enabled = false;
                return;
            }

            TryInitializeWorld();
        }

        private void TryInitializeWorld()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                _worldInitialized = false;
                return;
            }

            _entityManager = _world.EntityManager;
            _worldInitialized = true;
        }

        private void Update()
        {
            if (!_worldInitialized)
            {
                TryInitializeWorld();
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || !_worldInitialized)
            {
                return;
            }

            var rect = new Rect(padding.x, padding.y, 300f, 500f);
            GUILayout.BeginArea(rect);
            GUILayout.Box("Simulation Debug HUD");

            // Check scenario state
            using var scenarioCheckQuery = _entityManager.CreateEntityQuery(typeof(ScenarioState));
            if (scenarioCheckQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("No ScenarioState found");
                GUILayout.EndArea();
                return;
            }

            var scenario = scenarioCheckQuery.GetSingleton<ScenarioState>();

            // Godgame stats
            if (scenario.EnableGodgame)
            {
                GUILayout.Label("=== GODGAME ===", GUI.skin.box);

                // Village count
                using var villageQuery = _entityManager.CreateEntityQuery(typeof(VillageTag));
                int villageCount = villageQuery.CalculateEntityCount();
                GUILayout.Label($"Villages: {villageCount}");

                // Villagers per village and resources
                using var villagerQuery = _entityManager.CreateEntityQuery(typeof(VillagerId));
                int totalVillagers = villagerQuery.CalculateEntityCount();
                GUILayout.Label($"Total Villagers: {totalVillagers}");

                if (villageCount > 0)
                {
                    float avgVillagersPerVillage = (float)totalVillagers / villageCount;
                    GUILayout.Label($"Avg Villagers/Village: {avgVillagersPerVillage:F1}");
                }

                // Village resources (sum across all villages)
                float totalFood = 0f;
                float totalWood = 0f;
                float totalStone = 0f;
                float totalOre = 0f;

                using var villageResourcesQuery = _entityManager.CreateEntityQuery(typeof(VillageTag), typeof(VillageResources));
                if (!villageResourcesQuery.IsEmptyIgnoreFilter)
                {
                    var resourcesArray = villageResourcesQuery.ToComponentDataArray<VillageResources>(Allocator.Temp);
                    for (int i = 0; i < resourcesArray.Length; i++)
                    {
                        totalFood += resourcesArray[i].Food;
                        totalWood += resourcesArray[i].Wood;
                        totalStone += resourcesArray[i].Stone;
                        totalOre += resourcesArray[i].Ore;
                    }
                    resourcesArray.Dispose();
                }

                GUILayout.Label($"Total Food: {totalFood:F1}");
                GUILayout.Label($"Total Wood: {totalWood:F1}");
                GUILayout.Label($"Total Stone: {totalStone:F1}");
                GUILayout.Label($"Total Ore: {totalOre:F1}");

                // Resource nodes
                using var treeQuery = _entityManager.CreateEntityQuery(typeof(TreeTag));
                using var stoneQuery = _entityManager.CreateEntityQuery(typeof(StoneNodeTag));
                using var oreQuery = _entityManager.CreateEntityQuery(typeof(OreNodeTag));
                int treeCount = treeQuery.CalculateEntityCount();
                int stoneCount = stoneQuery.CalculateEntityCount();
                int oreCount = oreQuery.CalculateEntityCount();
                GUILayout.Label($"Resource Nodes - Trees: {treeCount}, Stone: {stoneCount}, Ore: {oreCount}");
            }

            // Space4X stats
            if (scenario.EnableSpace4x)
            {
                GUILayout.Label("=== SPACE4X ===", GUI.skin.box);

                // Carrier count
                using var carrierQuery = _entityManager.CreateEntityQuery(typeof(PlatformTag), typeof(PlatformKind), typeof(PlatformResources));
                int carrierCount = 0;
                float totalOre = 0f;
                float totalRefinedOre = 0f;
                float totalFuel = 0f;

                if (!carrierQuery.IsEmptyIgnoreFilter)
                {
                    var entities = carrierQuery.ToEntityArray(Allocator.Temp);
                    var kinds = carrierQuery.ToComponentDataArray<PlatformKind>(Allocator.Temp);
                    var resources = carrierQuery.ToComponentDataArray<PlatformResources>(Allocator.Temp);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        if ((kinds[i].Flags & PlatformFlags.IsCarrier) != 0)
                        {
                            carrierCount++;
                            totalOre += resources[i].Ore;
                            totalRefinedOre += resources[i].RefinedOre;
                            totalFuel += resources[i].Fuel;
                        }
                    }

                    entities.Dispose();
                    kinds.Dispose();
                    resources.Dispose();
                }

                GUILayout.Label($"Carriers: {carrierCount}");
                GUILayout.Label($"Total Ore: {totalOre:F1}");
                GUILayout.Label($"Total Refined Ore: {totalRefinedOre:F1}");
                GUILayout.Label($"Total Fuel: {totalFuel:F1}");

#if SPACE4X_AVAILABLE
                // Miner count
                using var minerQuery = _entityManager.CreateEntityQuery(typeof(MiningVesselTag));
                int minerCount = minerQuery.CalculateEntityCount();
                GUILayout.Label($"Miners: {minerCount}");
#endif

                // Asteroid count
                using var asteroidQuery = _entityManager.CreateEntityQuery(typeof(ResourceNodeTag), typeof(ResourceDeposit));
                int asteroidCount = asteroidQuery.CalculateEntityCount();
                GUILayout.Label($"Asteroids: {asteroidCount}");
            }

            // Scenario state (already have scenario from above)
            GUILayout.Label("=== SCENARIO ===", GUI.skin.box);
            GUILayout.Label($"Initialized: {scenario.IsInitialized}");
            GUILayout.Label($"EnableGodgame: {scenario.EnableGodgame}");
            GUILayout.Label($"EnableSpace4x: {scenario.EnableSpace4x}");

            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || !_worldInitialized)
            {
                return;
            }

            // Check scenario state
            using var scenarioQuery = _entityManager.CreateEntityQuery(typeof(ScenarioState));
            if (scenarioQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var scenarioState = scenarioQuery.GetSingleton<ScenarioState>();

            // Draw villages
            if (scenarioState.EnableGodgame)
            {
                Gizmos.color = Color.green;
                using var villageTransformQuery = _entityManager.CreateEntityQuery(typeof(VillageTag), typeof(LocalTransform));
                if (!villageTransformQuery.IsEmptyIgnoreFilter)
                {
                    var transforms = villageTransformQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        Gizmos.DrawSphere(transforms[i].Position, gizmoSphereRadius);
                    }
                    transforms.Dispose();
                }

                // Draw lines from villagers to their targets (if they have VillagerAIState with target)
                Gizmos.color = Color.yellow;
                using var villagerAIQuery = _entityManager.CreateEntityQuery(typeof(LocalTransform), typeof(VillagerAIState));
                if (!villagerAIQuery.IsEmptyIgnoreFilter)
                {
                    var entities = villagerAIQuery.ToEntityArray(Allocator.Temp);
                    var transforms = villagerAIQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    var aiStates = villagerAIQuery.ToComponentDataArray<VillagerAIState>(Allocator.Temp);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        var aiState = aiStates[i];
                        var pos = transforms[i].Position;

                        if (aiState.TargetEntity != Entity.Null && _entityManager.HasComponent<LocalTransform>(aiState.TargetEntity))
                        {
                            var targetTransform = _entityManager.GetComponentData<LocalTransform>(aiState.TargetEntity);
                            var targetPos = targetTransform.Position;
                            Gizmos.DrawLine(pos, targetPos);
                        }
                        else if (math.lengthsq(aiState.TargetPosition) > 0.01f)
                        {
                            Gizmos.DrawLine(pos, aiState.TargetPosition);
                        }
                    }

                    entities.Dispose();
                    transforms.Dispose();
                    aiStates.Dispose();
                }
            }

            // Draw carriers
            if (scenarioState.EnableSpace4x)
            {
                Gizmos.color = Color.cyan;
                using var platformQuery = _entityManager.CreateEntityQuery(typeof(PlatformTag), typeof(LocalTransform), typeof(PlatformKind));
                if (!platformQuery.IsEmptyIgnoreFilter)
                {
                    var entities = platformQuery.ToEntityArray(Allocator.Temp);
                    var transforms = platformQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    var kinds = platformQuery.ToComponentDataArray<PlatformKind>(Allocator.Temp);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        if ((kinds[i].Flags & PlatformFlags.IsCarrier) != 0)
                        {
                            Gizmos.DrawSphere(transforms[i].Position, gizmoSphereRadius * 2f);
                        }
                    }

                    entities.Dispose();
                    transforms.Dispose();
                    kinds.Dispose();
                }

#if SPACE4X_AVAILABLE
                // Draw lines from miners to their targets
                Gizmos.color = Color.magenta;
                using var minerQuery = _entityManager.CreateEntityQuery(typeof(LocalTransform), typeof(MiningJob));
                if (!minerQuery.IsEmptyIgnoreFilter)
                {
                    var transforms = minerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    var miningJobs = minerQuery.ToComponentDataArray<MiningJob>(Allocator.Temp);
                    var transformLookup = new ComponentLookup<LocalTransform>(_entityManager, true);
                    transformLookup.Update(_entityManager);

                    for (int i = 0; i < transforms.Length; i++)
                    {
                        var job = miningJobs[i];
                        var pos = transforms[i].Position;

                        if (job.Phase == MiningPhase.FlyToAsteroid && job.TargetAsteroid != Entity.Null)
                        {
                            if (transformLookup.HasComponent(job.TargetAsteroid))
                            {
                                var targetPos = transformLookup[job.TargetAsteroid].Position;
                                Gizmos.DrawLine(pos, targetPos);
                            }
                        }
                        else if (job.Phase == MiningPhase.ReturnToCarrier && job.CarrierEntity != Entity.Null)
                        {
                            if (transformLookup.HasComponent(job.CarrierEntity))
                            {
                                var targetPos = transformLookup[job.CarrierEntity].Position;
                                Gizmos.DrawLine(pos, targetPos);
                            }
                        }
                    }

                    transforms.Dispose();
                    miningJobs.Dispose();
                }
#endif
            }
        }

        private static bool IsLegacyProfileEnabled()
        {
            var value = SystemEnv.GetEnvironmentVariable(LegacyScenarioEnvVar);
            return IsEnabledValue(value);
        }

        private static bool IsEnabledValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
