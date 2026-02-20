#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems.Interaction;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public sealed class Space4XTradeDealSystemsTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World(nameof(Space4XTradeDealSystemsTests));
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
        public void TradeDealProposalSystem_DesperationAllowsHighMarkupOffer()
        {
            var buyer = _entityManager.CreateEntity(typeof(SupplyStatus));
            _entityManager.SetComponentData(buyer, new SupplyStatus
            {
                Fuel = 50f,
                FuelCapacity = 100f,
                Ammunition = 10f,
                AmmunitionCapacity = 100f,
                Provisions = 0f,
                ProvisionsCapacity = 100f,
                LifeSupport = 10f,
                LifeSupportCapacity = 100f,
                RepairParts = 10f,
                RepairPartsCapacity = 100f,
                Activity = ActivityLevel.Working,
                TicksSinceResupply = 200
            });

            var seller = _entityManager.CreateEntity();
            var offers = _entityManager.AddBuffer<TradeOffer>(seller);
            offers.Add(new TradeOffer
            {
                Type = TradeOfferType.Sell,
                ResourceType = MarketResourceType.Food,
                Quantity = 40f,
                PricePerUnit = 18f,
                CurrencyId = new FixedString64Bytes("credits"),
                OfferingEntity = seller,
                OfferingFactionId = 2,
                ExpirationTick = 0,
                IsFulfilled = 0
            });

            var prices = _entityManager.AddBuffer<MarketPriceEntry>(seller);
            prices.Add(new MarketPriceEntry
            {
                ResourceType = MarketResourceType.Food,
                BuyPrice = 10f,
                SellPrice = 10f,
                Supply = 100f,
                Demand = 40f,
                Volatility = (half)0f,
                BasePrice = 10f
            });

            var streamEntity = CreateIntentStreamEntity();
            var stream = _entityManager.GetBuffer<Space4XInteractionIntent>(streamEntity);
            stream.Add(new Space4XInteractionIntent
            {
                Action = Space4XInteractionIntentAction.Trade,
                Source = Space4XInteractionIntentSource.AgentAI,
                Actor = buyer,
                Target = seller,
                ContextEntity = buyer,
                ActorFactionId = 1,
                TargetFactionId = 2,
                Tick = 20,
                CorrelationId = 777,
                Priority = 1,
                Confidence = (half)1f,
                TopicId = new FixedString64Bytes("tests.trade.deal.desperate")
            });

            var dealEntity = CreateTradeDealStateEntity(Space4XTradeDealConfig.Default);
            SetTick(20);

            var proposalSystem = _world.GetOrCreateSystem<Space4XTradeDealProposalSystem>();
            proposalSystem.Update(_world.Unmanaged);

            var contracts = _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            Assert.AreEqual(1, contracts.Length, "Expected one accepted contract for desperate buyer.");

            var contract = contracts[0];
            Assert.AreEqual(Space4XTradeDealStatus.PendingExecution, contract.Status);
            Assert.GreaterOrEqual(contract.Desperation, 0.95f, "Need pressure should be reflected in contract.");
            Assert.Greater(contract.QuantityTotal, Space4XTradeDealConfig.Default.BaseLotQuantity, "Desperation should increase lot size.");
            Assert.AreEqual(Space4XMoneyMath.ToMicros(18f), contract.UnitPriceMicros);
            Assert.AreEqual(Space4XMoneyMath.ToMicros(10f), contract.ReferenceUnitPriceMicros);
        }

        [Test]
        public void TradeDealExecutionSystem_BusyParticipantDefersDeal()
        {
            var buyer = _entityManager.CreateEntity();
            var seller = _entityManager.CreateEntity(typeof(InCombatTag));

            var config = Space4XTradeDealConfig.Default;
            config.RetryBusyTicks = 7;
            var dealEntity = CreateTradeDealStateEntity(config);

            var contracts = _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            contracts.Add(new Space4XTradeDealContract
            {
                DealId = 1,
                SourceCorrelationId = 11,
                SourceAction = Space4XInteractionIntentAction.Trade,
                Status = Space4XTradeDealStatus.PendingExecution,
                LastFailure = Space4XTradeDealFailureReason.None,
                Buyer = buyer,
                Seller = seller,
                BuyerFactionId = 1,
                SellerFactionId = 2,
                MarketResource = MarketResourceType.Food,
                CargoResource = ResourceType.Food,
                CurrencyId = Space4XCurrencyId.Credits,
                UnitPriceMicros = Space4XMoneyMath.ToMicros(10f),
                QuantityTotal = 4f,
                QuantityRemaining = 4f,
                Desperation = 0.8f,
                Scarcity = 0.2f,
                CreatedTick = 1,
                ExpiresTick = 200,
                NextAttemptTick = 10
            });

            SetTick(10);
            var executionSystem = _world.GetOrCreateSystem<Space4XTradeDealExecutionSystem>();
            executionSystem.Update(_world.Unmanaged);

            contracts = _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            Assert.AreEqual(1, contracts.Length);
            Assert.AreEqual(Space4XTradeDealStatus.DeferredBusy, contracts[0].Status);
            Assert.AreEqual(Space4XTradeDealFailureReason.Busy, contracts[0].LastFailure);
            Assert.AreEqual(17u, contracts[0].NextAttemptTick);
        }

        [Test]
        public void TradeDealExecutionSystem_OutOfRangeThenInRange_CompletesTransfer()
        {
            var buyer = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XFaction));
            var seller = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XFaction));
            _entityManager.SetComponentData(buyer, Space4XFaction.Guild(1));
            _entityManager.SetComponentData(seller, Space4XFaction.Guild(2));
            _entityManager.SetComponentData(buyer, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.SetComponentData(seller, LocalTransform.FromPositionRotationScale(new float3(100f, 0f, 0f), quaternion.identity, 1f));
            SeedRelationScore(buyer, 2, 0);
            SeedRelationScore(seller, 1, 0);

            var sellerOffers = _entityManager.AddBuffer<TradeOffer>(seller);
            sellerOffers.Add(new TradeOffer
            {
                Type = TradeOfferType.Sell,
                ResourceType = MarketResourceType.Food,
                Quantity = 10f,
                PricePerUnit = 10f,
                CurrencyId = new FixedString64Bytes("credits"),
                OfferingEntity = seller,
                OfferingFactionId = 2,
                ExpirationTick = 0,
                IsFulfilled = 0
            });

            var sellerStorage = _entityManager.AddBuffer<ResourceStorage>(seller);
            sellerStorage.Add(new ResourceStorage
            {
                Type = ResourceType.Food,
                Amount = 10f,
                Capacity = 100f
            });

            var buyerStorage = _entityManager.AddBuffer<ResourceStorage>(buyer);
            buyerStorage.Add(new ResourceStorage
            {
                Type = ResourceType.Food,
                Amount = 0f,
                Capacity = 20f
            });

            var buyerMoney = _entityManager.AddBuffer<Space4XCurrencyBalance>(buyer);
            buyerMoney.Add(new Space4XCurrencyBalance
            {
                CurrencyId = Space4XCurrencyId.Credits,
                AmountMicros = Space4XMoneyMath.ToMicros(100f)
            });

            var sellerMoney = _entityManager.AddBuffer<Space4XCurrencyBalance>(seller);
            sellerMoney.Add(new Space4XCurrencyBalance
            {
                CurrencyId = Space4XCurrencyId.Credits,
                AmountMicros = Space4XMoneyMath.ToMicros(10f)
            });

            var config = Space4XTradeDealConfig.Default;
            config.ExecutionRange = 6f;
            config.TransferRatePerTick = 10f;
            config.RetryRangeTicks = 2;
            var dealEntity = CreateTradeDealStateEntity(config);
            var contracts = _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            contracts.Add(new Space4XTradeDealContract
            {
                DealId = 2,
                SourceCorrelationId = 12,
                SourceAction = Space4XInteractionIntentAction.Trade,
                Status = Space4XTradeDealStatus.PendingExecution,
                LastFailure = Space4XTradeDealFailureReason.None,
                Buyer = buyer,
                Seller = seller,
                BuyerFactionId = 1,
                SellerFactionId = 2,
                MarketResource = MarketResourceType.Food,
                CargoResource = ResourceType.Food,
                CurrencyId = Space4XCurrencyId.Credits,
                UnitPriceMicros = Space4XMoneyMath.ToMicros(10f),
                ReferenceUnitPriceMicros = Space4XMoneyMath.ToMicros(10f),
                QuantityTotal = 4f,
                QuantityRemaining = 4f,
                Desperation = 0.6f,
                Scarcity = 0.2f,
                CreatedTick = 1,
                ExpiresTick = 200,
                NextAttemptTick = 1
            });

            SetTick(1);
            var executionSystem = _world.GetOrCreateSystem<Space4XTradeDealExecutionSystem>();
            executionSystem.Update(_world.Unmanaged);

            contracts = _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            Assert.AreEqual(Space4XTradeDealStatus.DeferredRange, contracts[0].Status);
            Assert.AreEqual(Space4XTradeDealFailureReason.OutOfRange, contracts[0].LastFailure);
            Assert.AreEqual(3u, contracts[0].NextAttemptTick);

            _entityManager.SetComponentData(buyer, LocalTransform.FromPositionRotationScale(new float3(2f, 0f, 0f), quaternion.identity, 1f));
            SetTick(contracts[0].NextAttemptTick);
            executionSystem.Update(_world.Unmanaged);

            contracts = _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            Assert.AreEqual(Space4XTradeDealStatus.Completed, contracts[0].Status);
            Assert.AreEqual(0f, contracts[0].QuantityRemaining, 1e-4f);
            Assert.AreEqual(Space4XTradeDealFailureReason.None, contracts[0].LastFailure);

            sellerOffers = _entityManager.GetBuffer<TradeOffer>(seller);
            Assert.AreEqual(6f, sellerOffers[0].Quantity, 1e-4f);

            sellerStorage = _entityManager.GetBuffer<ResourceStorage>(seller);
            Assert.AreEqual(6f, sellerStorage[0].Amount, 1e-4f);

            buyerStorage = _entityManager.GetBuffer<ResourceStorage>(buyer);
            Assert.AreEqual(4f, buyerStorage[0].Amount, 1e-4f);

            buyerMoney = _entityManager.GetBuffer<Space4XCurrencyBalance>(buyer);
            sellerMoney = _entityManager.GetBuffer<Space4XCurrencyBalance>(seller);
            Assert.AreEqual(Space4XMoneyMath.ToMicros(60f), buyerMoney[0].AmountMicros);
            Assert.AreEqual(Space4XMoneyMath.ToMicros(50f), sellerMoney[0].AmountMicros);
            Assert.Greater(GetRelationScore(buyer, 2), 0, "Fair pricing should improve buyer relation.");
            Assert.Greater(GetContactStanding(buyer, 2), 0.2f, "Fair pricing should improve contact standing.");
        }

        [Test]
        public void TradeDealExecutionSystem_ExtortionPrice_DamagesBuyerRelation()
        {
            var buyer = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XFaction), typeof(SupplyStatus));
            var seller = _entityManager.CreateEntity(typeof(LocalTransform), typeof(Space4XFaction));
            _entityManager.SetComponentData(buyer, Space4XFaction.Guild(1));
            _entityManager.SetComponentData(seller, Space4XFaction.Guild(2));
            _entityManager.SetComponentData(buyer, new SupplyStatus
            {
                Fuel = 40f,
                FuelCapacity = 100f,
                Ammunition = 20f,
                AmmunitionCapacity = 100f,
                Provisions = 0f,
                ProvisionsCapacity = 100f,
                LifeSupport = 10f,
                LifeSupportCapacity = 100f,
                RepairParts = 10f,
                RepairPartsCapacity = 100f,
                Activity = ActivityLevel.Working,
                TicksSinceResupply = 300
            });
            _entityManager.SetComponentData(buyer, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 0f), quaternion.identity, 1f));
            _entityManager.SetComponentData(seller, LocalTransform.FromPositionRotationScale(new float3(1f, 0f, 0f), quaternion.identity, 1f));
            SeedRelationScore(buyer, 2, 0);

            var sellerOffers = _entityManager.AddBuffer<TradeOffer>(seller);
            sellerOffers.Add(new TradeOffer
            {
                Type = TradeOfferType.Sell,
                ResourceType = MarketResourceType.Food,
                Quantity = 10f,
                PricePerUnit = 18f,
                CurrencyId = new FixedString64Bytes("credits"),
                OfferingEntity = seller,
                OfferingFactionId = 2,
                ExpirationTick = 0,
                IsFulfilled = 0
            });

            var sellerStorage = _entityManager.AddBuffer<ResourceStorage>(seller);
            sellerStorage.Add(new ResourceStorage { Type = ResourceType.Food, Amount = 10f, Capacity = 100f });
            var buyerStorage = _entityManager.AddBuffer<ResourceStorage>(buyer);
            buyerStorage.Add(new ResourceStorage { Type = ResourceType.Food, Amount = 0f, Capacity = 20f });

            var buyerMoney = _entityManager.AddBuffer<Space4XCurrencyBalance>(buyer);
            buyerMoney.Add(new Space4XCurrencyBalance { CurrencyId = Space4XCurrencyId.Credits, AmountMicros = Space4XMoneyMath.ToMicros(100f) });
            var sellerMoney = _entityManager.AddBuffer<Space4XCurrencyBalance>(seller);
            sellerMoney.Add(new Space4XCurrencyBalance { CurrencyId = Space4XCurrencyId.Credits, AmountMicros = Space4XMoneyMath.ToMicros(0f) });

            var config = Space4XTradeDealConfig.Default;
            config.TransferRatePerTick = 10f;
            var dealEntity = CreateTradeDealStateEntity(config);
            var contracts = _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            contracts.Add(new Space4XTradeDealContract
            {
                DealId = 3,
                SourceCorrelationId = 13,
                SourceAction = Space4XInteractionIntentAction.Trade,
                Status = Space4XTradeDealStatus.PendingExecution,
                LastFailure = Space4XTradeDealFailureReason.None,
                Buyer = buyer,
                Seller = seller,
                BuyerFactionId = 1,
                SellerFactionId = 2,
                MarketResource = MarketResourceType.Food,
                CargoResource = ResourceType.Food,
                CurrencyId = Space4XCurrencyId.Credits,
                UnitPriceMicros = Space4XMoneyMath.ToMicros(18f),
                ReferenceUnitPriceMicros = Space4XMoneyMath.ToMicros(10f),
                QuantityTotal = 4f,
                QuantityRemaining = 4f,
                Desperation = 1f,
                Scarcity = 0.5f,
                CreatedTick = 1,
                ExpiresTick = 200,
                NextAttemptTick = 1
            });

            SetTick(1);
            var executionSystem = _world.GetOrCreateSystem<Space4XTradeDealExecutionSystem>();
            executionSystem.Update(_world.Unmanaged);

            Assert.AreEqual(Space4XTradeDealStatus.Completed, _entityManager.GetBuffer<Space4XTradeDealContract>(dealEntity)[0].Status);
            Assert.Less(GetRelationScore(buyer, 2), 0, "Extortionate pricing should reduce buyer relation.");
            Assert.Less(GetContactStanding(buyer, 2), 0.2f, "Extortionate pricing should reduce standing.");
        }

        private Entity CreateIntentStreamEntity()
        {
            var streamEntity = _entityManager.CreateEntity(typeof(Space4XInteractionIntentStream));
            _entityManager.AddBuffer<Space4XInteractionIntent>(streamEntity);
            return streamEntity;
        }

        private Entity CreateTradeDealStateEntity(Space4XTradeDealConfig config)
        {
            var dealEntity = _entityManager.CreateEntity(typeof(Space4XTradeDealState), typeof(Space4XTradeDealConfig));
            _entityManager.SetComponentData(dealEntity, new Space4XTradeDealState
            {
                NextDealId = 1,
                LastProposalTick = 0
            });
            _entityManager.SetComponentData(dealEntity, config);
            _entityManager.AddBuffer<Space4XTradeDealContract>(dealEntity);
            _entityManager.AddBuffer<Space4XTradeDealEvent>(dealEntity);
            return dealEntity;
        }

        private void SetTick(uint tick)
        {
            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>());
            var entity = query.GetSingletonEntity();
            var time = _entityManager.GetComponentData<TimeState>(entity);
            time.Tick = tick;
            _entityManager.SetComponentData(entity, time);
        }

        private void SeedRelationScore(Entity faction, ushort otherFactionId, sbyte score)
        {
            var relations = _entityManager.AddBuffer<FactionRelationEntry>(faction);
            relations.Add(new FactionRelationEntry
            {
                Relation = new FactionRelation
                {
                    OtherFaction = Entity.Null,
                    OtherFactionId = otherFactionId,
                    Score = score,
                    Trust = (half)0f,
                    Fear = (half)0f,
                    Respect = (half)0f,
                    TradeVolume = 0f,
                    RecentCombats = 0,
                    LastInteractionTick = 0
                }
            });
        }

        private int GetRelationScore(Entity faction, ushort otherFactionId)
        {
            var relations = _entityManager.GetBuffer<FactionRelationEntry>(faction);
            for (int i = 0; i < relations.Length; i++)
            {
                if (relations[i].Relation.OtherFactionId == otherFactionId)
                {
                    return relations[i].Relation.Score;
                }
            }

            return 0;
        }

        private float GetContactStanding(Entity faction, ushort otherFactionId)
        {
            if (!_entityManager.HasBuffer<Space4XContactStanding>(faction))
            {
                return 0f;
            }

            var standing = _entityManager.GetBuffer<Space4XContactStanding>(faction);
            for (int i = 0; i < standing.Length; i++)
            {
                if (standing[i].ContactFactionId == otherFactionId)
                {
                    return standing[i].Standing;
                }
            }

            return 0f;
        }
    }
}
#endif
