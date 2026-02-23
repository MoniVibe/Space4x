#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Tests
{
    public class Space4XTaxSystemsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XTaxSystemsTests");
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
        public void TaxBootstrap_AddsDefaultPolicyRuntimeAndBands()
        {
            var faction = CreateFaction(11, FactionOutlook.Authoritarian);

            var bootstrap = _world.GetOrCreateSystem<Space4XTaxBootstrapSystem>();
            bootstrap.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasComponent<Space4XTaxPolicy>(faction));
            Assert.IsTrue(_entityManager.HasComponent<Space4XTaxRuntime>(faction));
            Assert.IsTrue(_entityManager.HasBuffer<Space4XTaxBand>(faction));

            var bands = _entityManager.GetBuffer<Space4XTaxBand>(faction);
            Assert.GreaterOrEqual(bands.Length, 3);
        }

        [Test]
        public void TaxCollection_CollectsFromTaxableIncome_AndWithholdsBusinessWallet()
        {
            var faction = CreateFaction(12, FactionOutlook.None, startingCredits:0f);
            ConfigurePolicy(faction, Space4XTaxProfileSource.Ruler, baseRate:0.2f, lawWeight:0f, integrityWeight:0f, businessAssetRate:0f);

            var business = _entityManager.CreateEntity(typeof(Space4XBusinessState), typeof(Space4XTaxableIncome));
            _entityManager.SetComponentData(business, new Space4XBusinessState
            {
                Kind = Space4XBusinessKind.MiningCompany,
                OwnerKind = Space4XBusinessOwnerKind.Faction,
                Owner = faction,
                Colony = Entity.Null,
                Facility = Entity.Null,
                FacilityClass = FacilityBusinessClass.None,
                ActiveJobId = default,
                LastJobTick = 0u,
                NextJobTick = 0u,
                Credits = 100f
            });
            _entityManager.SetComponentData(business, new Space4XTaxableIncome
            {
                TaxAuthority = faction,
                GrossIncomePerTick = 20f,
                ShieldIncomePerTick = 5f,
                IsExempt = 0,
                WithholdFromWallet = 1
            });

            var taxSystem = _world.GetOrCreateSystem<Space4XTaxCollectionSystem>();
            taxSystem.Update(_world.Unmanaged);

            var businessAfter = _entityManager.GetComponentData<Space4XBusinessState>(business);
            var factionAfter = _entityManager.GetComponentData<FactionResources>(faction);
            var runtime = _entityManager.GetComponentData<Space4XTaxRuntime>(faction);

            Assert.AreEqual(97f, businessAfter.Credits, 1e-3f);
            Assert.AreEqual(3f, factionAfter.Credits, 1e-3f);
            Assert.AreEqual(3f, runtime.LastCollectedCredits, 1e-3f);
            Assert.AreEqual(1u, runtime.LastTaxpayerCount);
        }

        [Test]
        public void TaxCollection_ProfileSourceChangesEffectiveRate()
        {
            var rulerFaction = CreateFaction(21, FactionOutlook.None, startingCredits:0f);
            var societyFaction = CreateFaction(22, FactionOutlook.None, startingCredits:0f);

            _entityManager.AddComponentData(rulerFaction, AlignmentTriplet.FromFloats(1f, 0f, 0f));
            _entityManager.AddComponentData(societyFaction, AlignmentTriplet.FromFloats(1f, 0f, 0f));

            ConfigurePolicy(rulerFaction, Space4XTaxProfileSource.Ruler, baseRate:0.1f, lawWeight:0.1f, integrityWeight:0f, businessAssetRate:0f);
            ConfigurePolicy(societyFaction, Space4XTaxProfileSource.SocietyAverage, baseRate:0.1f, lawWeight:0.1f, integrityWeight:0f, businessAssetRate:0f);

            var citizen = _entityManager.CreateEntity(typeof(AlignmentTriplet));
            _entityManager.SetComponentData(citizen, AlignmentTriplet.FromFloats(-1f, 0f, 0f));
            var affiliations = _entityManager.AddBuffer<AffiliationTag>(citizen);
            affiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = societyFaction,
                Loyalty = (half)1f
            });

            var rulerTaxpayer = _entityManager.CreateEntity(typeof(Space4XTaxableIncome));
            _entityManager.SetComponentData(rulerTaxpayer, new Space4XTaxableIncome
            {
                TaxAuthority = rulerFaction,
                GrossIncomePerTick = 10f,
                ShieldIncomePerTick = 0f,
                IsExempt = 0,
                WithholdFromWallet = 0
            });

            var societyTaxpayer = _entityManager.CreateEntity(typeof(Space4XTaxableIncome));
            _entityManager.SetComponentData(societyTaxpayer, new Space4XTaxableIncome
            {
                TaxAuthority = societyFaction,
                GrossIncomePerTick = 10f,
                ShieldIncomePerTick = 0f,
                IsExempt = 0,
                WithholdFromWallet = 0
            });

            var taxSystem = _world.GetOrCreateSystem<Space4XTaxCollectionSystem>();
            taxSystem.Update(_world.Unmanaged);

            var rulerRuntime = _entityManager.GetComponentData<Space4XTaxRuntime>(rulerFaction);
            var societyRuntime = _entityManager.GetComponentData<Space4XTaxRuntime>(societyFaction);

            Assert.Greater(rulerRuntime.LastEffectiveRate01, societyRuntime.LastEffectiveRate01);
            Assert.Greater(_entityManager.GetComponentData<FactionResources>(rulerFaction).Credits,
                _entityManager.GetComponentData<FactionResources>(societyFaction).Credits);
        }

        [Test]
        public void TaxCollection_BusinessAssetTaxUsesColonyFactionAffiliation()
        {
            var faction = CreateFaction(31, FactionOutlook.None, startingCredits:0f);
            ConfigurePolicy(faction, Space4XTaxProfileSource.Ruler, baseRate:0f, lawWeight:0f, integrityWeight:0f, businessAssetRate:0.1f, exemption:50f);

            var colony = _entityManager.CreateEntity();
            var affiliations = _entityManager.AddBuffer<AffiliationTag>(colony);
            affiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = faction,
                Loyalty = (half)1f
            });

            var business = _entityManager.CreateEntity(typeof(Space4XBusinessState));
            _entityManager.SetComponentData(business, new Space4XBusinessState
            {
                Kind = Space4XBusinessKind.StationServices,
                OwnerKind = Space4XBusinessOwnerKind.Individual,
                Owner = Entity.Null,
                Colony = colony,
                Facility = Entity.Null,
                FacilityClass = FacilityBusinessClass.None,
                ActiveJobId = default,
                LastJobTick = 0u,
                NextJobTick = 0u,
                Credits = 100f
            });

            var taxSystem = _world.GetOrCreateSystem<Space4XTaxCollectionSystem>();
            taxSystem.Update(_world.Unmanaged);

            var businessAfter = _entityManager.GetComponentData<Space4XBusinessState>(business);
            var factionAfter = _entityManager.GetComponentData<FactionResources>(faction);

            Assert.AreEqual(95f, businessAfter.Credits, 1e-3f);
            Assert.AreEqual(5f, factionAfter.Credits, 1e-3f);
        }

        private Entity CreateFaction(ushort factionId, FactionOutlook outlook, float startingCredits = 0f)
        {
            var faction = _entityManager.CreateEntity(typeof(Space4XFaction), typeof(FactionResources));
            _entityManager.SetComponentData(faction, Space4XFaction.Empire(factionId, outlook));
            _entityManager.SetComponentData(faction, new FactionResources
            {
                Credits = startingCredits,
                Materials = 0f,
                Energy = 0f,
                Influence = 0f,
                Research = 0f,
                IncomeRate = 0f,
                ExpenseRate = 0f
            });
            return faction;
        }

        private void ConfigurePolicy(
            Entity faction,
            Space4XTaxProfileSource source,
            float baseRate,
            float lawWeight,
            float integrityWeight,
            float businessAssetRate,
            float exemption = 0f)
        {
            _entityManager.AddComponentData(faction, new Space4XTaxPolicy
            {
                Enabled = 1,
                CollectionIntervalTicks = 1u,
                ProfileSource = source,
                SocietyBlendWeight01 = (half)0.5f,
                BaseRate01 = (half)baseRate,
                MinRate01 = (half)0f,
                MaxRate01 = (half)1f,
                LawfulnessRateWeight01 = (half)lawWeight,
                IntegrityRateWeight01 = (half)integrityWeight,
                AuthoritarianRateBias01 = (half)0f,
                EgalitarianRateBias01 = (half)0f,
                BaseLeakRate01 = (half)0f,
                CorruptLeakBonus01 = (half)0f,
                BusinessAssetTaxRate01 = (half)businessAssetRate,
                BusinessCreditsExemption = exemption
            });

            _entityManager.AddComponentData(faction, new Space4XTaxRuntime
            {
                LastCollectionTick = 0u,
                LastCollectedCredits = 0f,
                LastLeakageCredits = 0f,
                LastEffectiveRate01 = 0f,
                LastProfileLaw = 0f,
                LastProfileIntegrity = 0f,
                LastTaxpayerCount = 0u,
                TotalCollectedCredits = 0f,
                TotalLeakageCredits = 0f
            });

            var bands = _entityManager.AddBuffer<Space4XTaxBand>(faction);
            bands.Add(new Space4XTaxBand
            {
                MinIncomePerTick = 0f,
                MaxIncomePerTick = 0f,
                RateOffset01 = (half)0f
            });
        }
    }
}
#endif

