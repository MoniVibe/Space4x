using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Interaction
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XInteractionIntentBootstrapSystem))]
    public partial struct Space4XTradeDealBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XTradeDealState>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XTradeDealState), typeof(Space4XTradeDealConfig));
            state.EntityManager.SetComponentData(entity, new Space4XTradeDealState
            {
                NextDealId = 1,
                LastProposalTick = 0
            });
            state.EntityManager.SetComponentData(entity, Space4XTradeDealConfig.Default);
            state.EntityManager.AddBuffer<Space4XTradeDealContract>(entity);
            state.EntityManager.AddBuffer<Space4XTradeDealEvent>(entity);
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XInteractionIntentPolicySystem))]
    public partial struct Space4XTradeDealProposalSystem : ISystem
    {
        private BufferLookup<TradeOffer> _offerLookup;
        private BufferLookup<MarketPriceEntry> _priceLookup;
        private ComponentLookup<SupplyStatus> _supplyLookup;
        private BufferLookup<ResourceStorage> _storageLookup;
        private EntityStorageInfoLookup _entityLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionIntentStream>();
            state.RequireForUpdate<Space4XTradeDealState>();
            state.RequireForUpdate<Space4XTradeDealConfig>();
            _offerLookup = state.GetBufferLookup<TradeOffer>(true);
            _priceLookup = state.GetBufferLookup<MarketPriceEntry>(true);
            _supplyLookup = state.GetComponentLookup<SupplyStatus>(true);
            _storageLookup = state.GetBufferLookup<ResourceStorage>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            _offerLookup.Update(ref state);
            _priceLookup.Update(ref state);
            _supplyLookup.Update(ref state);
            _storageLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var config = SystemAPI.GetSingleton<Space4XTradeDealConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            var tick = SystemAPI.TryGetSingleton<TimeState>(out var time) ? time.Tick : 0u;
            var streamEntity = SystemAPI.GetSingletonEntity<Space4XInteractionIntentStream>();
            var intents = state.EntityManager.GetBuffer<Space4XInteractionIntent>(streamEntity);
            if (intents.Length == 0)
            {
                return;
            }

            var dealEntity = SystemAPI.GetSingletonEntity<Space4XTradeDealState>();
            var dealState = state.EntityManager.GetComponentData<Space4XTradeDealState>(dealEntity);
            var contracts = state.EntityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            var events = state.EntityManager.GetBuffer<Space4XTradeDealEvent>(dealEntity);

            for (int i = 0; i < intents.Length; i++)
            {
                var intent = intents[i];
                if (intent.Action != Space4XInteractionIntentAction.Trade || intent.Actor == Entity.Null || intent.Target == Entity.Null)
                {
                    continue;
                }

                if (!_entityLookup.Exists(intent.Actor) || !_entityLookup.Exists(intent.Target))
                {
                    continue;
                }

                if (HasOpenDeal(contracts, intent.Actor, intent.Target, intent.CorrelationId))
                {
                    continue;
                }

                if (!TrySelectCandidate(intent.Actor, intent.Target, in config, out var candidate))
                {
                    AppendEvent(ref events, tick, 0, Space4XTradeDealStatus.Failed, Space4XTradeDealFailureReason.NoOffer, intent.Actor, intent.Target, MarketResourceType.Consumer, 0f, 0f, 0, new FixedString64Bytes("trade.deal.no_offer"));
                    continue;
                }

                var quantity = math.min(candidate.AvailableQuantity, math.max(0.25f, config.BaseLotQuantity * (1f + candidate.Desperation * config.DesperationLotScale)));
                if (quantity <= 0f)
                {
                    continue;
                }

                var dealId = dealState.NextDealId == 0 ? 1u : dealState.NextDealId;
                dealState.NextDealId = dealId + 1u;
                contracts.Add(new Space4XTradeDealContract
                {
                    DealId = dealId,
                    SourceCorrelationId = intent.CorrelationId,
                    SourceAction = intent.Action,
                    Status = Space4XTradeDealStatus.PendingExecution,
                    LastFailure = Space4XTradeDealFailureReason.None,
                    Buyer = intent.Actor,
                    Seller = intent.Target,
                    BuyerFactionId = intent.ActorFactionId,
                    SellerFactionId = intent.TargetFactionId,
                    MarketResource = candidate.Resource,
                    CargoResource = candidate.CargoResource,
                    CurrencyId = config.CurrencyId,
                    UnitPriceMicros = candidate.UnitPriceMicros,
                    ReferenceUnitPriceMicros = candidate.ReferenceUnitPriceMicros,
                    QuantityTotal = quantity,
                    QuantityRemaining = quantity,
                    Desperation = candidate.Desperation,
                    Scarcity = candidate.Scarcity,
                    CreatedTick = tick,
                    ExpiresTick = tick + math.max(60u, config.ExpiryTicks),
                    NextAttemptTick = tick
                });

                AppendEvent(ref events, tick, dealId, Space4XTradeDealStatus.PendingExecution, Space4XTradeDealFailureReason.None, intent.Actor, intent.Target, candidate.Resource, 0f, quantity, 0, new FixedString64Bytes("trade.deal.accepted"));
            }

            dealState.LastProposalTick = tick;
            state.EntityManager.SetComponentData(dealEntity, dealState);
            TrimEvents(ref events, 256);
            TrimContracts(ref contracts, 64);
        }

        private bool TrySelectCandidate(Entity buyer, Entity seller, in Space4XTradeDealConfig config, out TradeCandidate candidate)
        {
            candidate = default;
            if (!_offerLookup.HasBuffer(seller))
            {
                return false;
            }

            DynamicBuffer<MarketPriceEntry> prices = default;
            bool hasPrices = _priceLookup.HasBuffer(seller);
            if (hasPrices)
            {
                prices = _priceLookup[seller];
            }

            var offers = _offerLookup[seller];
            bool found = false;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (offer.IsFulfilled != 0 || offer.Type != TradeOfferType.Sell || offer.Quantity <= 0f)
                {
                    continue;
                }

                if (!TryMapMarketToResource(offer.ResourceType, out var cargoResource))
                {
                    continue;
                }

                var referencePrice = math.max(0.01f, offer.PricePerUnit);
                if (hasPrices && TryGetReferencePrice(prices, offer.ResourceType, out var marketPrice))
                {
                    referencePrice = marketPrice;
                }

                var markup = (offer.PricePerUnit - referencePrice) / referencePrice;
                var desperation = ResolveResourceNeed(buyer, offer.ResourceType, cargoResource);
                var scarcity = ResolveScarcity(offer.ResourceType, offer.Quantity, hasPrices ? prices : default);
                var maxMarkup = (float)config.BaseAcceptableMarkup + desperation * (float)config.DesperationMarkupScale + scarcity * (float)config.ScarcityMarkupScale;
                if (markup > maxMarkup + 0.0001f)
                {
                    continue;
                }

                var score = desperation * 1.4f + scarcity - markup * 0.5f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                found = true;
                candidate = new TradeCandidate
                {
                    Resource = offer.ResourceType,
                    CargoResource = cargoResource,
                    UnitPriceMicros = Space4XMoneyMath.ToMicros(math.max(0.01f, offer.PricePerUnit)),
                    ReferenceUnitPriceMicros = Space4XMoneyMath.ToMicros(math.max(0.01f, referencePrice)),
                    AvailableQuantity = math.max(0f, offer.Quantity),
                    Desperation = desperation,
                    Scarcity = scarcity
                };
            }

            return found;
        }

        private float ResolveResourceNeed(Entity actor, MarketResourceType marketResource, ResourceType cargoResource)
        {
            float needBySupply = 0f;
            if (_supplyLookup.HasComponent(actor))
            {
                var supply = _supplyLookup[actor];
                switch (marketResource)
                {
                    case MarketResourceType.Food:
                    case MarketResourceType.Water:
                        needBySupply = 1f - math.saturate(supply.ProvisionsRatio);
                        break;
                    case MarketResourceType.Energy:
                        needBySupply = 1f - math.saturate(supply.FuelRatio);
                        break;
                }
            }

            if (!_storageLookup.HasBuffer(actor))
            {
                return math.saturate(needBySupply);
            }

            var storage = _storageLookup[actor];
            float storageRatio = 0f;
            bool found = false;
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type != cargoResource)
                {
                    continue;
                }

                found = true;
                storageRatio = storage[i].Capacity > 0f ? math.saturate(storage[i].Amount / storage[i].Capacity) : (storage[i].Amount > 0f ? 1f : 0f);
                break;
            }

            var baselineNeed = marketResource == MarketResourceType.Food ||
                               marketResource == MarketResourceType.Water ||
                               marketResource == MarketResourceType.Energy
                ? 0.35f
                : 0.05f;
            var needByStorage = found ? 1f - storageRatio : baselineNeed;
            return math.saturate(math.max(needBySupply, needByStorage));
        }

        private static float ResolveScarcity(MarketResourceType resource, float offerQuantity, DynamicBuffer<MarketPriceEntry> prices)
        {
            float scarcity = 0f;
            if (prices.IsCreated)
            {
                for (int i = 0; i < prices.Length; i++)
                {
                    if (prices[i].ResourceType != resource)
                    {
                        continue;
                    }

                    var supply = math.max(0f, prices[i].Supply);
                    var demand = math.max(0f, prices[i].Demand);
                    scarcity = supply <= 0.001f ? 1f : math.saturate((demand + 1f) / (supply + 1f) - 1f);
                    break;
                }
            }

            if (offerQuantity <= 2f)
            {
                scarcity = math.max(scarcity, 0.85f);
            }
            else if (offerQuantity <= 5f)
            {
                scarcity = math.max(scarcity, 0.6f);
            }

            return scarcity;
        }

        private static bool TryGetReferencePrice(DynamicBuffer<MarketPriceEntry> prices, MarketResourceType resource, out float price)
        {
            for (int i = 0; i < prices.Length; i++)
            {
                if (prices[i].ResourceType != resource)
                {
                    continue;
                }

                var buy = math.max(0f, prices[i].BuyPrice);
                var sell = math.max(0f, prices[i].SellPrice);
                var mid = (buy + sell) * 0.5f;
                price = math.max(0.01f, mid > 0f ? mid : buy);
                return true;
            }

            price = 0f;
            return false;
        }

        private static bool HasOpenDeal(DynamicBuffer<Space4XTradeDealContract> contracts, Entity buyer, Entity seller, uint correlationId)
        {
            for (int i = 0; i < contracts.Length; i++)
            {
                var contract = contracts[i];
                if (contract.Buyer != buyer || contract.Seller != seller)
                {
                    continue;
                }

                if (correlationId != 0 && contract.SourceCorrelationId == correlationId)
                {
                    return true;
                }

                if (contract.Status == Space4XTradeDealStatus.Completed ||
                    contract.Status == Space4XTradeDealStatus.Failed ||
                    contract.Status == Space4XTradeDealStatus.Expired ||
                    contract.Status == Space4XTradeDealStatus.Cancelled)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static void TrimEvents(ref DynamicBuffer<Space4XTradeDealEvent> events, int max)
        {
            while (events.Length > max)
            {
                events.RemoveAt(0);
            }
        }

        private static void TrimContracts(ref DynamicBuffer<Space4XTradeDealContract> contracts, int max)
        {
            if (contracts.Length <= max)
            {
                return;
            }

            for (int i = contracts.Length - 1; i >= 0 && contracts.Length > max; i--)
            {
                var status = contracts[i].Status;
                if (status == Space4XTradeDealStatus.Completed ||
                    status == Space4XTradeDealStatus.Failed ||
                    status == Space4XTradeDealStatus.Expired ||
                    status == Space4XTradeDealStatus.Cancelled)
                {
                    contracts.RemoveAt(i);
                }
            }
        }

        private static bool TryMapMarketToResource(MarketResourceType market, out ResourceType resource)
        {
            switch (market)
            {
                case MarketResourceType.Ore: resource = ResourceType.Ore; return true;
                case MarketResourceType.RefinedMetal: resource = ResourceType.Minerals; return true;
                case MarketResourceType.RareEarth: resource = ResourceType.RareMetals; return true;
                case MarketResourceType.Energy: resource = ResourceType.Fuel; return true;
                case MarketResourceType.Food: resource = ResourceType.Food; return true;
                case MarketResourceType.Water: resource = ResourceType.Water; return true;
                case MarketResourceType.Industrial: resource = ResourceType.Supplies; return true;
                case MarketResourceType.Tech: resource = ResourceType.RelicData; return true;
                case MarketResourceType.Luxury: resource = ResourceType.BoosterGas; return true;
                case MarketResourceType.Military: resource = ResourceType.StrontiumClathrates; return true;
                case MarketResourceType.Consumer: resource = ResourceType.OrganicMatter; return true;
                default: resource = default; return false;
            }
        }

        private static void AppendEvent(ref DynamicBuffer<Space4XTradeDealEvent> events, uint tick, uint dealId, Space4XTradeDealStatus status, Space4XTradeDealFailureReason failure, Entity buyer, Entity seller, MarketResourceType resource, float quantityDelta, float quantityRemaining, long valueMicros, in FixedString64Bytes messageId)
        {
            events.Add(new Space4XTradeDealEvent
            {
                Tick = tick,
                DealId = dealId,
                Status = status,
                Failure = failure,
                Buyer = buyer,
                Seller = seller,
                MarketResource = resource,
                QuantityDelta = quantityDelta,
                QuantityRemaining = quantityRemaining,
                ValueMicros = valueMicros,
                MessageId = messageId
            });
        }

        private struct TradeCandidate
        {
            public MarketResourceType Resource;
            public ResourceType CargoResource;
            public long UnitPriceMicros;
            public long ReferenceUnitPriceMicros;
            public float AvailableQuantity;
            public float Desperation;
            public float Scarcity;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XTradeDealProposalSystem))]
    public partial struct Space4XTradeDealExecutionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<InCombatTag> _inCombatLookup;
        private ComponentLookup<MiningState> _miningLookup;
        private ComponentLookup<CaptainOrder> _captainOrderLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<ResourceStorage> _storageLookup;
        private BufferLookup<TradeOffer> _offerLookup;
        private BufferLookup<Space4XCurrencyBalance> _currencyLookup;
        private EntityStorageInfoLookup _entityLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTradeDealState>();
            state.RequireForUpdate<Space4XTradeDealConfig>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _inCombatLookup = state.GetComponentLookup<InCombatTag>(true);
            _miningLookup = state.GetComponentLookup<MiningState>(true);
            _captainOrderLookup = state.GetComponentLookup<CaptainOrder>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _storageLookup = state.GetBufferLookup<ResourceStorage>(false);
            _offerLookup = state.GetBufferLookup<TradeOffer>(false);
            _currencyLookup = state.GetBufferLookup<Space4XCurrencyBalance>(false);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _inCombatLookup.Update(ref state);
            _miningLookup.Update(ref state);
            _captainOrderLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _storageLookup.Update(ref state);
            _offerLookup.Update(ref state);
            _currencyLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var config = SystemAPI.GetSingleton<Space4XTradeDealConfig>();
            if (config.Enabled == 0)
            {
                return;
            }
            var tierConfig = SystemAPI.TryGetSingleton<Space4XContactTierConfig>(out var contactTierConfig)
                ? contactTierConfig
                : Space4XContactTierConfig.Default;

            var tick = SystemAPI.TryGetSingleton<TimeState>(out var time) ? time.Tick : 0u;
            var dealEntity = SystemAPI.GetSingletonEntity<Space4XTradeDealState>();
            var contracts = state.EntityManager.GetBuffer<Space4XTradeDealContract>(dealEntity);
            if (contracts.Length == 0)
            {
                return;
            }

            var events = state.EntityManager.GetBuffer<Space4XTradeDealEvent>(dealEntity);
            for (int i = 0; i < contracts.Length; i++)
            {
                var contract = contracts[i];
                if (contract.Status == Space4XTradeDealStatus.Completed ||
                    contract.Status == Space4XTradeDealStatus.Failed ||
                    contract.Status == Space4XTradeDealStatus.Expired ||
                    contract.Status == Space4XTradeDealStatus.Cancelled)
                {
                    continue;
                }

                if (tick >= contract.ExpiresTick)
                {
                    contract.Status = Space4XTradeDealStatus.Expired;
                    contract.LastFailure = Space4XTradeDealFailureReason.Expired;
                    contracts[i] = contract;
                    AppendEvent(ref events, tick, in contract, 0f, 0, new FixedString64Bytes("trade.deal.expired"));
                    continue;
                }

                if (!_entityLookup.Exists(contract.Buyer) || !_entityLookup.Exists(contract.Seller))
                {
                    contract.Status = Space4XTradeDealStatus.Failed;
                    contract.LastFailure = Space4XTradeDealFailureReason.InvalidEntity;
                    contracts[i] = contract;
                    AppendEvent(ref events, tick, in contract, 0f, 0, new FixedString64Bytes("trade.deal.invalid_entity"));
                    continue;
                }

                if (tick < contract.NextAttemptTick)
                {
                    continue;
                }

                if (IsBusy(contract.Buyer) || IsBusy(contract.Seller))
                {
                    contract.Status = Space4XTradeDealStatus.DeferredBusy;
                    contract.LastFailure = Space4XTradeDealFailureReason.Busy;
                    contract.NextAttemptTick = tick + math.max(1u, config.RetryBusyTicks);
                    contracts[i] = contract;
                    AppendEvent(ref events, tick, in contract, 0f, 0, new FixedString64Bytes("trade.deal.deferred_busy"));
                    continue;
                }

                if (!IsWithinRange(contract.Buyer, contract.Seller, config.ExecutionRange))
                {
                    contract.Status = Space4XTradeDealStatus.DeferredRange;
                    contract.LastFailure = Space4XTradeDealFailureReason.OutOfRange;
                    contract.NextAttemptTick = tick + math.max(1u, config.RetryRangeTicks);
                    contracts[i] = contract;
                    AppendEvent(ref events, tick, in contract, 0f, 0, new FixedString64Bytes("trade.deal.waiting_range"));
                    continue;
                }

                if (!TryExecuteTransferStep(ref contract, in config, out var transferred, out var valueMicros, out var failure))
                {
                    contract.LastFailure = failure;
                    if (IsRecoverableFailure(failure))
                    {
                        contract.Status = failure == Space4XTradeDealFailureReason.OutOfRange
                            ? Space4XTradeDealStatus.DeferredRange
                            : Space4XTradeDealStatus.DeferredBusy;
                        contract.NextAttemptTick = tick + math.max(1u, config.RetryBusyTicks);
                    }
                    else
                    {
                        contract.Status = Space4XTradeDealStatus.Failed;
                    }

                    contracts[i] = contract;
                    AppendEvent(ref events, tick, in contract, 0f, 0, ResolveFailureMessage(failure));
                    continue;
                }

                ApplyTradeRelationEffects(
                    state.EntityManager,
                    tick,
                    in contract,
                    transferred,
                    valueMicros,
                    in config,
                    in tierConfig);

                contract.QuantityRemaining = math.max(0f, contract.QuantityRemaining - transferred);
                contract.Status = contract.QuantityRemaining <= 0.001f
                    ? Space4XTradeDealStatus.Completed
                    : Space4XTradeDealStatus.Executing;
                contract.LastFailure = Space4XTradeDealFailureReason.None;
                contract.NextAttemptTick = tick + 1u;
                contracts[i] = contract;

                AppendEvent(ref events, tick, in contract, transferred, valueMicros,
                    contract.Status == Space4XTradeDealStatus.Completed
                        ? new FixedString64Bytes("trade.deal.completed")
                        : new FixedString64Bytes("trade.deal.partial"));
            }

            while (events.Length > 256)
            {
                events.RemoveAt(0);
            }
        }

        private bool TryExecuteTransferStep(ref Space4XTradeDealContract contract, in Space4XTradeDealConfig config, out float transferred, out long valueMicros, out Space4XTradeDealFailureReason failure)
        {
            transferred = 0f;
            valueMicros = 0;
            failure = Space4XTradeDealFailureReason.None;

            if (!_offerLookup.HasBuffer(contract.Seller))
            {
                failure = Space4XTradeDealFailureReason.OfferUnavailable;
                return false;
            }

            var offers = _offerLookup[contract.Seller];
            int offerIndex = FindOfferIndex(offers, contract.MarketResource);
            if (offerIndex < 0)
            {
                failure = Space4XTradeDealFailureReason.OfferUnavailable;
                return false;
            }

            var offer = offers[offerIndex];
            var step = math.min(math.max(0f, contract.QuantityRemaining), math.min(math.max(0.01f, config.TransferRatePerTick), math.max(0f, offer.Quantity)));
            if (step <= 0f)
            {
                failure = Space4XTradeDealFailureReason.OfferUnavailable;
                return false;
            }

            if (_storageLookup.HasBuffer(contract.Seller))
            {
                var sellerStorage = _storageLookup[contract.Seller];
                var available = SumStorage(sellerStorage, contract.CargoResource);
                step = math.min(step, available);
                if (step <= 0f)
                {
                    failure = Space4XTradeDealFailureReason.SellerSupplyUnavailable;
                    return false;
                }
            }

            if (!_storageLookup.HasBuffer(contract.Buyer))
            {
                failure = Space4XTradeDealFailureReason.BuyerCapacityUnavailable;
                return false;
            }

            var buyerStorage = _storageLookup[contract.Buyer];
            var buyerSlot = EnsureStorageSlot(ref buyerStorage, contract.CargoResource);
            var buyerEntry = buyerStorage[buyerSlot];
            var capacityRemaining = buyerEntry.GetRemainingCapacity();
            step = math.min(step, capacityRemaining);
            if (step <= 0f)
            {
                failure = Space4XTradeDealFailureReason.BuyerCapacityUnavailable;
                return false;
            }

            var unitPriceMicros = contract.UnitPriceMicros > 0 ? contract.UnitPriceMicros : Space4XMoneyMath.ToMicros(math.max(0.01f, offer.PricePerUnit));
            valueMicros = Space4XMoneyMath.ComputeLineMicros(step, unitPriceMicros);
            if (!TryTransferCurrency(contract.Buyer, contract.Seller, contract.CurrencyId, valueMicros))
            {
                failure = Space4XTradeDealFailureReason.BuyerFundsUnavailable;
                return false;
            }

            offer.Quantity = math.max(0f, offer.Quantity - step);
            if (offer.Quantity <= 0.001f)
            {
                offer.Quantity = 0f;
                offer.IsFulfilled = 1;
            }
            offers[offerIndex] = offer;

            if (_storageLookup.HasBuffer(contract.Seller))
            {
                var sellerStorage = _storageLookup[contract.Seller];
                if (!TryConsumeStorage(ref sellerStorage, contract.CargoResource, step, out _))
                {
                    failure = Space4XTradeDealFailureReason.SellerSupplyUnavailable;
                    return false;
                }
            }

            buyerEntry.Amount = math.min(buyerEntry.Capacity, buyerEntry.Amount + step);
            buyerStorage[buyerSlot] = buyerEntry;

            transferred = step;
            return true;
        }

        private void ApplyTradeRelationEffects(
            EntityManager entityManager,
            uint tick,
            in Space4XTradeDealContract contract,
            float transferred,
            long valueMicros,
            in Space4XTradeDealConfig config,
            in Space4XContactTierConfig tierConfig)
        {
            if (transferred <= 0f || valueMicros <= 0)
            {
                return;
            }

            var referenceMicros = contract.ReferenceUnitPriceMicros > 0 ? contract.ReferenceUnitPriceMicros : contract.UnitPriceMicros;
            if (referenceMicros <= 0)
            {
                return;
            }

            float priceOffset = ((float)contract.UnitPriceMicros - referenceMicros) / referenceMicros;
            float transferFraction = math.saturate(transferred / math.max(0.01f, contract.QuantityTotal));
            float valueUnits = math.max(0f, Space4XMoneyMath.FromMicros(valueMicros));
            float valueScale = math.max(0.2f, math.saturate(valueUnits / math.max(0.01f, config.RelationValueScaleUnits)));
            float buyerReactionScale = 1f + math.max(0f, contract.Desperation) * math.max(0f, (float)config.RelationDesperationScale);
            float stepScale = transferFraction * valueScale;

            int buyerDelta = QuantizeRelationDelta(
                ComputeTradeSentimentDelta(-priceOffset, buyerReactionScale, in config) * stepScale,
                config.RelationDeltaClampPerStep);
            int sellerDelta = QuantizeRelationDelta(
                ComputeTradeSentimentDelta(priceOffset, 1f, in config) * stepScale,
                config.RelationDeltaClampPerStep);

            EmitPersonalRelationInteraction(entityManager, contract.Buyer, contract.Seller, buyerDelta);
            EmitPersonalRelationInteraction(entityManager, contract.Seller, contract.Buyer, sellerDelta);

            if (!Space4XStandingUtility.TryResolveFaction(
                    contract.Buyer,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var buyerFaction,
                    out var buyerFactionId))
            {
                return;
            }

            if (!Space4XStandingUtility.TryResolveFaction(
                    contract.Seller,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var sellerFaction,
                    out var sellerFactionId))
            {
                return;
            }

            if (buyerFaction == Entity.Null ||
                sellerFaction == Entity.Null ||
                buyerFaction == sellerFaction ||
                buyerFactionId == 0 ||
                sellerFactionId == 0)
            {
                return;
            }

            ApplyFactionStandingDelta(entityManager, buyerFaction, sellerFaction, sellerFactionId, buyerDelta, tick, in tierConfig);
            ApplyFactionStandingDelta(entityManager, sellerFaction, buyerFaction, buyerFactionId, sellerDelta, tick, in tierConfig);
        }

        private static float ComputeTradeSentimentDelta(float favorability, float reactionScale, in Space4XTradeDealConfig config)
        {
            float magnitude = math.abs(favorability);
            float weight = favorability >= 0f
                ? math.max(0f, (float)config.RelationGoodPriceWeight)
                : math.max(0f, (float)config.RelationExtortionWeight);
            float signedPriceImpact = math.sign(favorability) * magnitude * weight;
            return ((float)config.RelationBaseGoodwill + signedPriceImpact) * math.max(0f, reactionScale);
        }

        private static int QuantizeRelationDelta(float delta, sbyte clampMagnitude)
        {
            int rounded = (int)math.round(delta);
            int clamp = math.max(0, (int)clampMagnitude);
            if (clamp == 0)
            {
                return rounded;
            }

            return math.clamp(rounded, -clamp, clamp);
        }

        private static void ApplyFactionStandingDelta(
            EntityManager entityManager,
            Entity observerFaction,
            Entity otherFaction,
            ushort otherFactionId,
            int relationDelta,
            uint tick,
            in Space4XContactTierConfig tierConfig)
        {
            if (observerFaction == Entity.Null || otherFactionId == 0 || relationDelta == 0)
            {
                return;
            }

            if (!entityManager.HasBuffer<FactionRelationEntry>(observerFaction))
            {
                entityManager.AddBuffer<FactionRelationEntry>(observerFaction);
            }

            var relationBuffer = entityManager.GetBuffer<FactionRelationEntry>(observerFaction);
            bool foundRelation = false;
            for (int i = 0; i < relationBuffer.Length; i++)
            {
                var entry = relationBuffer[i];
                if (entry.Relation.OtherFactionId != otherFactionId)
                {
                    continue;
                }

                var relation = entry.Relation;
                if (relation.OtherFaction == Entity.Null && otherFaction != Entity.Null)
                {
                    relation.OtherFaction = otherFaction;
                }
                relation.Score = (sbyte)math.clamp((int)relation.Score + relationDelta, -100, 100);
                relation.LastInteractionTick = tick;
                entry.Relation = relation;
                relationBuffer[i] = entry;
                foundRelation = true;
                break;
            }

            if (!foundRelation)
            {
                relationBuffer.Add(new FactionRelationEntry
                {
                    Relation = new FactionRelation
                    {
                        OtherFaction = otherFaction,
                        OtherFactionId = otherFactionId,
                        Score = (sbyte)math.clamp(relationDelta, -100, 100),
                        Trust = (half)0f,
                        Fear = (half)0f,
                        Respect = (half)0f,
                        TradeVolume = 0f,
                        RecentCombats = 0,
                        LastInteractionTick = tick
                    }
                });
            }

            if (!entityManager.HasBuffer<Space4XContactStanding>(observerFaction))
            {
                entityManager.AddBuffer<Space4XContactStanding>(observerFaction);
            }

            var standingBuffer = entityManager.GetBuffer<Space4XContactStanding>(observerFaction);
            float standingDelta = relationDelta / 200f;
            for (int i = 0; i < standingBuffer.Length; i++)
            {
                if (standingBuffer[i].ContactFactionId != otherFactionId)
                {
                    continue;
                }

                var entry = standingBuffer[i];
                float nextStanding = math.saturate((float)entry.Standing + standingDelta);
                entry.Standing = (half)nextStanding;
                entry.Tier = Space4XContactTierUtility.ResolveTier(nextStanding, tierConfig);
                standingBuffer[i] = entry;
                return;
            }

            float baseStanding = math.saturate(0.2f + standingDelta);
            standingBuffer.Add(new Space4XContactStanding
            {
                ContactFactionId = otherFactionId,
                Standing = (half)baseStanding,
                LoyaltyPoints = 0f,
                Tier = Space4XContactTierUtility.ResolveTier(baseStanding, tierConfig)
            });
        }

        private static void EmitPersonalRelationInteraction(
            EntityManager entityManager,
            Entity source,
            Entity target,
            int relationDelta)
        {
            if (source == Entity.Null || target == Entity.Null || relationDelta == 0)
            {
                return;
            }

            if (!entityManager.HasBuffer<EntityRelation>(source))
            {
                entityManager.AddBuffer<EntityRelation>(source);
            }

            if (!entityManager.HasBuffer<EntityRelation>(target))
            {
                entityManager.AddBuffer<EntityRelation>(target);
            }

            var requestEntity = entityManager.CreateEntity(typeof(RecordInteractionRequest));
            var magnitude = math.abs(relationDelta);
            var outcome = relationDelta >= 0
                ? (magnitude >= 5 ? InteractionOutcome.VeryPositive : InteractionOutcome.Positive)
                : (magnitude >= 5 ? InteractionOutcome.VeryNegative : InteractionOutcome.Negative);

            entityManager.SetComponentData(requestEntity, new RecordInteractionRequest
            {
                EntityA = source,
                EntityB = target,
                Outcome = outcome,
                IntensityChange = (sbyte)math.clamp(relationDelta * 4, -40, 40),
                TrustChange = (sbyte)math.clamp(relationDelta * 2, -20, 20),
                IsMutual = false
            });
        }

        private bool IsBusy(Entity entity)
        {
            if (entity == Entity.Null)
            {
                return true;
            }

            if (_inCombatLookup.HasComponent(entity))
            {
                return true;
            }

            if (_miningLookup.HasComponent(entity) && _miningLookup[entity].Phase != MiningPhase.Idle)
            {
                return true;
            }

            if (_captainOrderLookup.HasComponent(entity))
            {
                var order = _captainOrderLookup[entity];
                if (order.Status == CaptainOrderStatus.Executing &&
                    order.Type != CaptainOrderType.Trade &&
                    order.Type != CaptainOrderType.Standby)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsWithinRange(Entity buyer, Entity seller, float maxRange)
        {
            if (!_transformLookup.HasComponent(buyer) || !_transformLookup.HasComponent(seller))
            {
                return false;
            }

            var buyerPos = _transformLookup[buyer].Position;
            var sellerPos = _transformLookup[seller].Position;
            return math.distancesq(buyerPos, sellerPos) <= maxRange * maxRange;
        }

        private bool TryTransferCurrency(Entity buyer, Entity seller, Space4XCurrencyId currencyId, long amountMicros)
        {
            if (amountMicros <= 0 || buyer == seller)
            {
                return true;
            }

            if (!_currencyLookup.HasBuffer(buyer) || !_currencyLookup.HasBuffer(seller))
            {
                return false;
            }

            if (!CanDebitCurrency(buyer, currencyId, amountMicros))
            {
                return false;
            }

            if (!TryApplyCurrencyDelta(buyer, currencyId, -amountMicros, false))
            {
                return false;
            }

            if (!TryApplyCurrencyDelta(seller, currencyId, amountMicros, true))
            {
                TryApplyCurrencyDelta(buyer, currencyId, amountMicros, true);
                return false;
            }

            return true;
        }

        private bool CanDebitCurrency(Entity actor, Space4XCurrencyId currencyId, long amountMicros)
        {
            var balances = _currencyLookup[actor];
            for (int i = 0; i < balances.Length; i++)
            {
                if (balances[i].CurrencyId != currencyId)
                {
                    continue;
                }

                return balances[i].AmountMicros >= amountMicros;
            }

            return false;
        }

        private bool TryApplyCurrencyDelta(Entity actor, Space4XCurrencyId currencyId, long deltaMicros, bool allowCreate)
        {
            if (!_currencyLookup.HasBuffer(actor))
            {
                return false;
            }

            var balances = _currencyLookup[actor];
            for (int i = 0; i < balances.Length; i++)
            {
                if (balances[i].CurrencyId != currencyId)
                {
                    continue;
                }

                var value = balances[i];
                var next = value.AmountMicros + deltaMicros;
                if (next < 0)
                {
                    return false;
                }

                value.AmountMicros = next;
                balances[i] = value;
                return true;
            }

            if (!allowCreate || deltaMicros < 0)
            {
                return false;
            }

            balances.Add(new Space4XCurrencyBalance
            {
                CurrencyId = currencyId,
                AmountMicros = deltaMicros
            });
            return true;
        }

        private static int FindOfferIndex(DynamicBuffer<TradeOffer> offers, MarketResourceType resource)
        {
            int best = -1;
            float bestPrice = float.MaxValue;
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (offer.IsFulfilled != 0 || offer.Type != TradeOfferType.Sell || offer.ResourceType != resource || offer.Quantity <= 0f)
                {
                    continue;
                }

                if (offer.PricePerUnit < bestPrice)
                {
                    bestPrice = offer.PricePerUnit;
                    best = i;
                }
            }

            return best;
        }

        private static float SumStorage(DynamicBuffer<ResourceStorage> storage, ResourceType type)
        {
            float sum = 0f;
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type == type)
                {
                    sum += math.max(0f, storage[i].Amount);
                }
            }

            return sum;
        }

        private static int EnsureStorageSlot(ref DynamicBuffer<ResourceStorage> storage, ResourceType type)
        {
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type == type)
                {
                    return i;
                }
            }

            storage.Add(ResourceStorage.Create(type));
            return storage.Length - 1;
        }

        private static bool TryConsumeStorage(ref DynamicBuffer<ResourceStorage> storage, ResourceType type, float amount, out float consumed)
        {
            consumed = 0f;
            if (amount <= 0f)
            {
                return true;
            }

            for (int i = 0; i < storage.Length && consumed + 0.0001f < amount; i++)
            {
                if (storage[i].Type != type)
                {
                    continue;
                }

                var entry = storage[i];
                var take = math.min(math.max(0f, entry.Amount), amount - consumed);
                if (take <= 0f)
                {
                    continue;
                }

                entry.Amount -= take;
                storage[i] = entry;
                consumed += take;
            }

            return consumed + 0.0001f >= amount;
        }

        private static bool IsRecoverableFailure(Space4XTradeDealFailureReason failure)
        {
            return failure == Space4XTradeDealFailureReason.Busy ||
                   failure == Space4XTradeDealFailureReason.OutOfRange ||
                   failure == Space4XTradeDealFailureReason.SellerSupplyUnavailable ||
                   failure == Space4XTradeDealFailureReason.BuyerCapacityUnavailable ||
                   failure == Space4XTradeDealFailureReason.BuyerFundsUnavailable;
        }

        private static FixedString64Bytes ResolveFailureMessage(Space4XTradeDealFailureReason failure)
        {
            return failure switch
            {
                Space4XTradeDealFailureReason.Busy => new FixedString64Bytes("trade.deal.deferred_busy"),
                Space4XTradeDealFailureReason.OutOfRange => new FixedString64Bytes("trade.deal.waiting_range"),
                Space4XTradeDealFailureReason.OfferUnavailable => new FixedString64Bytes("trade.deal.offer_unavailable"),
                Space4XTradeDealFailureReason.SellerSupplyUnavailable => new FixedString64Bytes("trade.deal.seller_empty"),
                Space4XTradeDealFailureReason.BuyerCapacityUnavailable => new FixedString64Bytes("trade.deal.buyer_full"),
                Space4XTradeDealFailureReason.BuyerFundsUnavailable => new FixedString64Bytes("trade.deal.insufficient_funds"),
                Space4XTradeDealFailureReason.CurrencyTransferFailed => new FixedString64Bytes("trade.deal.currency_failed"),
                Space4XTradeDealFailureReason.InvalidEntity => new FixedString64Bytes("trade.deal.invalid_entity"),
                Space4XTradeDealFailureReason.Expired => new FixedString64Bytes("trade.deal.expired"),
                _ => new FixedString64Bytes("trade.deal.failed")
            };
        }

        private static void AppendEvent(ref DynamicBuffer<Space4XTradeDealEvent> events, uint tick, in Space4XTradeDealContract contract, float quantityDelta, long valueMicros, in FixedString64Bytes message)
        {
            events.Add(new Space4XTradeDealEvent
            {
                Tick = tick,
                DealId = contract.DealId,
                Status = contract.Status,
                Failure = contract.LastFailure,
                Buyer = contract.Buyer,
                Seller = contract.Seller,
                MarketResource = contract.MarketResource,
                QuantityDelta = quantityDelta,
                QuantityRemaining = contract.QuantityRemaining,
                ValueMicros = valueMicros,
                MessageId = message
            });
        }
    }
}
