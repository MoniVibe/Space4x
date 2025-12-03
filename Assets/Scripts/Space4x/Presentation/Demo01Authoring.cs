using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// Authoring component for Demo_01 scene setup.
    /// Creates carriers, crafts, and asteroids with presentation components.
    /// </summary>
    [DisallowMultipleComponent]
    public class Demo01Authoring : MonoBehaviour
    {
        [Header("Demo Configuration")]
        [Tooltip("Number of carriers to spawn")]
        [Range(1, 10)]
        public int CarrierCount = 4;

        [Tooltip("Number of crafts per carrier")]
        [Range(1, 10)]
        public int CraftsPerCarrier = 4;

        [Tooltip("Number of asteroids to spawn")]
        [Range(5, 50)]
        public int AsteroidCount = 20;

        [Header("Spawn Area")]
        [Tooltip("Spawn area size")]
        public float SpawnAreaSize = 100f;

        [Tooltip("Minimum distance between asteroids")]
        public float MinAsteroidDistance = 10f;

        [Header("Faction Colors")]
        public Color[] FactionColors = new Color[]
        {
            new Color(0.2f, 0.4f, 1f, 1f),   // Blue
            new Color(1f, 0.2f, 0.2f, 1f),   // Red
            new Color(0.2f, 1f, 0.2f, 1f),   // Green
            new Color(1f, 1f, 0.2f, 1f)      // Yellow
        };

        [Header("LOD Configuration")]
        public float FullDetailMaxDistance = 100f;
        public float ReducedDetailMaxDistance = 500f;
        public float ImpostorMaxDistance = 2000f;

        [Header("Performance Budget")]
        public int MaxFullDetailCrafts = 1000;
        public bool AutoAdjustLOD = true;
        public bool AutoAdjustDensity = true;

        [Header("Debug")]
        [Tooltip("Enable LOD debug visualization (colors entities by LOD level)")]
        public bool EnableLODDebug = false;
    }

    /// <summary>
    /// Baker for Demo01Authoring.
    /// </summary>
    public class Demo01Baker : Baker<Demo01Authoring>
    {
        public override void Bake(Demo01Authoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Add LOD config
            AddComponent(entity, new PresentationLODConfig
            {
                FullDetailMaxDistance = authoring.FullDetailMaxDistance,
                ReducedDetailMaxDistance = authoring.ReducedDetailMaxDistance,
                ImpostorMaxDistance = authoring.ImpostorMaxDistance
            });

            // Add render density config
            AddComponent(entity, new RenderDensityConfig
            {
                Density = 1f,
                AutoAdjust = authoring.AutoAdjustDensity
            });

            // Add performance budget config
            AddComponent(entity, new PerformanceBudgetConfig
            {
                MaxFullDetailCarriers = 100,
                MaxFullDetailCrafts = authoring.MaxFullDetailCrafts,
                MaxReducedDetailEntities = 10000,
                MaxFleetImpostors = 1000,
                MaxDrawCalls = 500,
                FrameTimeBudgetMs = 16f,
                AutoAdjustLOD = authoring.AutoAdjustLOD,
                AutoAdjustDensity = authoring.AutoAdjustDensity
            });

            // Add debug overlay config
            AddComponent(entity, new DebugOverlayConfig
            {
                ShowResourceFields = false,
                ShowFactionZones = false,
                ShowDebugPaths = false,
                ShowLODVisualization = authoring.EnableLODDebug,
                ShowMetrics = true,
                ShowInspector = true
            });
        }
    }

    /// <summary>
    /// Runtime system that spawns Demo_01 entities.
    /// This runs once at initialization to create the demo scene content.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Demo01SpawnSystem : ISystem
    {
        private bool _spawned;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PresentationLODConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_spawned)
            {
                return;
            }

            // Check if we have the Demo01 config
            if (!SystemAPI.HasSingleton<PerformanceBudgetConfig>())
            {
                return;
            }

            _spawned = true;

            // Demo entities are typically spawned by Space4XMiningDemoAuthoring
            // This system just ensures the presentation singletons are created

            // Create selection input singleton if not exists
            if (!SystemAPI.HasSingleton<SelectionInput>())
            {
                var inputEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(inputEntity, new SelectionInput());
            }

            // Create command input singleton if not exists
            if (!SystemAPI.HasSingleton<CommandInput>())
            {
                var inputEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(inputEntity, new CommandInput());
            }

            // Create selection state singleton if not exists
            if (!SystemAPI.HasSingleton<SelectionState>())
            {
                var stateEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(stateEntity, new SelectionState
                {
                    SelectedCount = 0,
                    PrimarySelected = Entity.Null,
                    Type = SelectionType.None
                });
            }

            // Create presentation metrics singleton if not exists
            if (!SystemAPI.HasSingleton<PresentationMetrics>())
            {
                var metricsEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(metricsEntity, new PresentationMetrics());
            }

            Debug.Log("[Demo01SpawnSystem] Demo_01 presentation singletons initialized.");
        }
    }
}

