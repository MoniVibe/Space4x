#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public class Space4XStationServiceSystemsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XStationServiceSystemsTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
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
        public void StationServiceBootstrapSeedsProfileAndAccessPolicy()
        {
            var station = _entityManager.CreateEntity(
                typeof(StationId),
                typeof(DockingCapacity),
                typeof(Space4XMarket),
                typeof(RefitFacilityTag));
            _entityManager.SetComponentData(station, new StationId { Id = new FixedString64Bytes("station.trade.01") });
            _entityManager.SetComponentData(station, DockingCapacity.LightCarrier);
            _entityManager.SetComponentData(station, new Space4XMarket
            {
                LocationType = MarketLocationType.Station,
                Size = MarketSize.Medium,
                TaxRate = (half)0.05f,
                BlackMarketAccess = (half)0f,
                MarketHealth = (half)1f,
                IsEmbargoed = 0,
                OwnerFactionId = 0,
                LastUpdateTick = 0
            });

            var system = _world.GetOrCreateSystem<Space4XStationServiceBootstrapSystem>();
            system.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasComponent<Space4XStationServiceProfile>(station));
            Assert.IsTrue(_entityManager.HasComponent<Space4XStationAccessPolicy>(station));

            var profile = _entityManager.GetComponentData<Space4XStationServiceProfile>(station);
            Assert.AreEqual(Space4XStationSpecialization.TradeHub, profile.Specialization);
            Assert.IsTrue((profile.Services & Space4XStationServiceFlags.Docking) != 0);
            Assert.IsTrue((profile.Services & Space4XStationServiceFlags.TradeMarket) != 0);
            Assert.IsTrue((profile.Services & Space4XStationServiceFlags.Shipyard) != 0);
            Assert.IsTrue((profile.Services & Space4XStationServiceFlags.Refit) != 0);

            var access = _entityManager.GetComponentData<Space4XStationAccessPolicy>(station);
            Assert.LessOrEqual(access.MinStandingForDock, 0.15f);
            Assert.Greater(access.WarningRadiusMeters, access.NoFlyRadiusMeters);
        }

        [Test]
        public void DockingSystemRejectsStationDockingBelowStandingGate()
        {
            var stationFaction = _entityManager.CreateEntity(typeof(Space4XFaction));
            _entityManager.SetComponentData(stationFaction, Space4XFaction.Guild(200));

            var station = _entityManager.CreateEntity(
                typeof(StationId),
                typeof(DockingCapacity),
                typeof(Space4XStationAccessPolicy));
            _entityManager.SetComponentData(station, new StationId { Id = new FixedString64Bytes("station.military.01") });
            _entityManager.SetComponentData(station, DockingCapacity.LightCarrier);
            _entityManager.SetComponentData(station, new Space4XStationAccessPolicy
            {
                MinStandingForApproach = 0.6f,
                MinStandingForDock = 0.8f,
                WarningRadiusMeters = 150f,
                NoFlyRadiusMeters = 80f,
                EnforceNoFlyZone = 1,
                DenyDockingWithoutStanding = 1
            });

            var stationAffiliations = _entityManager.AddBuffer<AffiliationTag>(station);
            stationAffiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = stationFaction,
                Loyalty = (half)1f
            });

            var actorFaction = _entityManager.CreateEntity(typeof(Space4XFaction));
            _entityManager.SetComponentData(actorFaction, Space4XFaction.Empire(100, FactionOutlook.None));
            var relations = _entityManager.AddBuffer<FactionRelationEntry>(actorFaction);
            relations.Add(new FactionRelationEntry
            {
                Relation = new FactionRelation
                {
                    OtherFaction = stationFaction,
                    OtherFactionId = 200,
                    Score = -60,
                    Trust = (half)0f,
                    Fear = (half)0f,
                    Respect = (half)0f,
                    TradeVolume = 0f,
                    RecentCombats = 0,
                    LastInteractionTick = 0
                }
            });

            var vessel = _entityManager.CreateEntity(typeof(Carrier), typeof(DockingRequest));
            _entityManager.SetComponentData(vessel, new Carrier
            {
                CarrierId = new FixedString64Bytes("actor.ship.01"),
                AffiliationEntity = actorFaction
            });
            _entityManager.SetComponentData(vessel, new DockingRequest
            {
                TargetCarrier = station,
                RequiredSlot = DockingSlotType.Utility,
                RequestTick = 0,
                Priority = 0
            });

            var dockingSystem = _world.GetOrCreateSystem<Space4XDockingSystem>();
            dockingSystem.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasComponent<DockingRequest>(vessel));
            Assert.IsFalse(_entityManager.HasComponent<DockedTag>(vessel));
            var capacity = _entityManager.GetComponentData<DockingCapacity>(station);
            Assert.AreEqual(0, capacity.TotalDocked);
        }

        [Test]
        public void DockingPolicyBootstrapSeedsQueuePolicyForStations()
        {
            var station = _entityManager.CreateEntity(typeof(StationId), typeof(DockingCapacity));
            _entityManager.SetComponentData(station, new StationId { Id = new FixedString64Bytes("station.queue.bootstrap") });
            _entityManager.SetComponentData(station, DockingCapacity.LightCarrier);

            var bootstrap = _world.GetOrCreateSystem<Space4XDockingPolicyBootstrapSystem>();
            bootstrap.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasComponent<DockingQueuePolicy>(station));
            Assert.IsTrue(_entityManager.HasComponent<DockingQueueState>(station));
            var queuePolicy = _entityManager.GetComponentData<DockingQueuePolicy>(station);
            Assert.AreEqual(2, queuePolicy.MaxProcessedPerTick);
        }

        [Test]
        public void DockingQueueThroughputCapsSuccessfulDocksPerTick()
        {
            var station = _entityManager.CreateEntity(
                typeof(StationId),
                typeof(DockingCapacity),
                typeof(DockingQueuePolicy),
                typeof(DockingQueueState));
            _entityManager.SetComponentData(station, new StationId { Id = new FixedString64Bytes("station.queue.cap") });
            _entityManager.SetComponentData(station, new DockingCapacity
            {
                MaxSmallCraft = 0,
                MaxMediumCraft = 0,
                MaxLargeCraft = 0,
                MaxExternalMooring = 0,
                MaxUtility = 4
            });
            _entityManager.SetComponentData(station, new DockingQueuePolicy
            {
                MaxProcessedPerTick = 1
            });
            _entityManager.SetComponentData(station, new DockingQueueState
            {
                LastTick = 0,
                PendingRequests = 0,
                ProcessedRequests = 0
            });
            _entityManager.AddBuffer<DockedEntity>(station);

            var vesselA = _entityManager.CreateEntity(typeof(DockingRequest));
            _entityManager.SetComponentData(vesselA, new DockingRequest
            {
                TargetCarrier = station,
                RequiredSlot = DockingSlotType.Utility,
                RequestTick = 1,
                Priority = 0
            });

            var vesselB = _entityManager.CreateEntity(typeof(DockingRequest));
            _entityManager.SetComponentData(vesselB, new DockingRequest
            {
                TargetCarrier = station,
                RequiredSlot = DockingSlotType.Utility,
                RequestTick = 2,
                Priority = 0
            });

            var dockingSystem = _world.GetOrCreateSystem<Space4XDockingSystem>();
            dockingSystem.Update(_world.Unmanaged);

            var dockedCount = (_entityManager.HasComponent<DockedTag>(vesselA) ? 1 : 0) +
                              (_entityManager.HasComponent<DockedTag>(vesselB) ? 1 : 0);
            var pendingCount = (_entityManager.HasComponent<DockingRequest>(vesselA) ? 1 : 0) +
                               (_entityManager.HasComponent<DockingRequest>(vesselB) ? 1 : 0);

            Assert.AreEqual(1, dockedCount);
            Assert.AreEqual(1, pendingCount);

            var queueState = _entityManager.GetComponentData<DockingQueueState>(station);
            Assert.AreEqual(2, queueState.PendingRequests);
            Assert.AreEqual(1, queueState.ProcessedRequests);
        }

        [Test]
        public void NoFlyBoundaryFlagsViolationAndClearsDeniedDockingRequest()
        {
            var stationFaction = _entityManager.CreateEntity(typeof(Space4XFaction));
            _entityManager.SetComponentData(stationFaction, Space4XFaction.Guild(300));

            var station = _entityManager.CreateEntity(
                typeof(StationId),
                typeof(LocalTransform),
                typeof(Space4XStationAccessPolicy));
            _entityManager.SetComponentData(station, new StationId { Id = new FixedString64Bytes("station.nofly.01") });
            _entityManager.SetComponentData(station, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            _entityManager.SetComponentData(station, new Space4XStationAccessPolicy
            {
                MinStandingForApproach = 0.7f,
                MinStandingForDock = 0.8f,
                WarningRadiusMeters = 100f,
                NoFlyRadiusMeters = 50f,
                EnforceNoFlyZone = 1,
                DenyDockingWithoutStanding = 1
            });
            var stationAffiliations = _entityManager.AddBuffer<AffiliationTag>(station);
            stationAffiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = stationFaction,
                Loyalty = (half)1f
            });

            var actorFaction = _entityManager.CreateEntity(typeof(Space4XFaction));
            _entityManager.SetComponentData(actorFaction, Space4XFaction.Empire(100, FactionOutlook.None));
            var relations = _entityManager.AddBuffer<FactionRelationEntry>(actorFaction);
            relations.Add(new FactionRelationEntry
            {
                Relation = new FactionRelation
                {
                    OtherFaction = stationFaction,
                    OtherFactionId = 300,
                    Score = -80
                }
            });

            var vessel = _entityManager.CreateEntity(
                typeof(Carrier),
                typeof(LocalTransform),
                typeof(DockingRequest));
            _entityManager.SetComponentData(vessel, new Carrier
            {
                CarrierId = new FixedString64Bytes("actor.ship.02"),
                AffiliationEntity = actorFaction
            });
            _entityManager.SetComponentData(vessel, LocalTransform.FromPositionRotationScale(new float3(10f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.SetComponentData(vessel, new DockingRequest
            {
                TargetCarrier = station,
                RequiredSlot = DockingSlotType.Utility,
                RequestTick = 0,
                Priority = 0
            });

            var boundarySystem = _world.GetOrCreateSystem<Space4XStationNoFlyBoundarySystem>();
            boundarySystem.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasComponent<Space4XStationNoFlyViolation>(vessel));
            var violation = _entityManager.GetComponentData<Space4XStationNoFlyViolation>(vessel);
            Assert.AreEqual(station, violation.Station);
            Assert.AreEqual(1, violation.InsideNoFly);
            Assert.Greater(violation.Severity, 0.9f);
            Assert.IsFalse(_entityManager.HasComponent<DockingRequest>(vessel));
        }

        [Test]
        public void StationServiceBootstrapUsesCatalogOverridesWhenPresent()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<StationCatalogBlob>();
            var stations = builder.Allocate(ref root.Stations, 1);
            stations[0] = new StationSpec
            {
                Id = new FixedString64Bytes("station.catalog.override"),
                HasRefitFacility = false,
                FacilityZoneRadius = 0f,
                PresentationArchetype = new FixedString64Bytes("station"),
                DefaultStyleTokens = new StyleTokens { Palette = 0, Roughness = 128, Pattern = 0 },
                Specialization = Space4XStationSpecialization.Military,
                Services = Space4XStationServiceFlags.Docking | Space4XStationServiceFlags.Shipyard | Space4XStationServiceFlags.MissionBoard,
                Tier = 5,
                ServiceScale = 2.5f,
                HasServiceProfileOverride = 1,
                MinStandingForApproach = 0.4f,
                MinStandingForDock = 0.7f,
                WarningRadiusMeters = 240f,
                NoFlyRadiusMeters = 140f,
                EnforceNoFlyZone = 1,
                DenyDockingWithoutStanding = 1,
                HasAccessPolicyOverride = 1
            };

            var blob = builder.CreateBlobAssetReference<StationCatalogBlob>(Allocator.Persistent);
            var catalogEntity = _entityManager.CreateEntity(typeof(StationCatalogSingleton));
            _entityManager.SetComponentData(catalogEntity, new StationCatalogSingleton { Catalog = blob });

            var station = _entityManager.CreateEntity(typeof(StationId), typeof(DockingCapacity));
            _entityManager.SetComponentData(station, new StationId { Id = new FixedString64Bytes("station.catalog.override") });
            _entityManager.SetComponentData(station, DockingCapacity.LightCarrier);

            var system = _world.GetOrCreateSystem<Space4XStationServiceBootstrapSystem>();
            system.Update(_world.Unmanaged);

            var profile = _entityManager.GetComponentData<Space4XStationServiceProfile>(station);
            Assert.AreEqual(Space4XStationSpecialization.Military, profile.Specialization);
            Assert.AreEqual(Space4XStationServiceFlags.Docking | Space4XStationServiceFlags.Shipyard | Space4XStationServiceFlags.MissionBoard, profile.Services);
            Assert.AreEqual(5, profile.Tier);
            Assert.AreEqual(2.5f, profile.ServiceScale, 1e-4f);

            var access = _entityManager.GetComponentData<Space4XStationAccessPolicy>(station);
            Assert.AreEqual(0.4f, access.MinStandingForApproach, 1e-4f);
            Assert.AreEqual(0.7f, access.MinStandingForDock, 1e-4f);
            Assert.AreEqual(240f, access.WarningRadiusMeters, 1e-4f);
            Assert.AreEqual(140f, access.NoFlyRadiusMeters, 1e-4f);
            Assert.AreEqual(1, access.EnforceNoFlyZone);
            Assert.AreEqual(1, access.DenyDockingWithoutStanding);
        }

        [Test]
        public void StationServiceBootstrapUsesEntityOverridesBeforeCatalog()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<StationCatalogBlob>();
            var stations = builder.Allocate(ref root.Stations, 1);
            stations[0] = new StationSpec
            {
                Id = new FixedString64Bytes("station.entity.override"),
                Specialization = Space4XStationSpecialization.TradeHub,
                Services = Space4XStationServiceFlags.TradeMarket,
                Tier = 2,
                ServiceScale = 1.5f,
                HasServiceProfileOverride = 1,
                MinStandingForApproach = 0f,
                MinStandingForDock = 0.05f,
                WarningRadiusMeters = 90f,
                NoFlyRadiusMeters = 45f,
                EnforceNoFlyZone = 1,
                DenyDockingWithoutStanding = 0,
                HasAccessPolicyOverride = 1
            };

            var blob = builder.CreateBlobAssetReference<StationCatalogBlob>(Allocator.Persistent);
            var catalogEntity = _entityManager.CreateEntity(typeof(StationCatalogSingleton));
            _entityManager.SetComponentData(catalogEntity, new StationCatalogSingleton { Catalog = blob });

            var station = _entityManager.CreateEntity(
                typeof(StationId),
                typeof(DockingCapacity),
                typeof(Space4XStationServiceProfileOverride),
                typeof(Space4XStationAccessPolicyOverride));
            _entityManager.SetComponentData(station, new StationId { Id = new FixedString64Bytes("station.entity.override") });
            _entityManager.SetComponentData(station, DockingCapacity.LightCarrier);
            _entityManager.SetComponentData(station, new Space4XStationServiceProfileOverride
            {
                Enabled = 1,
                Specialization = Space4XStationSpecialization.Shipyard,
                Services = Space4XStationServiceFlags.Docking | Space4XStationServiceFlags.Shipyard | Space4XStationServiceFlags.Refit,
                Tier = 4,
                ServiceScale = 2f
            });
            _entityManager.SetComponentData(station, new Space4XStationAccessPolicyOverride
            {
                Enabled = 1,
                MinStandingForApproach = 0.2f,
                MinStandingForDock = 0.3f,
                WarningRadiusMeters = 180f,
                NoFlyRadiusMeters = 100f,
                EnforceNoFlyZone = 1,
                DenyDockingWithoutStanding = 1
            });

            var system = _world.GetOrCreateSystem<Space4XStationServiceBootstrapSystem>();
            system.Update(_world.Unmanaged);

            var profile = _entityManager.GetComponentData<Space4XStationServiceProfile>(station);
            Assert.AreEqual(Space4XStationSpecialization.Shipyard, profile.Specialization);
            Assert.AreEqual(Space4XStationServiceFlags.Docking | Space4XStationServiceFlags.Shipyard | Space4XStationServiceFlags.Refit, profile.Services);
            Assert.AreEqual(4, profile.Tier);
            Assert.AreEqual(2f, profile.ServiceScale, 1e-4f);

            var access = _entityManager.GetComponentData<Space4XStationAccessPolicy>(station);
            Assert.AreEqual(0.2f, access.MinStandingForApproach, 1e-4f);
            Assert.AreEqual(0.3f, access.MinStandingForDock, 1e-4f);
            Assert.AreEqual(180f, access.WarningRadiusMeters, 1e-4f);
            Assert.AreEqual(100f, access.NoFlyRadiusMeters, 1e-4f);
            Assert.AreEqual(1, access.DenyDockingWithoutStanding);
        }
    }
}
#endif
