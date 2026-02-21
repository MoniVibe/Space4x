#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests
{
    using PDCarrierModuleSlot = PureDOTS.Runtime.Ships.CarrierModuleSlot;
    using PDShipModule = PureDOTS.Runtime.Ships.ShipModule;
    using PDCrewMember = PureDOTS.Runtime.Platform.PlatformCrewMember;

    public class Space4XLeisureSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XLeisureSystemTests");
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
        public void LeisureFacility_ReplenishesNeeds_AndAddsPositiveMoraleModifier()
        {
            var ship = CreateShipBase();
            _entityManager.SetComponentData(ship, new CrewCapacity
            {
                MaxCrew = 80,
                CurrentCrew = 24,
                CriticalMax = 120
            });
            _entityManager.SetComponentData(ship, AlignmentTriplet.FromFloats(0.7f, 0.6f, 0.8f));
            _entityManager.SetComponentData(ship, new LeisureNeedState
            {
                Entertainment = (half)0.7f,
                Comfort = (half)0.7f,
                Social = (half)0.7f,
                Nourishment = (half)0.8f,
                EntertainmentDecay = (half)0.01f,
                ComfortDecay = (half)0.01f,
                SocialDecay = (half)0.01f,
                NourishmentDecay = (half)0.01f,
                LastUpdateTick = 0u
            });

            var module = _entityManager.CreateEntity(typeof(PDShipModule), typeof(LeisureFacilityLimb));
            _entityManager.SetComponentData(module, new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.Museum,
                HousingCapacity = 64f,
                EntertainmentRate = 0.18f,
                ComfortRate = 0.06f,
                SocialRate = 0.04f,
                NourishmentRate = 0.02f,
                EspionageRisk = 0.01f,
                PoisonRisk = 0.01f,
                AssassinationRisk = 0.01f,
                Illicitness = 0f,
                BriberyPressure = 0f
            });
            var limbStates = _entityManager.AddBuffer<ModuleLimbState>(module);
            limbStates.Add(new ModuleLimbState
            {
                LimbId = ModuleLimbId.StructuralFrame,
                Family = ModuleLimbFamily.Structural,
                Integrity = 1f,
                Exposure = 1f
            });

            var slots = _entityManager.AddBuffer<PDCarrierModuleSlot>(ship);
            slots.Add(new PDCarrierModuleSlot { InstalledModule = module });

            var before = _entityManager.GetComponentData<LeisureNeedState>(ship);

            var system = _world.GetOrCreateSystem<Space4XLeisureNeedSystem>();
            system.Update(_world.Unmanaged);

            var after = _entityManager.GetComponentData<LeisureNeedState>(ship);
            Assert.Greater((float)after.Entertainment, (float)before.Entertainment, "Entertainment need should recover.");

            var modifiers = _entityManager.GetBuffer<MoraleModifier>(ship);
            Assert.IsTrue(TryGetModifier(modifiers, MoraleModifierSource.Leisure, out var leisure));
            Assert.Greater((float)leisure.Strength, 0f, "Leisure modifier should be positive with adequate housing/facilities.");
        }

        [Test]
        public void LeisureWithoutHousing_OvercrowdingProducesNegativeMoraleModifier()
        {
            var ship = CreateShipBase();
            _entityManager.SetComponentData(ship, new CrewCapacity
            {
                MaxCrew = 40,
                CurrentCrew = 120,
                CriticalMax = 160
            });
            _entityManager.SetComponentData(ship, new LeisureNeedState
            {
                Entertainment = (half)1f,
                Comfort = (half)1f,
                Social = (half)1f,
                Nourishment = (half)1f,
                EntertainmentDecay = (half)0f,
                ComfortDecay = (half)0f,
                SocialDecay = (half)0f,
                NourishmentDecay = (half)0f,
                LastUpdateTick = 0u
            });

            var system = _world.GetOrCreateSystem<Space4XLeisureNeedSystem>();
            system.Update(_world.Unmanaged);

            var modifiers = _entityManager.GetBuffer<MoraleModifier>(ship);
            Assert.IsTrue(TryGetModifier(modifiers, MoraleModifierSource.Leisure, out var leisure));
            Assert.Less((float)leisure.Strength, 0f, "Overcrowding with no housing facilities should penalize morale.");

            var aggregate = _entityManager.GetComponentData<LeisureFacilityAggregate>(ship);
            Assert.GreaterOrEqual(aggregate.Overcrowding, 0.99f, "Overcrowding should saturate when crew has no housing.");
        }

        [Test]
        public void HighRiskLeisureFacility_EmitsIncident_AndEspionagePenalty()
        {
            var ship = CreateShipBase();
            _entityManager.SetComponentData(ship, new CrewCapacity
            {
                MaxCrew = 60,
                CurrentCrew = 20,
                CriticalMax = 90
            });
            _entityManager.SetComponentData(ship, AlignmentTriplet.FromFloats(-1f, -1f, -1f));
            _entityManager.SetComponentData(ship, new LeisureSecurityPolicy
            {
                CounterIntelLevel = (half)0f,
                FoodSafetyLevel = (half)0f,
                InternalSecurityLevel = (half)0f,
                BriberyBudget = (half)0f
            });
            _entityManager.AddComponentData(ship, new SuspicionScore { Value = (half)0f });

            var module = _entityManager.CreateEntity(typeof(PDShipModule), typeof(LeisureFacilityLimb));
            _entityManager.SetComponentData(module, new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.ContrabandDen,
                HousingCapacity = 24f,
                EntertainmentRate = 0.12f,
                ComfortRate = 0.03f,
                SocialRate = 0.08f,
                NourishmentRate = 0.04f,
                EspionageRisk = 1f,
                PoisonRisk = 1f,
                AssassinationRisk = 1f,
                Illicitness = 1f,
                BriberyPressure = 1f
            });

            var slots = _entityManager.AddBuffer<PDCarrierModuleSlot>(ship);
            slots.Add(new PDCarrierModuleSlot { InstalledModule = module });

            var system = _world.GetOrCreateSystem<Space4XLeisureNeedSystem>();
            system.Update(_world.Unmanaged);

            var incidents = _entityManager.GetBuffer<LeisureIncidentEvent>(ship);
            Assert.Greater(incidents.Length, 0, "High-risk illicit facility should emit an incident event.");

            var modifiers = _entityManager.GetBuffer<MoraleModifier>(ship);
            Assert.IsTrue(TryGetModifier(modifiers, MoraleModifierSource.Espionage, out var espionage));
            Assert.Less((float)espionage.Strength, 0f, "Incident should apply a negative espionage morale modifier.");

            var suspicion = _entityManager.GetComponentData<SuspicionScore>(ship);
            Assert.Greater((float)suspicion.Value, 0f, "Incident should raise suspicion.");
        }

        [Test]
        public void OrbitalArena_EmitsOpportunityEvent_WithRewards()
        {
            var ship = CreateShipBase();
            _entityManager.SetComponentData(ship, new CrewCapacity
            {
                MaxCrew = 60,
                CurrentCrew = 12,
                CriticalMax = 90
            });
            _entityManager.SetComponentData(ship, AlignmentTriplet.FromFloats(-0.7f, -0.6f, -0.8f));

            var module = _entityManager.CreateEntity(typeof(PDShipModule), typeof(LeisureFacilityLimb));
            _entityManager.SetComponentData(module, new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.OrbitalArena,
                ArenaTier = ArenaProgramTier.ThirdBlood,
                HousingCapacity = 0f,
                EntertainmentRate = 0.25f,
                ComfortRate = 0.01f,
                SocialRate = 0.14f,
                NourishmentRate = 0f,
                AmbientLawBias = -0.2f,
                AmbientGoodBias = -0.22f,
                AmbientIntegrityBias = -0.2f,
                AmbientIntensity = 0.35f,
                EspionageRisk = 0f,
                PoisonRisk = 0f,
                AssassinationRisk = 0f,
                Illicitness = 0f,
                BriberyPressure = 0f,
                ParticipationPurseRate = 2f,
                LootYieldRate = 2f,
                SalvageRightsRate = 2f,
                ReputationYieldRate = 2f
            });

            var slots = _entityManager.AddBuffer<PDCarrierModuleSlot>(ship);
            slots.Add(new PDCarrierModuleSlot { InstalledModule = module });

            var system = _world.GetOrCreateSystem<Space4XLeisureNeedSystem>();
            system.Update(_world.Unmanaged);

            var opportunities = _entityManager.GetBuffer<LeisureOpportunityEvent>(ship);
            Assert.Greater(opportunities.Length, 0, "Orbital arenas should emit leisure opportunities.");
            var opportunity = opportunities[opportunities.Length - 1];
            Assert.AreEqual(LeisureOpportunityType.OrbitalWargame, opportunity.Type);
            Assert.AreEqual(module, opportunity.SourceModule);
            Assert.Greater(opportunity.PrizePurse, 0f);
            Assert.Greater(opportunity.LootYield, 0f);
            Assert.Greater(opportunity.SalvageRights, 0f);
            Assert.Greater(opportunity.ReputationGain, 0f);
        }

        [Test]
        public void ArenaAmbience_DriftsCrewTowardOrAwayByAlignmentMatch()
        {
            var ship = CreateShipBase();
            _entityManager.SetComponentData(ship, new CrewCapacity
            {
                MaxCrew = 12,
                CurrentCrew = 2,
                CriticalMax = 20
            });

            var crewBuffer = _entityManager.AddBuffer<PDCrewMember>(ship);

            var matchedCrew = _entityManager.CreateEntity(typeof(AlignmentTriplet));
            _entityManager.SetComponentData(matchedCrew, AlignmentTriplet.FromFloats(-0.5f, -0.5f, -0.5f));
            crewBuffer.Add(new PDCrewMember { CrewEntity = matchedCrew, RoleId = 0 });

            var mismatchedCrew = _entityManager.CreateEntity(typeof(AlignmentTriplet));
            _entityManager.SetComponentData(mismatchedCrew, AlignmentTriplet.FromFloats(0.5f, 0.5f, 0.5f));
            crewBuffer.Add(new PDCrewMember { CrewEntity = mismatchedCrew, RoleId = 0 });

            var module = _entityManager.CreateEntity(typeof(PDShipModule), typeof(LeisureFacilityLimb));
            _entityManager.SetComponentData(module, new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.Arena,
                ArenaTier = ArenaProgramTier.SanguinisExtremis,
                HousingCapacity = 0f,
                EntertainmentRate = 0.2f,
                ComfortRate = 0.01f,
                SocialRate = 0.08f,
                NourishmentRate = 0f,
                AmbientLawBias = -0.8f,
                AmbientGoodBias = -0.8f,
                AmbientIntegrityBias = -0.8f,
                AmbientIntensity = 0.9f,
                EspionageRisk = 0f,
                PoisonRisk = 0f,
                AssassinationRisk = 0f,
                Illicitness = 0f,
                BriberyPressure = 0f
            });

            var slots = _entityManager.AddBuffer<PDCarrierModuleSlot>(ship);
            slots.Add(new PDCarrierModuleSlot { InstalledModule = module });

            var beforeMatched = _entityManager.GetComponentData<AlignmentTriplet>(matchedCrew);
            var beforeMismatched = _entityManager.GetComponentData<AlignmentTriplet>(mismatchedCrew);

            var system = _world.GetOrCreateSystem<Space4XLeisureNeedSystem>();
            system.Update(_world.Unmanaged);

            var afterMatched = _entityManager.GetComponentData<AlignmentTriplet>(matchedCrew);
            var afterMismatched = _entityManager.GetComponentData<AlignmentTriplet>(mismatchedCrew);

            Assert.Less((float)afterMatched.Law, (float)beforeMatched.Law, "Matching crew should drift toward the arena ambience outlook.");
            Assert.Greater((float)afterMismatched.Law, (float)beforeMismatched.Law, "Mismatched crew should drift away from the arena ambience outlook.");
        }

        [Test]
        public void TempleAmbience_BendsCrewTowardSpiritualLawfulProfile()
        {
            var ship = CreateShipBase();
            _entityManager.SetComponentData(ship, new CrewCapacity
            {
                MaxCrew = 12,
                CurrentCrew = 1,
                CriticalMax = 20
            });

            var crewBuffer = _entityManager.AddBuffer<PDCrewMember>(ship);
            var crewEntity = _entityManager.CreateEntity(typeof(AlignmentTriplet));
            _entityManager.SetComponentData(crewEntity, AlignmentTriplet.FromFloats(0f, 0f, 0f));
            crewBuffer.Add(new PDCrewMember { CrewEntity = crewEntity, RoleId = 0 });

            var module = _entityManager.CreateEntity(typeof(PDShipModule), typeof(LeisureFacilityLimb));
            _entityManager.SetComponentData(module, new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.Temple,
                ArenaTier = ArenaProgramTier.Exhibition,
                HousingCapacity = 0f,
                EntertainmentRate = 0.05f,
                ComfortRate = 0.06f,
                SocialRate = 0.05f,
                NourishmentRate = 0.01f,
                AmbientLawBias = 0.8f,
                AmbientGoodBias = 0.7f,
                AmbientIntegrityBias = 0.9f,
                AmbientIntensity = 0.9f,
                EspionageRisk = 0f,
                PoisonRisk = 0f,
                AssassinationRisk = 0f,
                Illicitness = 0f,
                BriberyPressure = 0f,
                ParticipationPurseRate = 0f,
                LootYieldRate = 0f,
                SalvageRightsRate = 0f,
                ReputationYieldRate = 0.3f
            });

            var slots = _entityManager.AddBuffer<PDCarrierModuleSlot>(ship);
            slots.Add(new PDCarrierModuleSlot { InstalledModule = module });

            var before = _entityManager.GetComponentData<AlignmentTriplet>(crewEntity);

            var system = _world.GetOrCreateSystem<Space4XLeisureNeedSystem>();
            system.Update(_world.Unmanaged);

            var after = _entityManager.GetComponentData<AlignmentTriplet>(crewEntity);
            Assert.Greater((float)after.Law, (float)before.Law);
            Assert.Greater((float)after.Good, (float)before.Good);
            Assert.Greater((float)after.Integrity, (float)before.Integrity);
        }

        private Entity CreateShipBase()
        {
            var ship = _entityManager.CreateEntity(
                typeof(MoraleState),
                typeof(LeisureNeedState),
                typeof(LeisurePreferenceProfile),
                typeof(LeisureSecurityPolicy),
                typeof(LeisureFacilityAggregate));

            _entityManager.SetComponentData(ship, MoraleState.FromBaseline(0f));
            _entityManager.SetComponentData(ship, LeisureNeedState.Default);
            _entityManager.SetComponentData(ship, LeisurePreferenceProfile.Neutral);
            _entityManager.SetComponentData(ship, LeisureSecurityPolicy.Default);
            _entityManager.SetComponentData(ship, default(LeisureFacilityAggregate));
            _entityManager.AddBuffer<MoraleModifier>(ship);
            _entityManager.AddBuffer<LeisureIncidentEvent>(ship);
            _entityManager.AddBuffer<LeisureOpportunityEvent>(ship);
            return ship;
        }

        private static bool TryGetModifier(DynamicBuffer<MoraleModifier> modifiers, MoraleModifierSource source, out MoraleModifier modifier)
        {
            for (var i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].Source != source)
                {
                    continue;
                }

                modifier = modifiers[i];
                return true;
            }

            modifier = default;
            return false;
        }
    }
}
#endif
