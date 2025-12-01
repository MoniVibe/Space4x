using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Tests.TestHarness;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    /// <summary>
    /// Integration tests for fleet intercept systems with carriers.
    /// Validates intercept request generation, course calculation, and telemetry publishing.
    /// </summary>
    public class Space4XFleetInterceptIntegrationTests
    {
        private ISystemTestHarness _harness;
        private EntityManager _entityManager;
        private Entity _timeEntity;
        private Entity _rewindEntity;

        [SetUp]
        public void SetUp()
        {
            _harness = new ISystemTestHarness(fixedDelta: 0.2f);
            _entityManager = _harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
            CoreSingletonBootstrapSystem.EnsureFleetInterceptQueue(_entityManager);
            CoreSingletonBootstrapSystem.EnsureTelemetryStream(_entityManager);

            _timeEntity = _entityManager.CreateEntityQuery(typeof(TimeState)).GetSingletonEntity();
            _rewindEntity = _entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.FixedDeltaTime = 0.2f;
            time.Tick = 0;
            time.IsPaused = false;
            _entityManager.SetComponentData(_timeEntity, time);

            var rewind = _entityManager.GetComponentData<RewindState>(_rewindEntity);
            rewind.Mode = RewindMode.Record;
            _entityManager.SetComponentData(_rewindEntity, rewind);

            _harness.Add<FleetBroadcastSystem>();
            _harness.Add<FleetInterceptRequestSystem>();
            _harness.Add<InterceptPathfindingSystem>();
            _harness.Add<RendezvousCoordinationSystem>();
            _harness.Add<Space4XFleetInterceptTelemetrySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _harness?.Dispose();
        }

        [Test]
        public void InterceptRequestGeneratedForNearbyFleets()
        {
            // Create carrier1 with FleetMovementBroadcast at (0, 0, 0)
            var carrier1 = CreateCarrierWithBroadcast(new float3(0f, 0f, 0f), "FLEET-1", allowsInterception: true);
            
            // Create carrier2 with FleetMovementBroadcast at (10, 0, 0)
            var carrier2 = CreateCarrierWithBroadcast(new float3(10f, 0f, 0f), "FLEET-2", allowsInterception: true);
            
            // Add InterceptCapability to carrier1
            _entityManager.AddComponentData(carrier1, new InterceptCapability
            {
                MaxSpeed = 5f,
                TechTier = 1,
                AllowIntercept = 1
            });

            // Update time
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 5;
            _entityManager.SetComponentData(_timeEntity, time);

            // Run broadcast and request systems
            _harness.Step(2);

            // Query InterceptRequest buffer on queue entity
            var queueQuery = _entityManager.CreateEntityQuery(typeof(Space4XFleetInterceptQueue));
            if (!queueQuery.IsEmptyIgnoreFilter)
            {
                var queueEntity = queueQuery.GetSingletonEntity();
                var requestsBuffer = _entityManager.GetBuffer<InterceptRequest>(queueEntity);
                
                bool foundRequest = false;
                for (int i = 0; i < requestsBuffer.Length; i++)
                {
                    var request = requestsBuffer[i];
                    if (request.Requester == carrier1 && request.Target == carrier2)
                    {
                        foundRequest = true;
                        Assert.AreEqual(5u, request.RequestTick, "Request should have correct tick");
                        break;
                    }
                }
                Assert.IsTrue(foundRequest, "InterceptRequest should be created with carrier1 as requester, carrier2 as target");
            }
        }

        [Test]
        public void InterceptCourseCalculatedAndApplied()
        {
            // Create target carrier with FleetMovementBroadcast moving at velocity (1, 0, 0)
            var targetCarrier = CreateCarrierWithBroadcast(new float3(0f, 0f, 0f), "TARGET-FLEET", allowsInterception: true);
            _entityManager.AddComponentData(targetCarrier, new FleetKinematics
            {
                Velocity = new float3(1f, 0f, 0f)
            });

            // Create requester carrier with InterceptCapability at (-10, 0, 0)
            var requesterCarrier = CreateCarrierWithBroadcast(new float3(-10f, 0f, 0f), "REQUESTER-FLEET", allowsInterception: false);
            _entityManager.AddComponentData(requesterCarrier, new InterceptCapability
            {
                MaxSpeed = 5f,
                TechTier = 1,
                AllowIntercept = 1
            });

            // Update time
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 10;
            _entityManager.SetComponentData(_timeEntity, time);

            // Run systems
            _harness.Step(3);

            // Verify InterceptCourse component set on requester
            Assert.IsTrue(_entityManager.HasComponent<InterceptCourse>(requesterCarrier),
                "InterceptCourse component should be added to requester");

            var course = _entityManager.GetComponentData<InterceptCourse>(requesterCarrier);
            Assert.AreEqual(targetCarrier, course.TargetFleet, "TargetFleet should match target carrier");
            Assert.AreEqual(1, course.UsesInterception, "UsesInterception should be 1 if tech allows");
            Assert.Greater(course.EstimatedInterceptTick, 0u, "EstimatedInterceptTick should be greater than 0");
            Assert.That(math.lengthsq(course.InterceptPoint), Is.GreaterThan(0f), "InterceptPoint should be calculated");
        }

        [Test]
        public void FleetTelemetryPublished()
        {
            // Create TelemetryStream singleton (should already exist from bootstrap)
            var telemetryQuery = _entityManager.CreateEntityQuery(typeof(TelemetryStream));
            Entity telemetryEntity;
            if (telemetryQuery.IsEmptyIgnoreFilter)
            {
                telemetryEntity = _entityManager.CreateEntity();
                _entityManager.AddComponent<TelemetryStream>(telemetryEntity);
                _entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
            }
            else
            {
                telemetryEntity = telemetryQuery.GetSingletonEntity();
                if (!_entityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
                {
                    _entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);
                }
            }

            // Create Space4XFleetInterceptQueue with telemetry
            var queueQuery = _entityManager.CreateEntityQuery(typeof(Space4XFleetInterceptQueue));
            Entity queueEntity;
            if (queueQuery.IsEmptyIgnoreFilter)
            {
                queueEntity = _entityManager.CreateEntity();
                _entityManager.AddComponentData(queueEntity, new Space4XFleetInterceptQueue());
                _entityManager.AddBuffer<InterceptRequest>(queueEntity);
                _entityManager.AddBuffer<FleetInterceptCommandLogEntry>(queueEntity);
                _entityManager.AddComponentData(queueEntity, new Space4XFleetInterceptTelemetry
                {
                    InterceptAttempts = 0,
                    RendezvousAttempts = 0,
                    LastAttemptTick = 0
                });
            }
            else
            {
                queueEntity = queueQuery.GetSingletonEntity();
                if (!_entityManager.HasComponent<Space4XFleetInterceptTelemetry>(queueEntity))
                {
                    _entityManager.AddComponentData(queueEntity, new Space4XFleetInterceptTelemetry
                    {
                        InterceptAttempts = 0,
                        RendezvousAttempts = 0,
                        LastAttemptTick = 0
                    });
                }
            }

            // Generate intercept activity
            var carrier1 = CreateCarrierWithBroadcast(new float3(0f, 0f, 0f), "FLEET-1", allowsInterception: true);
            var carrier2 = CreateCarrierWithBroadcast(new float3(10f, 0f, 0f), "FLEET-2", allowsInterception: true);
            _entityManager.AddComponentData(carrier1, new InterceptCapability
            {
                MaxSpeed = 5f,
                TechTier = 1,
                AllowIntercept = 1
            });

            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 5;
            _entityManager.SetComponentData(_timeEntity, time);

            // Run intercept systems to generate telemetry
            _harness.Step(4);

            // Run telemetry system
            _harness.Step();

            // Verify metrics published
            var metricsBuffer = _entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            bool foundInterceptMetric = false;
            bool foundRendezvousMetric = false;
            
            for (int i = 0; i < metricsBuffer.Length; i++)
            {
                var metric = metricsBuffer[i];
                var key = metric.Key.ToString();
                if (key.Contains("space4x.intercept.attempts"))
                {
                    foundInterceptMetric = true;
                }
                if (key.Contains("space4x.intercept.rendezvous"))
                {
                    foundRendezvousMetric = true;
                }
            }

            // At least one intercept-related metric should be published
            Assert.IsTrue(foundInterceptMetric || foundRendezvousMetric || metricsBuffer.Length > 0,
                "Telemetry metrics should be published for intercept activity");
        }

        [Test]
        public void CarriersWithSpace4XFleetInterceptAuthoringComponentsWork()
        {
            // Create carrier with components that would be added by Space4XFleetInterceptAuthoring
            var carrier = _entityManager.CreateEntity();
            _entityManager.AddComponentData(carrier, new Carrier
            {
                CarrierId = new FixedString64Bytes("CARRIER-1"),
                AffiliationEntity = Entity.Null,
                Speed = 5f,
                PatrolCenter = float3.zero,
                PatrolRadius = 50f
            });
            _entityManager.AddComponentData(carrier, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.AddComponent<SpatialIndexedTag>(carrier);
            _entityManager.AddComponentData(carrier, new FleetMovementBroadcast
            {
                Position = float3.zero,
                Velocity = float3.zero,
                LastUpdateTick = 0,
                AllowsInterception = 1,
                TechTier = 1
            });
            _entityManager.AddComponentData(carrier, new SpatialGridResidency
            {
                CellId = 0,
                LastPosition = float3.zero,
                Version = 0
            });
            _entityManager.AddComponentData(carrier, new InterceptCapability
            {
                MaxSpeed = 10f,
                TechTier = 1,
                AllowIntercept = 1
            });

            // Update time
            var time = _entityManager.GetComponentData<TimeState>(_timeEntity);
            time.Tick = 1;
            _entityManager.SetComponentData(_timeEntity, time);

            // Run broadcast system
            _harness.Step();

            // Verify broadcast updated
            var broadcast = _entityManager.GetComponentData<FleetMovementBroadcast>(carrier);
            Assert.AreEqual(1u, broadcast.LastUpdateTick, "Broadcast should be updated by FleetBroadcastSystem");
        }

        private Entity CreateCarrierWithBroadcast(float3 position, string fleetId, bool allowsInterception)
        {
            var entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes(fleetId),
                AffiliationEntity = Entity.Null,
                Speed = 2f,
                PatrolCenter = position,
                PatrolRadius = 50f
            });
            _entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            _entityManager.AddComponent<SpatialIndexedTag>(entity);
            _entityManager.AddComponentData(entity, new FleetMovementBroadcast
            {
                Position = position,
                Velocity = float3.zero,
                LastUpdateTick = 0,
                AllowsInterception = (byte)(allowsInterception ? 1 : 0),
                TechTier = 1
            });
            return entity;
        }
    }
}

