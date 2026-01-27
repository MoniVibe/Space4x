using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.TestUtilities
{
    /// <summary>
    /// Configuration for creating a registry test world.
    /// </summary>
    public struct RegistryTestConfig
    {
        /// <summary>
        /// Name for the test world.
        /// </summary>
        public string WorldName;

        /// <summary>
        /// Whether to boot core singletons (TimeState, RewindState, etc.).
        /// </summary>
        public bool BootCoreSingletons;

        /// <summary>
        /// Whether to create a TelemetryStream singleton.
        /// </summary>
        public bool CreateTelemetryStream;

        /// <summary>
        /// Whether to create a RegistryDirectory singleton.
        /// </summary>
        public bool CreateRegistryDirectory;

        /// <summary>
        /// Whether to create a spatial grid.
        /// </summary>
        public bool CreateSpatialGrid;

        /// <summary>
        /// Deterministic random seed for test reproducibility.
        /// </summary>
        public uint RandomSeed;

        /// <summary>
        /// Initial tick value.
        /// </summary>
        public uint InitialTick;

        /// <summary>
        /// Fixed delta time in seconds.
        /// </summary>
        public float FixedDeltaTime;

        /// <summary>
        /// Creates a default configuration suitable for most registry tests.
        /// </summary>
        public static RegistryTestConfig CreateDefault(string worldName = "TestWorld")
        {
            return new RegistryTestConfig
            {
                WorldName = worldName,
                BootCoreSingletons = true,
                CreateTelemetryStream = true,
                CreateRegistryDirectory = true,
                CreateSpatialGrid = false,
                RandomSeed = 12345u,
                InitialTick = 1u,
                FixedDeltaTime = 1f / 60f
            };
        }

        /// <summary>
        /// Creates a minimal configuration without optional features.
        /// </summary>
        public static RegistryTestConfig CreateMinimal(string worldName = "MinimalTestWorld")
        {
            return new RegistryTestConfig
            {
                WorldName = worldName,
                BootCoreSingletons = true,
                CreateTelemetryStream = false,
                CreateRegistryDirectory = false,
                CreateSpatialGrid = false,
                RandomSeed = 0u,
                InitialTick = 0u,
                FixedDeltaTime = 1f / 60f
            };
        }

        /// <summary>
        /// Creates a full configuration with all features enabled.
        /// </summary>
        public static RegistryTestConfig CreateFull(string worldName = "FullTestWorld")
        {
            return new RegistryTestConfig
            {
                WorldName = worldName,
                BootCoreSingletons = true,
                CreateTelemetryStream = true,
                CreateRegistryDirectory = true,
                CreateSpatialGrid = true,
                RandomSeed = 12345u,
                InitialTick = 1u,
                FixedDeltaTime = 1f / 60f
            };
        }
    }

    /// <summary>
    /// Parameters for creating test colonies.
    /// </summary>
    public struct ColonyTestParams
    {
        public int ColonyId;
        public float SupplyDemand;
        public float SupplyAmount;
        public float3 Position;

        public static ColonyTestParams CreateDefault(int id = 1)
        {
            return new ColonyTestParams
            {
                ColonyId = id,
                SupplyDemand = 100f,
                SupplyAmount = 80f,
                Position = float3.zero
            };
        }
    }

    /// <summary>
    /// Parameters for creating test fleets.
    /// </summary>
    public struct FleetTestParams
    {
        public int FleetId;
        public int FactionId;
        public int ShipCount;
        public float3 Position;

        public static FleetTestParams CreateDefault(int id = 1)
        {
            return new FleetTestParams
            {
                FleetId = id,
                FactionId = 1,
                ShipCount = 5,
                Position = float3.zero
            };
        }
    }

    /// <summary>
    /// Parameters for creating test logistics routes.
    /// </summary>
    public struct LogisticsRouteTestParams
    {
        public int RouteId;
        public Entity SourceEntity;
        public Entity DestinationEntity;
        public float CargoCapacity;

        public static LogisticsRouteTestParams CreateDefault(int id = 1)
        {
            return new LogisticsRouteTestParams
            {
                RouteId = id,
                SourceEntity = Entity.Null,
                DestinationEntity = Entity.Null,
                CargoCapacity = 100f
            };
        }
    }

    /// <summary>
    /// Parameters for creating test miracles.
    /// </summary>
    public struct MiracleTestParams
    {
        public int MiracleId;
        public PureDOTS.Runtime.Components.MiracleType Type;
        public PureDOTS.Runtime.Components.MiracleCastingMode CastingMode;
        public float3 TargetPosition;
        public Entity CasterEntity;

        public static MiracleTestParams CreateDefault(int id = 1)
        {
            return new MiracleTestParams
            {
                MiracleId = id,
                Type = PureDOTS.Runtime.Components.MiracleType.Rain,
                CastingMode = PureDOTS.Runtime.Components.MiracleCastingMode.Thrown, // Token equivalent in Components namespace
                TargetPosition = float3.zero,
                CasterEntity = Entity.Null
            };
        }
    }
}

