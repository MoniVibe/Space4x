using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests
{
    public class Space4XAffiliationComplianceSystemTests
    {
        private World _world;
        private EntityManager _entityManager;
        private Entity _snapshotEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XAffiliationComplianceSystemTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);

            _snapshotEntity = _entityManager.CreateEntity(typeof(Space4XRegistrySnapshot));
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void ComplianceProducesBreachScaledByLoyalty()
        {
            var doctrine = CreateDoctrine(0.5f, 1f, 0.5f, 1f, 0.4f, 0.1f);
            var entity = CreateCrew(doctrine, loyalty:0.5f, warAxis:0f, AlignmentTriplet.FromFloats(0f, 0f, 0f));

            var complianceSystem = _world.GetOrCreateSystem<Space4XAffiliationComplianceSystem>();
            complianceSystem.Update(_world.Unmanaged);

            var breaches = _entityManager.GetBuffer<ComplianceBreach>(entity);
            Assert.AreEqual(1, breaches.Length);
            Assert.AreEqual(ComplianceBreachType.Independence, breaches[0].Type);
            Assert.AreEqual(0.675f, (float)breaches[0].Severity, 1e-3f);

            var tickets = _entityManager.GetBuffer<ComplianceTicket>(entity);
            Assert.AreEqual(1, tickets.Length);
            Assert.AreEqual(breaches[0].Type, tickets[0].Type);
            Assert.AreEqual((float)breaches[0].Severity, (float)tickets[0].Severity, 1e-3f);
            Assert.AreNotEqual(0u, tickets[0].Tick);
        }

        [Test]
        public void SpyDeviationRaisesSuspicionAndPublishesTelemetry()
        {
            var doctrine = CreateDoctrine(0.5f, 1f, 0.5f, 1f, 0.3f, 0.4f);
            var entity = CreateCrew(doctrine, loyalty:0f, warAxis:0f, AlignmentTriplet.FromFloats(0f, 0f, 0f));
            _entityManager.AddComponent<SpyRole>(entity);

            var telemetryEntity = _entityManager.CreateEntity(typeof(TelemetryStream));
            _entityManager.AddBuffer<TelemetryMetric>(telemetryEntity);

            var complianceSystem = _world.GetOrCreateSystem<Space4XAffiliationComplianceSystem>();
            complianceSystem.Update(_world.Unmanaged);

            var suspicion = _entityManager.GetComponentData<SuspicionScore>(entity);
            Assert.AreEqual(0.4f, (float)suspicion.Value, 1e-3f);
            Assert.IsFalse(_entityManager.HasBuffer<ComplianceBreach>(entity));

            var telemetrySystem = _world.GetOrCreateSystem<Space4XComplianceTelemetrySystem>();
            telemetrySystem.Update(_world.Unmanaged);

            var snapshot = _entityManager.GetComponentData<Space4XRegistrySnapshot>(_snapshotEntity);
            Assert.AreEqual(0, snapshot.ComplianceBreachCount);
            Assert.AreEqual(0.4f, snapshot.ComplianceAverageSuspicion, 1e-3f);
            Assert.AreEqual(0.4f, snapshot.ComplianceAverageSpySuspicion, 1e-3f);
            Assert.AreEqual(0.4f, snapshot.ComplianceMaxSuspicion, 1e-3f);
            Assert.AreEqual(1, snapshot.ComplianceSuspicionAlertCount);

            var metrics = _entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            AssertMetric(metrics, new FixedString64Bytes("space4x.compliance.breaches"), 0f);
            AssertMetric(metrics, new FixedString64Bytes("space4x.compliance.suspicion.mean"), 0.4f);
            AssertMetric(metrics, new FixedString64Bytes("space4x.compliance.suspicion.max"), 0.4f);
            AssertMetric(metrics, new FixedString64Bytes("space4x.compliance.suspicion.alerts"), 1f);
        }

        [Test]
        public void CrewAggregationBuildsTopOutlooksAndPresence()
        {
            var entity = _entityManager.CreateEntity(typeof(AlignmentTriplet), typeof(RaceId), typeof(CultureId));
            _entityManager.SetComponentData(entity, new RaceId { Value = 7 });
            _entityManager.SetComponentData(entity, new CultureId { Value = 3 });

            var outlooks = _entityManager.AddBuffer<OutlookEntry>(entity);
            outlooks.Add(new OutlookEntry { OutlookId = OutlookId.Opportunist, Weight = (half)0.5f });
            outlooks.Add(new OutlookEntry { OutlookId = OutlookId.Fanatic, Weight = (half)(-0.8f) });
            outlooks.Add(new OutlookEntry { OutlookId = OutlookId.Mutinous, Weight = (half)0.3f });
            outlooks.Add(new OutlookEntry { OutlookId = OutlookId.Loyalist, Weight = (half)0.6f });

            var aggregation = _world.GetOrCreateSystem<Space4XCrewAggregationSystem>();
            aggregation.Update(_world.Unmanaged);

            var top = _entityManager.GetBuffer<TopOutlook>(entity);
            Assert.AreEqual(3, top.Length);
            Assert.AreEqual(OutlookId.Fanatic, top[0].OutlookId);
            Assert.AreEqual(OutlookId.Loyalist, top[1].OutlookId);
            Assert.AreEqual(OutlookId.Opportunist, top[2].OutlookId);

            var races = _entityManager.GetBuffer<RacePresence>(entity);
            Assert.AreEqual(1, races.Length);
            Assert.AreEqual(7, races[0].RaceId);

            var cultures = _entityManager.GetBuffer<CulturePresence>(entity);
            Assert.AreEqual(1, cultures.Length);
            Assert.AreEqual(3, cultures[0].CultureId);
        }

        [Test]
        public void ComplianceTicketsAreQueuedDeterministically()
        {
            var queueEntity = _entityManager.CreateEntity(typeof(ComplianceTicketQueue));
            _entityManager.AddBuffer<ComplianceTicketQueueEntry>(queueEntity);

            var first = CreateCrew(Entity.Null, loyalty:1f, warAxis:0f, AlignmentTriplet.FromFloats(-1f, 0f, 0f));
            var second = CreateCrew(Entity.Null, loyalty:1f, warAxis:1f, AlignmentTriplet.FromFloats(-1f, 0f, 0f));

            var firstTickets = _entityManager.AddBuffer<ComplianceTicket>(first);
            firstTickets.Add(new ComplianceTicket
            {
                Affiliation = Entity.Null,
                Type = ComplianceBreachType.Mutiny,
                Severity = (half)0.9f,
                Tick = 5
            });

            var secondTickets = _entityManager.AddBuffer<ComplianceTicket>(second);
            secondTickets.Add(new ComplianceTicket
            {
                Affiliation = Entity.Null,
                Type = ComplianceBreachType.Desertion,
                Severity = (half)0.5f,
                Tick = 4
            });

            var queueSystem = _world.GetOrCreateSystem<Space4XComplianceTicketQueueSystem>();
            queueSystem.Update(_world.Unmanaged);

            var queue = _entityManager.GetBuffer<ComplianceTicketQueueEntry>(queueEntity);
            Assert.AreEqual(2, queue.Length);
            Assert.AreEqual(ComplianceBreachType.Desertion, queue[0].Type);
            Assert.AreEqual(ComplianceBreachType.Mutiny, queue[1].Type);
            Assert.IsTrue(queue[0].Tick <= queue[1].Tick);

            var inboxSystem = _world.GetOrCreateSystem<Space4XCompliancePlannerBridgeSystem>();
            inboxSystem.Update(_world.Unmanaged);

            var bridgeInbox = _world.EntityManager.GetBuffer<CompliancePlannerTicket>(_world.EntityManager.CreateEntityQuery(typeof(CompliancePlannerInbox)).GetSingletonEntity());
            Assert.AreEqual(2, bridgeInbox.Length);
            Assert.AreEqual(queue[0].Type, bridgeInbox[0].Type);
            Assert.AreEqual(queue[1].Type, bridgeInbox[1].Type);
        }

        private Entity CreateDoctrine(float lawMin, float lawMax, float warMin, float warMax, float chaosThreshold, float suspicionGain)
        {
            var entity = _entityManager.CreateEntity(typeof(DoctrineProfile));
            _entityManager.AddBuffer<DoctrineAxisExpectation>(entity);
            _entityManager.AddBuffer<DoctrineOutlookExpectation>(entity);

            _entityManager.SetComponentData(entity, new DoctrineProfile
            {
                AlignmentWindow = new AlignmentWindow
                {
                    LawMin = (half)lawMin,
                    LawMax = (half)lawMax,
                    GoodMin = (half)(-1f),
                    GoodMax = (half)1f,
                    IntegrityMin = (half)(-1f),
                    IntegrityMax = (half)1f
                },
                AxisTolerance = (half)0f,
                OutlookTolerance = (half)0f,
                ChaosMutinyThreshold = (half)chaosThreshold,
                LawfulContractFloor = (half)0.2f,
                SuspicionGain = (half)suspicionGain
            });

            var expectations = _entityManager.GetBuffer<DoctrineAxisExpectation>(entity);
            expectations.Add(new DoctrineAxisExpectation
            {
                Axis = EthicAxisId.War,
                Min = (half)warMin,
                Max = (half)warMax
            });

            return entity;
        }

        private Entity CreateCrew(Entity affiliation, float loyalty, float warAxis, AlignmentTriplet alignment)
        {
            var entity = _entityManager.CreateEntity(typeof(AlignmentTriplet));
            _entityManager.SetComponentData(entity, alignment);

            var affiliations = _entityManager.AddBuffer<AffiliationTag>(entity);
            affiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Fleet,
                Target = affiliation,
                Loyalty = (half)loyalty
            });

            var axisBuffer = _entityManager.AddBuffer<EthicAxisValue>(entity);
            axisBuffer.Add(new EthicAxisValue
            {
                Axis = EthicAxisId.War,
                Value = (half)warAxis
            });

            return entity;
        }

        private static void AssertMetric(DynamicBuffer<TelemetryMetric> metrics, FixedString64Bytes key, float expected)
        {
            foreach (var metric in metrics)
            {
                if (metric.Key.Equals(key))
                {
                    Assert.AreEqual(expected, metric.Value, 1e-3f);
                    return;
                }
            }

            Assert.Fail($"Metric '{key}' was not written.");
        }
    }
}
