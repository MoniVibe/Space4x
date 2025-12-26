using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Celestial;
using PureDOTS.Runtime.Environment;
using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Time;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Scenario
{
    /// <summary>
    /// Ensures the canonical star + planetoid entities exist for Space4X scenarios.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.CoreSingletonBootstrapSystem))]
    public partial struct Space4XCelestialBootstrapSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                state.Enabled = false;
                return;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableSpace4x)
            {
                return;
            }

            if (!SystemAPI.QueryBuilder().WithAll<StarLuminosity>().Build().IsEmptyIgnoreFilter)
            {
                _initialized = true;
                state.Enabled = false;
                return;
            }

            var entityManager = state.EntityManager;

            var star = entityManager.CreateEntity();
            entityManager.AddComponentData(star, new StarLuminosity { Luminosity = 1f });
            entityManager.AddComponentData(star, new StarSolarYield { Yield = 1f, LastCalculationTick = 0 });
            entityManager.AddComponentData(star, LocalTransform.FromPosition(float3.zero));

            var world = entityManager.CreateEntity();
            entityManager.AddComponentData(world, new StarParent { ParentStar = star });
            entityManager.AddComponentData(world, new SunlightState { GlobalIntensity = 1f, SourceStar = star, LastUpdateTick = 0 });
            entityManager.AddComponentData(world, new OrbitParameters
            {
                OrbitalPeriodSeconds = 86400f,
                InitialPhase = 0f,
                OrbitNormal = new float3(0f, 1f, 0f),
                TimeOfDayOffset = 0f,
                ParentPlanet = Entity.Null
            });
            entityManager.AddComponentData(world, new OrbitState { OrbitalPhase = 0f, LastUpdateTick = 0 });
            entityManager.AddComponentData(world, new TimeOfDayState
            {
                TimeOfDayNorm = 0f,
                Phase = TimeOfDayPhase.Night,
                PreviousPhase = TimeOfDayPhase.Night
            });
            entityManager.AddComponentData(world, TimeOfDayConfig.Default);
            entityManager.AddComponentData(world, new SunlightFactor { Sunlight = 1f });

            entityManager.AddComponentData(world, new OrbitalState
            {
                ParentBody = star,
                SemiMajorAxis = 50f,
                Eccentricity = 0f,
                Inclination = 0f,
                ArgumentOfPeriapsis = 0f,
                LongitudeOfAscendingNode = 0f,
                OrbitalPeriod = 3600f,
                CurrentPhase = 0f,
                MeanAnomaly = 0f,
                EpochTick = 0
            });
            entityManager.AddComponentData(world, new CelestialOrbitPose
            {
                Position = new float3(50f, 0f, 0f),
                Forward = new float3(0f, 0f, 1f),
                Up = new float3(0f, 1f, 0f),
                LastUpdateTick = 0
            });
            entityManager.AddComponentData(world, LocalTransform.FromPosition(new float3(50f, 0f, 0f)));

            _initialized = true;
            state.Enabled = false;
        }
    }
}
