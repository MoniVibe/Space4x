using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Bootstrap system that creates environment singletons and initializes moisture grid.
    /// Runs in InitializationSystemGroup to set up environment infrastructure.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct EnvironmentBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // This system runs once to set up environment infrastructure
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // No-op; initialization happens in OnCreate
        }

        /// <summary>
        /// Initializes environment system by creating singletons and initializing moisture grid.
        /// Called from game-specific bootstrap code.
        /// </summary>
        public static void InitializeEnvironmentSystem(EntityManager entityManager)
        {
            // Check if already initialized
            if (HasSingleton<PureDOTS.Environment.ClimateState>(entityManager))
            {
                return; // Already initialized
            }

            // Create ClimateState singleton
            var climateEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(climateEntity, new PureDOTS.Environment.ClimateState
            {
                CurrentSeason = PureDOTS.Environment.Season.Spring,
                SeasonProgress = 0f,
                TimeOfDayHours = 12f,
                DayNightProgress = 0.5f,
                GlobalTemperature = 20f,
                GlobalWindDirection = new float2(1f, 0f),
                GlobalWindStrength = 5f,
                AtmosphericMoisture = 50f,
                CloudCover = 30f,
                LastUpdateTick = 0
            });

            // Create WindState singleton
            var windEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(windEntity, new WindState
            {
                Direction = new float2(1f, 0f), // East
                Strength = WindConfig.Default.BaseStrength,
                Type = WindType.Breeze,
                LastUpdateTick = 0
            });

            // Create WindConfig singleton
            var windConfigEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(windConfigEntity, WindConfig.Default);

            // Create SunlightState singleton
            var sunlightEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(sunlightEntity, new SunlightState
            {
                GlobalIntensity = 1f,
                SourceStar = Entity.Null,
                LastUpdateTick = 0
            });

            // Create SunlightConfig singleton
            var sunlightConfigEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(sunlightConfigEntity, SunlightConfig.Default);

            // Initialize moisture grid aligned with SpatialGridConfig
            InitializeMoistureGrid(entityManager);

            // Create MoistureConfig singleton
            var moistureConfigEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(moistureConfigEntity, MoistureConfig.Default);
        }

        private static void InitializeMoistureGrid(EntityManager entityManager)
        {
            // Check if SpatialGridConfig exists
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpatialGridConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                // No spatial grid, create minimal moisture grid
                CreateEmptyMoistureGrid(entityManager);
                return;
            }

            var spatialConfig = query.GetSingleton<SpatialGridConfig>();
            var width = spatialConfig.CellCounts.x;
            var height = spatialConfig.CellCounts.y;
            var cellSize = spatialConfig.CellSize;

            // Build moisture grid blob aligned with spatial grid
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MoistureCellBlob>();
            var cellsArray = builder.Allocate(ref root.Cells, width * height);

            // Initialize all cells with default moisture
            var defaultMoisture = 0.5f; // 50% moisture
            for (int i = 0; i < cellsArray.Length; i++)
            {
                cellsArray[i] = new MoistureCell
                {
                    Moisture = defaultMoisture,
                    DrainageRate = MoistureConfig.Default.DrainageFactor,
                    AbsorptionRate = MoistureConfig.Default.BaseAbsorptionRate,
                    LastUpdateTick = 0
                };
            }

            var blob = builder.CreateBlobAssetReference<MoistureCellBlob>(Allocator.Persistent);
            builder.Dispose();

            // Create MoistureGridState singleton
            var moistureGridEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(moistureGridEntity, new MoistureGridState
            {
                Grid = blob,
                Width = width,
                Height = height,
                CellSize = cellSize,
                LastUpdateTick = 0
            });
        }

        private static void CreateEmptyMoistureGrid(EntityManager entityManager)
        {
            // Create empty moisture grid (1x1)
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MoistureCellBlob>();
            builder.Allocate(ref root.Cells, 0);
            var blob = builder.CreateBlobAssetReference<MoistureCellBlob>(Allocator.Persistent);
            builder.Dispose();

            var moistureGridEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(moistureGridEntity, new MoistureGridState
            {
                Grid = blob,
                Width = 1,
                Height = 1,
                CellSize = 1f,
                LastUpdateTick = 0
            });
        }

        private static bool HasSingleton<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }
    }
}

