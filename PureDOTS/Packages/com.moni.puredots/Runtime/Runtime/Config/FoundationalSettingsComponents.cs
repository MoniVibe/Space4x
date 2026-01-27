using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Config
{
    /// <summary>
    /// Spatial partitioning configuration for hash grids, octrees, and spatial queries.
    /// Tweaking these affects memory usage, query performance, and spatial precision.
    /// </summary>
    public struct SpatialPartitioningConfig : IComponentData
    {
        /// <summary>Grid cell size in meters (1-1000). Smaller = more precise, more memory.</summary>
        public float CellSize;

        /// <summary>Expected max entities per cell (10-10000). Pre-allocation hint.</summary>
        public int MaxEntitiesPerCell;

        /// <summary>Default radius for spatial queries in meters (1-5000).</summary>
        public float QueryRadius;

        /// <summary>How often to rebuild spatial hash (1-60 ticks). Lower = fresher data, higher cost.</summary>
        public byte RebuildFrequency;

        /// <summary>Octree/BVH max depth for 3D partitioning (4-16).</summary>
        public byte MaxTreeDepth;

        /// <summary>Max entities per leaf node before split (8-256).</summary>
        public ushort LeafCapacity;

        public static SpatialPartitioningConfig Default => new SpatialPartitioningConfig
        {
            CellSize = 10f,
            MaxEntitiesPerCell = 100,
            QueryRadius = 150f,
            RebuildFrequency = 10,
            MaxTreeDepth = 8,
            LeafCapacity = 64
        };

        public static SpatialPartitioningConfig LowEnd => new SpatialPartitioningConfig
        {
            CellSize = 100f,
            MaxEntitiesPerCell = 50,
            QueryRadius = 100f,
            RebuildFrequency = 30,
            MaxTreeDepth = 6,
            LeafCapacity = 128
        };

        public static SpatialPartitioningConfig HighEnd => new SpatialPartitioningConfig
        {
            CellSize = 10f,
            MaxEntitiesPerCell = 1000,
            QueryRadius = 200f,
            RebuildFrequency = 5,
            MaxTreeDepth = 12,
            LeafCapacity = 32
        };
    }

    /// <summary>
    /// Physics engine configuration. Controls gravity, time steps, solver quality, and collision behavior.
    /// BURST-FRIENDLY: All blittable types, no managed references.
    /// </summary>
    public struct PhysicsEngineConfig : IComponentData
    {
        /// <summary>Gravity vector in m/s² (typically 0, -9.81, 0 for Earth).</summary>
        public float3 Gravity;

        /// <summary>Fixed physics time step in seconds (0.001-0.1). Smaller = more accurate, slower.</summary>
        public float FixedDeltaTime;

        /// <summary>Max velocity clamp to prevent tunneling (1-10000 m/s).</summary>
        public float MaxVelocity;

        /// <summary>Constraint solver iterations (1-50). More = more rigid, slower.</summary>
        public byte SolverIterations;

        /// <summary>Collision margin/"skin" in meters (0.001-0.1). Prevents micro-penetrations.</summary>
        public float CollisionMargin;

        /// <summary>Enable continuous collision detection (expensive but prevents tunneling).</summary>
        public bool ContinuousCollisionDetection;

        public static PhysicsEngineConfig Default => new PhysicsEngineConfig
        {
            Gravity = new float3(0, -9.81f, 0),
            FixedDeltaTime = 1f / 60f,
            MaxVelocity = 500f,
            SolverIterations = 8,
            CollisionMargin = 0.01f,
            ContinuousCollisionDetection = true
        };

        public static PhysicsEngineConfig LowEnd => new PhysicsEngineConfig
        {
            Gravity = new float3(0, -9.81f, 0),
            FixedDeltaTime = 1f / 30f,
            MaxVelocity = 300f,
            SolverIterations = 4,
            CollisionMargin = 0.02f,
            ContinuousCollisionDetection = false
        };

        public static PhysicsEngineConfig HighEnd => new PhysicsEngineConfig
        {
            Gravity = new float3(0, -9.81f, 0),
            FixedDeltaTime = 1f / 120f,
            MaxVelocity = 1000f,
            SolverIterations = 16,
            CollisionMargin = 0.005f,
            ContinuousCollisionDetection = true
        };

        public static PhysicsEngineConfig ZeroG => new PhysicsEngineConfig
        {
            Gravity = float3.zero,
            FixedDeltaTime = 1f / 60f,
            MaxVelocity = 5000f,
            SolverIterations = 8,
            CollisionMargin = 0.01f,
            ContinuousCollisionDetection = true
        };
    }

    /// <summary>
    /// Job system and parallelization configuration.
    /// Controls batch sizes, thread counts, and update frequencies.
    /// </summary>
    public struct JobSystemConfig : IComponentData
    {
        /// <summary>Default batch size for parallel jobs (1-10000).</summary>
        public int DefaultBatchSize;

        /// <summary>Max worker threads (1-128). Usually = CPU core count.</summary>
        public byte MaxThreads;

        /// <summary>Enable parallel scheduling. Set false to force serial (debug mode).</summary>
        public bool EnableParallelism;

        public static JobSystemConfig Default => new JobSystemConfig
        {
            DefaultBatchSize = 1000,
            MaxThreads = 8,
            EnableParallelism = true
        };

        public static JobSystemConfig LowEnd => new JobSystemConfig
        {
            DefaultBatchSize = 500,
            MaxThreads = 2,
            EnableParallelism = true
        };

        public static JobSystemConfig HighEnd => new JobSystemConfig
        {
            DefaultBatchSize = 2000,
            MaxThreads = 16,
            EnableParallelism = true
        };
    }

    /// <summary>
    /// Memory management and allocation configuration.
    /// Pre-allocation capacities for entities, components, and buffers.
    /// </summary>
    public struct MemoryManagementConfig : IComponentData
    {
        /// <summary>Pre-allocated entity capacity (100-1000000).</summary>
        public int EntityCapacity;

        /// <summary>Default component pool size (10-100000).</summary>
        public int DefaultComponentPoolSize;

        /// <summary>Default dynamic buffer capacity (4-1024 elements).</summary>
        public ushort DefaultBufferCapacity;

        /// <summary>Default NativeContainer capacity (16-65536).</summary>
        public int DefaultNativeContainerCapacity;

        public static MemoryManagementConfig Default => new MemoryManagementConfig
        {
            EntityCapacity = 50000,
            DefaultComponentPoolSize = 10000,
            DefaultBufferCapacity = 16,
            DefaultNativeContainerCapacity = 256
        };

        public static MemoryManagementConfig LowEnd => new MemoryManagementConfig
        {
            EntityCapacity = 10000,
            DefaultComponentPoolSize = 2000,
            DefaultBufferCapacity = 8,
            DefaultNativeContainerCapacity = 64
        };

        public static MemoryManagementConfig HighEnd => new MemoryManagementConfig
        {
            EntityCapacity = 500000,
            DefaultComponentPoolSize = 100000,
            DefaultBufferCapacity = 64,
            DefaultNativeContainerCapacity = 2048
        };
    }

    /// <summary>
    /// Rendering and presentation configuration.
    /// LOD distances, culling settings, batch sizes, shadow quality.
    /// </summary>
    public struct RenderingConfig : IComponentData
    {
        /// <summary>LOD distance thresholds in meters (3 levels).</summary>
        public float3 LODDistances; // [LOD0, LOD1, LOD2]

        /// <summary>LOD bias multiplier (0.1-10). &lt;1 = higher quality, &gt;1 = lower quality.</summary>
        public float LODBias;

        /// <summary>Maximum render distance in meters (10-10000).</summary>
        public float CullingDistance;

        /// <summary>Enable frustum culling.</summary>
        public bool FrustumCulling;

        /// <summary>Enable occlusion culling.</summary>
        public bool OcclusionCulling;

        /// <summary>Entities per GPU instanced draw call (1-1023).</summary>
        public ushort InstanceBatchSize;

        /// <summary>Number of shadow map cascades (0-4).</summary>
        public byte ShadowCascades;

        public static RenderingConfig Default => new RenderingConfig
        {
            LODDistances = new float3(50, 200, 1000),
            LODBias = 1.0f,
            CullingDistance = 500f,
            FrustumCulling = true,
            OcclusionCulling = true,
            InstanceBatchSize = 1023,
            ShadowCascades = 2
        };

        public static RenderingConfig LowEnd => new RenderingConfig
        {
            LODDistances = new float3(5, 10, 20),
            LODBias = 2.0f,
            CullingDistance = 100f,
            FrustumCulling = true,
            OcclusionCulling = false,
            InstanceBatchSize = 512,
            ShadowCascades = 1
        };

        public static RenderingConfig HighEnd => new RenderingConfig
        {
            LODDistances = new float3(200, 1000, 5000),
            LODBias = 0.5f,
            CullingDistance = 2000f,
            FrustumCulling = true,
            OcclusionCulling = true,
            InstanceBatchSize = 1023,
            ShadowCascades = 4
        };
    }

    /// <summary>
    /// AI and pathfinding configuration.
    /// Search budgets, heuristic weights, terrain costs, behavior thresholds.
    /// </summary>
    public struct AIPathfindingConfig : IComponentData
    {
        /// <summary>Max A* nodes explored before giving up (100-100000).</summary>
        public int MaxSearchNodes;

        /// <summary>A* heuristic weight (0.5-2.0). 1.0 = optimal, &gt;1.0 = greedy/fast.</summary>
        public float HeuristicWeight;

        /// <summary>Diagonal movement cost multiplier (1.0-2.0). 1.414 = Euclidean.</summary>
        public float DiagonalCost;

        /// <summary>HP ratio threshold for fleeing (0-1).</summary>
        public float FleeThreshold;

        /// <summary>Ticks between AI decision updates (1-300).</summary>
        public ushort DecisionUpdateFrequency;

        public static AIPathfindingConfig Default => new AIPathfindingConfig
        {
            MaxSearchNodes = 5000,
            HeuristicWeight = 1.0f,
            DiagonalCost = 1.414f,
            FleeThreshold = 0.3f,
            DecisionUpdateFrequency = 30
        };

        public static AIPathfindingConfig Fast => new AIPathfindingConfig
        {
            MaxSearchNodes = 500,
            HeuristicWeight = 2.0f,
            DiagonalCost = 1.0f,
            FleeThreshold = 0.3f,
            DecisionUpdateFrequency = 60
        };

        public static AIPathfindingConfig Quality => new AIPathfindingConfig
        {
            MaxSearchNodes = 50000,
            HeuristicWeight = 1.0f,
            DiagonalCost = 1.414f,
            FleeThreshold = 0.3f,
            DecisionUpdateFrequency = 10
        };
    }

    /// <summary>
    /// Gameplay rules and balancing constants.
    /// Damage formulas, resource rates, time scaling, build speeds.
    /// </summary>
    public struct GameplayRulesConfig : IComponentData
    {
        /// <summary>Defense scaling exponent (0.5-2.0). 1.0 = linear.</summary>
        public float DefenseExponent;

        /// <summary>Critical hit damage multiplier (1.5-5.0).</summary>
        public float CriticalMultiplier;

        /// <summary>Global resource gather rate multiplier (0.1-10.0).</summary>
        public float ResourceGatherRateMultiplier;

        /// <summary>Day/night cycle duration in seconds (10-3600).</summary>
        public float DayNightCycleDuration;

        /// <summary>Global build time multiplier (0.01-10.0).</summary>
        public float BuildTimeMultiplier;

        public static GameplayRulesConfig Default => new GameplayRulesConfig
        {
            DefenseExponent = 1.0f,
            CriticalMultiplier = 2.0f,
            ResourceGatherRateMultiplier = 1.0f,
            DayNightCycleDuration = 600f,
            BuildTimeMultiplier = 1.0f
        };

        public static GameplayRulesConfig Casual => new GameplayRulesConfig
        {
            DefenseExponent = 1.0f,
            CriticalMultiplier = 1.5f,
            ResourceGatherRateMultiplier = 3.0f,
            DayNightCycleDuration = 300f,
            BuildTimeMultiplier = 0.5f
        };

        public static GameplayRulesConfig Hardcore => new GameplayRulesConfig
        {
            DefenseExponent = 1.0f,
            CriticalMultiplier = 3.0f,
            ResourceGatherRateMultiplier = 0.5f,
            DayNightCycleDuration = 1200f,
            BuildTimeMultiplier = 2.0f
        };

        public static GameplayRulesConfig Debug => new GameplayRulesConfig
        {
            DefenseExponent = 1.0f,
            CriticalMultiplier = 2.0f,
            ResourceGatherRateMultiplier = 100.0f,
            DayNightCycleDuration = 10f,
            BuildTimeMultiplier = 0.01f
        };
    }

    /// <summary>
    /// Complete foundational settings profile.
    /// Bundles all configuration categories for easy save/load/compare.
    /// </summary>
    public struct FoundationalSettingsProfile : IComponentData
    {
        public ProfileType Type;

        // References to config entities (or embed configs directly)
        public SpatialPartitioningConfig Spatial;
        public PhysicsEngineConfig Physics;
        public JobSystemConfig JobSystem;
        public MemoryManagementConfig Memory;
        public RenderingConfig Rendering;
        public AIPathfindingConfig AIPathfinding;
        public GameplayRulesConfig GameplayRules;

        public enum ProfileType : byte
        {
            Custom = 0,
            Default = 1,
            LowEnd = 2,
            HighEnd = 3,
            Casual = 4,
            Hardcore = 5,
            Debug = 6,
            PhysicsStress = 7,
            PathfindingStress = 8
        }

        public static FoundationalSettingsProfile CreateDefault()
        {
            return new FoundationalSettingsProfile
            {
                Type = ProfileType.Default,
                Spatial = SpatialPartitioningConfig.Default,
                Physics = PhysicsEngineConfig.Default,
                JobSystem = JobSystemConfig.Default,
                Memory = MemoryManagementConfig.Default,
                Rendering = RenderingConfig.Default,
                AIPathfinding = AIPathfindingConfig.Default,
                GameplayRules = GameplayRulesConfig.Default
            };
        }

        public static FoundationalSettingsProfile CreateLowEnd()
        {
            return new FoundationalSettingsProfile
            {
                Type = ProfileType.LowEnd,
                Spatial = SpatialPartitioningConfig.LowEnd,
                Physics = PhysicsEngineConfig.LowEnd,
                JobSystem = JobSystemConfig.LowEnd,
                Memory = MemoryManagementConfig.LowEnd,
                Rendering = RenderingConfig.LowEnd,
                AIPathfinding = AIPathfindingConfig.Fast,
                GameplayRules = GameplayRulesConfig.Default
            };
        }

        public static FoundationalSettingsProfile CreateHighEnd()
        {
            return new FoundationalSettingsProfile
            {
                Type = ProfileType.HighEnd,
                Spatial = SpatialPartitioningConfig.HighEnd,
                Physics = PhysicsEngineConfig.HighEnd,
                JobSystem = JobSystemConfig.HighEnd,
                Memory = MemoryManagementConfig.HighEnd,
                Rendering = RenderingConfig.HighEnd,
                AIPathfinding = AIPathfindingConfig.Quality,
                GameplayRules = GameplayRulesConfig.Default
            };
        }

        public static FoundationalSettingsProfile CreateCasual()
        {
            var profile = CreateDefault();
            profile.Type = ProfileType.Casual;
            profile.GameplayRules = GameplayRulesConfig.Casual;
            return profile;
        }

        public static FoundationalSettingsProfile CreateHardcore()
        {
            var profile = CreateDefault();
            profile.Type = ProfileType.Hardcore;
            profile.GameplayRules = GameplayRulesConfig.Hardcore;
            return profile;
        }

        public static FoundationalSettingsProfile CreateDebug()
        {
            var profile = CreateDefault();
            profile.Type = ProfileType.Debug;
            profile.GameplayRules = GameplayRulesConfig.Debug;
            return profile;
        }
    }

    /// <summary>
    /// Tracks foundational setting changes for logging and replay.
    /// Used by sandbox inspector to record all tweaks.
    /// </summary>
    public struct FoundationalSettingChangeEvent : IBufferElementData
    {
        public uint Tick;
        public SettingCategory Category;
        public FixedString64Bytes SettingName;
        public float PreviousValue;
        public float NewValue;

        public enum SettingCategory : byte
        {
            Spatial = 0,
            Physics = 1,
            JobSystem = 2,
            Memory = 3,
            Rendering = 4,
            AIPathfinding = 5,
            GameplayRules = 6
        }
    }

    /// <summary>
    /// Performance metrics tracked during foundational setting tweaks.
    /// Updated every frame to show live impact of changes.
    /// </summary>
    public struct FoundationalPerformanceMetrics : IComponentData
    {
        public float FrameTimeMs;
        public float PhysicsTimeMs;
        public float RenderTimeMs;
        public float PathfindingTimeMs;

        public int AllocationsThisFrame;
        public long TotalMemoryMB;

        public int SpatialHashCellCount;
        public float SpatialQueryAvgMs;

        public int EntitiesRendered;
        public int DrawCallsThisFrame;

        public int PathfindingQueriesThisFrame;
        public float PathfindingSuccessRate;
    }
}
