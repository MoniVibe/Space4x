using NUnit.Framework;
using PureDOTS.Environment;
using PureDOTS.Runtime.Celestial;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Celestial;
using PureDOTS.Systems.Environment;
using PureDOTS.Systems.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Tests.Celestial
{
    public sealed class CelestialLightingIntegrationTests
    {
        [Test]
        public void CelestialOrbitPose_ComputesExpectedPositionAtEpoch()
        {
            using var world = new World("CelestialOrbitPoseTest");
            World.DefaultGameObjectInjectionWorld = world;
            var entityManager = world.EntityManager;

            CreateTimeSingletons(entityManager, tick: 0);

            var parent = entityManager.CreateEntity(typeof(CelestialOrbitPose), typeof(LocalTransform));
            entityManager.SetComponentData(parent, new CelestialOrbitPose
            {
                Position = float3.zero,
                Forward = new float3(0f, 0f, 1f),
                Up = new float3(0f, 1f, 0f),
                LastUpdateTick = 0
            });
            entityManager.SetComponentData(parent, LocalTransform.FromPosition(float3.zero));

            var orbiting = entityManager.CreateEntity(typeof(OrbitalState), typeof(CelestialOrbitPose), typeof(LocalTransform));
            entityManager.SetComponentData(orbiting, new OrbitalState
            {
                ParentBody = parent,
                SemiMajorAxis = 10f,
                Eccentricity = 0f,
                Inclination = 0f,
                ArgumentOfPeriapsis = 0f,
                LongitudeOfAscendingNode = 0f,
                OrbitalPeriod = 60f,
                CurrentPhase = 0f,
                MeanAnomaly = 0f,
                EpochTick = 0
            });

            var system = world.GetOrCreateSystem<CelestialOrbitPoseSystem>();
            system.Update(world.Unmanaged);

            var pose = entityManager.GetComponentData<CelestialOrbitPose>(orbiting);
            Assert.That(pose.Position.x, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(pose.Position.y, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(pose.Position.z, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void SunlightGrid_SolidVolumeZeroesLight()
        {
            using var world = new World("SunlightGridSolidTest");
            World.DefaultGameObjectInjectionWorld = world;
            var entityManager = world.EntityManager;

            CreateTimeSingletons(entityManager, tick: 0);
            CreateClimateSingleton(entityManager, hours: 12f);

            var gridEntity = entityManager.CreateEntity(typeof(SunlightGrid));
            var metadata = EnvironmentGridMetadata.Create(float3.zero, new float3(20f, 0f, 20f), 10f, new int2(2, 2));
            entityManager.SetComponentData(gridEntity, new SunlightGrid
            {
                Metadata = metadata,
                Blob = default,
                ChannelId = new FixedString64Bytes("sunlight"),
                SunDirection = new float3(0f, -1f, 0f),
                SunIntensity = 1f,
                LastUpdateTick = uint.MaxValue
            });
            var buffer = entityManager.AddBuffer<SunlightGridRuntimeSample>(gridEntity);
            buffer.ResizeUninitialized(metadata.CellCount);

            var flatSurface = entityManager.CreateEntity(typeof(TerrainFlatSurface));
            entityManager.SetComponentData(flatSurface, new TerrainFlatSurface
            {
                Height = 0f,
                Enabled = 1
            });

            var solidSphere = entityManager.CreateEntity(typeof(TerrainSolidSphere));
            entityManager.SetComponentData(solidSphere, new TerrainSolidSphere
            {
                Center = new float3(5f, -0.5f, 5f),
                Radius = 1f,
                Enabled = 1
            });

            var terrainConfigEntity = entityManager.CreateEntity(typeof(TerrainWorldConfig));
            entityManager.SetComponentData(terrainConfigEntity, TerrainWorldConfig.Default);

            var system = world.GetOrCreateSystem<SunlightGridUpdateSystem>();
            system.Update(world.Unmanaged);

            var samples = entityManager.GetBuffer<SunlightGridRuntimeSample>(gridEntity).Reinterpret<SunlightSample>();
            var cellIndex = EnvironmentGridMath.GetCellIndex(metadata, new int2(0, 0));
            var sample = samples[cellIndex];

            Assert.That(sample.DirectLight, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(sample.AmbientLight, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void VegetationSamplesSunlightGridAndFallback()
        {
            using var world = new World("VegetationSunlightSamplingTest");
            World.DefaultGameObjectInjectionWorld = world;
            var entityManager = world.EntityManager;

            CreateTimeSingletons(entityManager, tick: 0);

            var gridEntity = entityManager.CreateEntity(typeof(SunlightGrid));
            var metadata = EnvironmentGridMetadata.Create(float3.zero, new float3(20f, 0f, 20f), 10f, new int2(2, 2));
            entityManager.SetComponentData(gridEntity, new SunlightGrid
            {
                Metadata = metadata,
                Blob = default,
                ChannelId = new FixedString64Bytes("sunlight"),
                SunDirection = new float3(0f, -1f, 0f),
                SunIntensity = 1f,
                LastUpdateTick = 0
            });
            var buffer = entityManager.AddBuffer<SunlightGridRuntimeSample>(gridEntity);
            buffer.ResizeUninitialized(metadata.CellCount);
            var runtime = buffer.Reinterpret<SunlightSample>();
            for (int i = 0; i < runtime.Length; i++)
            {
                runtime[i] = new SunlightSample { DirectLight = 50f, AmbientLight = 10f, OccluderCount = 0 };
            }

            var vegetation = entityManager.CreateEntity(typeof(VegetationEnvironmentState), typeof(LocalTransform));
            entityManager.SetComponentData(vegetation, LocalTransform.FromPosition(new float3(5f, 0f, 5f)));

            var system = world.GetOrCreateSystem<VegetationSunlightIntegrationSystem>();
            system.Update(world.Unmanaged);

            var env = entityManager.GetComponentData<VegetationEnvironmentState>(vegetation);
            Assert.That(env.Light, Is.EqualTo(0.6f).Within(1e-3f));

            entityManager.DestroyEntity(gridEntity);
            var sunlightStateEntity = entityManager.CreateEntity(typeof(SunlightState));
            entityManager.SetComponentData(sunlightStateEntity, new SunlightState
            {
                GlobalIntensity = 0.3f,
                SourceStar = Entity.Null,
                LastUpdateTick = 0
            });

            system.Update(world.Unmanaged);
            env = entityManager.GetComponentData<VegetationEnvironmentState>(vegetation);
            Assert.That(env.Light, Is.EqualTo(0.36f).Within(1e-3f));
        }

        private static void CreateTimeSingletons(EntityManager entityManager, uint tick)
        {
            var timeEntity = entityManager.CreateEntity(typeof(TimeState));
            entityManager.SetComponentData(timeEntity, new TimeState
            {
                Tick = tick,
                DeltaTime = 1f / 60f,
                DeltaSeconds = 1f / 60f,
                FixedDeltaTime = 1f / 60f,
                WorldSeconds = tick * (1f / 60f),
                ElapsedTime = tick * (1f / 60f),
                CurrentSpeedMultiplier = 1f,
                IsPaused = false
            });

            var rewindEntity = entityManager.CreateEntity(typeof(RewindState));
            entityManager.SetComponentData(rewindEntity, new RewindState
            {
                Mode = RewindMode.Record,
                TargetTick = 0,
                TickDuration = 1f / 60f,
                MaxHistoryTicks = 1024,
                PendingStepTicks = 0
            });
        }

        private static void CreateClimateSingleton(EntityManager entityManager, float hours)
        {
            var climateEntity = entityManager.CreateEntity(typeof(ClimateState));
            entityManager.SetComponentData(climateEntity, new ClimateState
            {
                CurrentSeason = Season.Summer,
                SeasonProgress = 0.5f,
                TimeOfDayHours = hours,
                DayNightProgress = hours / 24f,
                GlobalTemperature = 20f,
                GlobalWindDirection = new float2(1f, 0f),
                GlobalWindStrength = 1f,
                AtmosphericMoisture = 50f,
                CloudCover = 0f,
                LastUpdateTick = 0
            });
        }
    }
}
