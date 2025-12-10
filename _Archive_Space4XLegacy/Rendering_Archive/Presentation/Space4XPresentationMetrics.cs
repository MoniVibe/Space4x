using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    // ============================================================================
    // Metrics Components
    // ============================================================================

    /// <summary>
    /// Presentation performance metrics. Singleton component.
    /// </summary>
    public struct PresentationMetrics : IComponentData
    {
        /// <summary>Total number of entities with presentation components</summary>
        public int TotalPresentationEntities;
        /// <summary>Number of entities currently being rendered</summary>
        public int VisibleEntities;
        /// <summary>Number of entities at FullDetail LOD</summary>
        public int FullDetailCount;
        /// <summary>Number of entities at ReducedDetail LOD</summary>
        public int ReducedDetailCount;
        /// <summary>Number of entities at Impostor LOD</summary>
        public int ImpostorCount;
        /// <summary>Number of entities at Hidden LOD</summary>
        public int HiddenCount;
        /// <summary>Number of carriers visible</summary>
        public int VisibleCarriers;
        /// <summary>Number of crafts visible</summary>
        public int VisibleCrafts;
        /// <summary>Number of asteroids visible</summary>
        public int VisibleAsteroids;
        /// <summary>Current render density (0-1)</summary>
        public float CurrentRenderDensity;
        /// <summary>Frame time for presentation systems (ms)</summary>
        public float PresentationFrameTimeMs;
        /// <summary>Number of active fleet impostors</summary>
        public int FleetImpostorCount;
        /// <summary>Number of real fleets (with Space4XFleet component)</summary>
        public int RealFleetCount;
        /// <summary>Entity creation rate (entities per second)</summary>
        public float EntityCreationRate;
        /// <summary>Entity destruction rate (entities per second)</summary>
        public float EntityDestructionRate;
        /// <summary>Last update tick</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Performance budget configuration. Singleton component.
    /// </summary>
    public struct PerformanceBudgetConfig : IComponentData
    {
        /// <summary>Maximum full detail carriers</summary>
        public int MaxFullDetailCarriers;
        /// <summary>Maximum full detail crafts</summary>
        public int MaxFullDetailCrafts;
        /// <summary>Maximum reduced detail entities</summary>
        public int MaxReducedDetailEntities;
        /// <summary>Maximum fleet impostors</summary>
        public int MaxFleetImpostors;
        /// <summary>Maximum draw calls target</summary>
        public int MaxDrawCalls;
        /// <summary>Frame time budget in ms</summary>
        public float FrameTimeBudgetMs;
        /// <summary>Enable automatic LOD adjustment</summary>
        public bool AutoAdjustLOD;
        /// <summary>Enable automatic density adjustment</summary>
        public bool AutoAdjustDensity;

        public static PerformanceBudgetConfig Default => new PerformanceBudgetConfig
        {
            MaxFullDetailCarriers = 100,
            MaxFullDetailCrafts = 1000,
            MaxReducedDetailEntities = 10000,
            MaxFleetImpostors = 1000,
            MaxDrawCalls = 500,
            FrameTimeBudgetMs = 16f,
            AutoAdjustLOD = true,
            AutoAdjustDensity = true
        };
    }

    /// <summary>
    /// System that collects presentation metrics.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup), OrderLast = true)]
    public partial struct Space4XPresentationMetricsSystem : ISystem
    {
        private uint _frameCount;

        public void OnCreate(ref SystemState state)
        {
            // Create metrics singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<PresentationMetrics>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new PresentationMetrics());
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            _frameCount++;

            // Only update metrics every 10 frames to reduce overhead
            if (_frameCount % 10 != 0)
            {
                return;
            }

            var metrics = new PresentationMetrics
            {
                LastUpdateTick = _frameCount
            };

            // Count entities by LOD level (using PureDOTS RenderLODData)
            foreach (var lodData in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.RenderLODData>>())
            {
                metrics.TotalPresentationEntities++;

                byte lod = lodData.ValueRO.RecommendedLOD;
                if (lod < 3) // Visible (0-2, PureDOTS uses 3 for hidden)
                {
                    metrics.VisibleEntities++;
                    
                    switch (lod)
                    {
                        case 0:
                            metrics.FullDetailCount++;
                            break;
                        case 1:
                            metrics.ReducedDetailCount++;
                            break;
                        case 2:
                            metrics.ImpostorCount++;
                            break;
                        default:
                            break;
                    }
                }
                else // Hidden/Culled (3)
                {
                    metrics.HiddenCount++;
                }
            }

            // Count visible carriers
            foreach (var lodData in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.RenderLODData>>().WithAll<CarrierPresentationTag>())
            {
                if (lodData.ValueRO.RecommendedLOD < 3)
                {
                    metrics.VisibleCarriers++;
                }
            }

            // Count visible crafts
            foreach (var lodData in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.RenderLODData>>().WithAll<CraftPresentationTag>())
            {
                if (lodData.ValueRO.RecommendedLOD < 3)
                {
                    metrics.VisibleCrafts++;
                }
            }

            // Count visible asteroids
            foreach (var lodData in SystemAPI.Query<RefRO<PureDOTS.Runtime.Rendering.RenderLODData>>().WithAll<AsteroidPresentationTag>())
            {
                if (lodData.ValueRO.RecommendedLOD < 3)
                {
                    metrics.VisibleAsteroids++;
                }
            }

            // Count fleet impostors
            foreach (var _ in SystemAPI.Query<RefRO<FleetImpostorTag>>())
            {
                metrics.FleetImpostorCount++;
            }

            // Count real fleets
            foreach (var _ in SystemAPI.Query<RefRO<Space4XFleet>>())
            {
                metrics.RealFleetCount++;
            }

            // Get current render density
            if (SystemAPI.TryGetSingleton<RenderDensityConfig>(out var densityConfig))
            {
                metrics.CurrentRenderDensity = densityConfig.Density;
            }
            else
            {
                metrics.CurrentRenderDensity = 1f;
            }

            // Update metrics singleton
            if (SystemAPI.TryGetSingletonEntity<PresentationMetrics>(out var metricsEntity))
            {
                state.EntityManager.SetComponentData(metricsEntity, metrics);
            }
        }
    }

    /// <summary>
    /// System that auto-adjusts LOD and render density based on performance budgets.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(Space4XPresentationMetricsSystem))]
    public partial struct Space4XPerformanceBudgetSystem : ISystem
    {
        private uint _frameCount;

        public void OnCreate(ref SystemState state)
        {
            // Create budget config singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<PerformanceBudgetConfig>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, PerformanceBudgetConfig.Default);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            _frameCount++;

            // Only check budgets every 30 frames
            if (_frameCount % 30 != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PresentationMetrics>(out var metrics))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PerformanceBudgetConfig>(out var budgetConfig))
            {
                return;
            }

            // Auto-adjust LOD thresholds if over budget
            if (budgetConfig.AutoAdjustLOD)
            {
                AdjustLODThresholds(ref state, metrics, budgetConfig);
            }

            // Auto-adjust render density if over budget
            if (budgetConfig.AutoAdjustDensity)
            {
                AdjustRenderDensity(ref state, metrics, budgetConfig);
            }
        }

        private void AdjustLODThresholds(ref SystemState state, PresentationMetrics metrics, PerformanceBudgetConfig budgetConfig)
        {
            if (!SystemAPI.TryGetSingletonEntity<PresentationLODConfig>(out var lodConfigEntity))
            {
                return;
            }

            var lodConfig = state.EntityManager.GetComponentData<PresentationLODConfig>(lodConfigEntity);
            bool changed = false;

            // If too many full detail entities, reduce full detail range
            if (metrics.FullDetailCount > budgetConfig.MaxFullDetailCrafts)
            {
                lodConfig.FullDetailMaxDistance = math.max(20f, lodConfig.FullDetailMaxDistance * 0.9f);
                changed = true;
            }
            // If well under budget, can increase range
            else if (metrics.FullDetailCount < budgetConfig.MaxFullDetailCrafts * 0.5f && lodConfig.FullDetailMaxDistance < 100f)
            {
                lodConfig.FullDetailMaxDistance = math.min(100f, lodConfig.FullDetailMaxDistance * 1.05f);
                changed = true;
            }

            // If too many reduced detail entities, reduce range
            if (metrics.ReducedDetailCount > budgetConfig.MaxReducedDetailEntities)
            {
                lodConfig.ReducedDetailMaxDistance = math.max(100f, lodConfig.ReducedDetailMaxDistance * 0.9f);
                changed = true;
            }

            if (changed)
            {
                state.EntityManager.SetComponentData(lodConfigEntity, lodConfig);
            }
        }

        private void AdjustRenderDensity(ref SystemState state, PresentationMetrics metrics, PerformanceBudgetConfig budgetConfig)
        {
            if (!SystemAPI.TryGetSingletonEntity<RenderDensityConfig>(out var densityConfigEntity))
            {
                return;
            }

            var densityConfig = state.EntityManager.GetComponentData<RenderDensityConfig>(densityConfigEntity);

            // If too many visible crafts, reduce density
            if (metrics.VisibleCrafts > budgetConfig.MaxFullDetailCrafts)
            {
                float targetDensity = (float)budgetConfig.MaxFullDetailCrafts / metrics.VisibleCrafts;
                densityConfig.Density = math.max(0.1f, math.lerp(densityConfig.Density, targetDensity, 0.1f));
                state.EntityManager.SetComponentData(densityConfigEntity, densityConfig);
            }
            // If well under budget, can increase density
            else if (metrics.VisibleCrafts < budgetConfig.MaxFullDetailCrafts * 0.5f && densityConfig.Density < 1f)
            {
                densityConfig.Density = math.min(1f, densityConfig.Density * 1.05f);
                state.EntityManager.SetComponentData(densityConfigEntity, densityConfig);
            }
        }
    }

    /// <summary>
    /// Authoring component for performance budget configuration.
    /// </summary>
    public class PerformanceBudgetConfigAuthoring : UnityEngine.MonoBehaviour
    {
        [UnityEngine.Header("Entity Budgets")]
        public int MaxFullDetailCarriers = 100;
        public int MaxFullDetailCrafts = 1000;
        public int MaxReducedDetailEntities = 10000;
        public int MaxFleetImpostors = 1000;

        [UnityEngine.Header("Performance Budgets")]
        public int MaxDrawCalls = 500;
        public float FrameTimeBudgetMs = 16f;

        [UnityEngine.Header("Auto-Adjustment")]
        public bool AutoAdjustLOD = true;
        public bool AutoAdjustDensity = true;
    }

    /// <summary>
    /// Baker for PerformanceBudgetConfigAuthoring.
    /// </summary>
    public class PerformanceBudgetConfigBaker : Baker<PerformanceBudgetConfigAuthoring>
    {
        public override void Bake(PerformanceBudgetConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PerformanceBudgetConfig
            {
                MaxFullDetailCarriers = authoring.MaxFullDetailCarriers,
                MaxFullDetailCrafts = authoring.MaxFullDetailCrafts,
                MaxReducedDetailEntities = authoring.MaxReducedDetailEntities,
                MaxFleetImpostors = authoring.MaxFleetImpostors,
                MaxDrawCalls = authoring.MaxDrawCalls,
                FrameTimeBudgetMs = authoring.FrameTimeBudgetMs,
                AutoAdjustLOD = authoring.AutoAdjustLOD,
                AutoAdjustDensity = authoring.AutoAdjustDensity
            });
        }
    }
}

