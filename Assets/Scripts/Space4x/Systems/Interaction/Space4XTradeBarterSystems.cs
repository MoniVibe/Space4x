using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Economy.Resources;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Interaction
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XTradeBarterBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            EnsureState(ref state);
            EnsureConfig(ref state);
            EnsureFeePolicy(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        private static void EnsureState(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XTradeBarterState>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XTradeBarterState));
            state.EntityManager.SetComponentData(entity, new Space4XTradeBarterState
            {
                IsOpen = 0,
                SessionId = 0,
                LastProcessedGateTick = 0,
                EntryPointSlot = 0,
                ContextEntity = Entity.Null,
                PartyA = Entity.Null,
                PartyB = Entity.Null,
                PartyAInventoryEntity = Entity.Null,
                PartyBInventoryEntity = Entity.Null,
                PartyAFactionId = 0,
                PartyBFactionId = 0
            });
            state.EntityManager.AddBuffer<Space4XTradeBarterViewEntry>(entity);
            state.EntityManager.AddBuffer<Space4XTradeBarterOfferEntry>(entity);
            state.EntityManager.AddBuffer<Space4XTradeBarterCommand>(entity);
            state.EntityManager.AddBuffer<Space4XTradeBarterEvent>(entity);
            state.EntityManager.AddBuffer<Space4XTradeBarterResolvedLine>(entity);
            state.EntityManager.AddBuffer<Space4XEconomyLedgerEvent>(entity);
        }

        private static void EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XTradeBarterConfig>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XTradeBarterConfig));
            state.EntityManager.SetComponentData(entity, Space4XTradeBarterConfig.Default);
        }

        private static void EnsureFeePolicy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XTradeFeePolicyConfig>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XTradeFeePolicyConfig));
            state.EntityManager.SetComponentData(entity, Space4XTradeFeePolicyConfig.Default);
        }
    }

    /// <summary>
    /// Opens/closes barter sessions from accepted trade gate outcomes.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XInteractionGateResolveSystem))]
    [UpdateBefore(typeof(Space4XTradeBarterCommandSystem))]
    public partial struct Space4XTradeBarterFromGateSystem : ISystem
    {
        private ComponentLookup<BusinessInventory> _businessInventoryLookup;
        private ComponentLookup<Inventory> _inventoryLookup;
        private ComponentLookup<ColonyIndustryInventory> _colonyInventoryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTradeBarterState>();
            state.RequireForUpdate<Space4XInteractionGateState>();
            _businessInventoryLookup = state.GetComponentLookup<BusinessInventory>(true);
            _inventoryLookup = state.GetComponentLookup<Inventory>(true);
            _colonyInventoryLookup = state.GetComponentLookup<ColonyIndustryInventory>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _businessInventoryLookup.Update(ref state);
            _inventoryLookup.Update(ref state);
            _colonyInventoryLookup.Update(ref state);

            var barterEntity = SystemAPI.GetSingletonEntity<Space4XTradeBarterState>();
            var barterState = state.EntityManager.GetComponentData<Space4XTradeBarterState>(barterEntity);
            var barterView = state.EntityManager.GetBuffer<Space4XTradeBarterViewEntry>(barterEntity);
            var barterOffers = state.EntityManager.GetBuffer<Space4XTradeBarterOfferEntry>(barterEntity);
            var barterCommands = state.EntityManager.GetBuffer<Space4XTradeBarterCommand>(barterEntity);
            var barterEvents = state.EntityManager.GetBuffer<Space4XTradeBarterEvent>(barterEntity);
            var resolvedLines = state.EntityManager.GetBuffer<Space4XTradeBarterResolvedLine>(barterEntity);

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateEvents = state.EntityManager.GetBuffer<Space4XYarnGateEvent>(gateEntity);
            if (gateEvents.Length == 0)
            {
                return;
            }

            var dirty = false;
            for (int i = gateEvents.Length - 1; i >= 0; i--)
            {
                var gateEvent = gateEvents[i];
                if (gateEvent.Kind != Space4XInteractionGateKind.Trade)
                {
                    continue;
                }

                if (gateEvent.Tick != 0 && gateEvent.Tick <= barterState.LastProcessedGateTick)
                {
                    continue;
                }

                if (gateEvent.Tick != 0)
                {
                    barterState.LastProcessedGateTick = gateEvent.Tick;
                }
                dirty = true;

                if (gateEvent.Accepted == 0)
                {
                    continue;
                }

                if (gateEvent.Slot == 5)
                {
                    var closingSnapshot = barterState;
                    CloseSession(ref barterState, ref barterView, ref barterOffers, ref barterCommands, ref resolvedLines);
                    closingSnapshot.IsOpen = 0;
                    AppendEvent(ref barterEvents, in closingSnapshot, Space4XTradeBarterStatus.Cancelled, 0f, 0f, new FixedString128Bytes("trade.barter.closed"));
                    break;
                }

                var partyA = gateEvent.Actor;
                var partyB = gateEvent.Target != Entity.Null ? gateEvent.Target : gateEvent.ContextEntity;
                if (partyA == Entity.Null || partyB == Entity.Null)
                {
                    continue;
                }

                barterState.IsOpen = 1;
                barterState.EntryPointSlot = gateEvent.Slot;
                barterState.ContextEntity = gateEvent.ContextEntity;
                barterState.PartyA = partyA;
                barterState.PartyB = partyB;
                barterState.PartyAInventoryEntity = ResolveInventoryEntity(partyA);
                barterState.PartyBInventoryEntity = ResolveInventoryEntity(partyB);
                barterState.PartyAFactionId = 0;
                barterState.PartyBFactionId = 0;
                barterState.SessionId = BuildSessionId(gateEvent.Tick, partyA, partyB);

                barterView.Clear();
                barterOffers.Clear();
                barterCommands.Clear();
                resolvedLines.Clear();
                AppendEvent(ref barterEvents, in barterState, Space4XTradeBarterStatus.Opened, 0f, 0f, new FixedString128Bytes("trade.barter.opened"));
                break;
            }

            if (dirty)
            {
                state.EntityManager.SetComponentData(barterEntity, barterState);
            }
        }

        private Entity ResolveInventoryEntity(Entity owner)
        {
            if (owner == Entity.Null)
            {
                return Entity.Null;
            }

            if (_businessInventoryLookup.HasComponent(owner))
            {
                var inventoryEntity = _businessInventoryLookup[owner].InventoryEntity;
                if (inventoryEntity != Entity.Null)
                {
                    return inventoryEntity;
                }
            }

            if (_colonyInventoryLookup.HasComponent(owner))
            {
                var inventoryEntity = _colonyInventoryLookup[owner].InventoryEntity;
                if (inventoryEntity != Entity.Null)
                {
                    return inventoryEntity;
                }
            }

            return _inventoryLookup.HasComponent(owner) ? owner : Entity.Null;
        }

        private static uint BuildSessionId(uint tick, Entity partyA, Entity partyB)
        {
            uint safeTick = tick != 0 ? tick : 1u;
            uint a = (uint)math.max(partyA.Index, 0);
            uint b = (uint)math.max(partyB.Index, 0);
            return math.hash(new uint4(safeTick, a + 17u, b + 31u, (uint)partyA.Version + ((uint)partyB.Version << 16)));
        }

        private static void CloseSession(
            ref Space4XTradeBarterState state,
            ref DynamicBuffer<Space4XTradeBarterViewEntry> view,
            ref DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            ref DynamicBuffer<Space4XTradeBarterCommand> commands,
            ref DynamicBuffer<Space4XTradeBarterResolvedLine> resolved)
        {
            state.IsOpen = 0;
            state.EntryPointSlot = 0;
            state.SessionId = 0;
            state.ContextEntity = Entity.Null;
            state.PartyA = Entity.Null;
            state.PartyB = Entity.Null;
            state.PartyAInventoryEntity = Entity.Null;
            state.PartyBInventoryEntity = Entity.Null;
            state.PartyAFactionId = 0;
            state.PartyBFactionId = 0;
            view.Clear();
            offers.Clear();
            commands.Clear();
            resolved.Clear();
        }

        private static void AppendEvent(
            ref DynamicBuffer<Space4XTradeBarterEvent> events,
            in Space4XTradeBarterState state,
            Space4XTradeBarterStatus status,
            float valueA,
            float valueB,
            in FixedString128Bytes message)
        {
            events.Add(new Space4XTradeBarterEvent
            {
                SessionId = state.SessionId,
                Status = status,
                ValueOfferedByAMicros = Space4XMoneyMath.ToMicros(valueA),
                ValueOfferedByBMicros = Space4XMoneyMath.ToMicros(valueB),
                NetDeltaMicros = Space4XMoneyMath.ToMicros(valueA - valueB),
                FeePaidByAMicros = 0,
                FeePaidByBMicros = 0,
                ValueOfferedByA = valueA,
                ValueOfferedByB = valueB,
                NetDelta = valueA - valueB,
                IsOpenAfter = state.IsOpen,
                Message = message
            });
        }
    }

    /// <summary>
    /// Applies offer/commit/cancel commands to the active barter session.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XTradeBarterFromGateSystem))]
    [UpdateBefore(typeof(Space4XTradeBarterViewBuildSystem))]
    public partial struct Space4XTradeBarterCommandSystem : ISystem
    {
        private BufferLookup<InventoryItem> _inventoryItemsLookup;
        private BufferLookup<ResourceStorage> _cargoLookup;
        private BufferLookup<TradeOffer> _tradeOfferLookup;
        private BufferLookup<MarketPriceEntry> _priceLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTradeBarterState>();
            state.RequireForUpdate<Space4XTradeBarterConfig>();
            _inventoryItemsLookup = state.GetBufferLookup<InventoryItem>(true);
            _cargoLookup = state.GetBufferLookup<ResourceStorage>(true);
            _tradeOfferLookup = state.GetBufferLookup<TradeOffer>(true);
            _priceLookup = state.GetBufferLookup<MarketPriceEntry>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _inventoryItemsLookup.Update(ref state);
            _cargoLookup.Update(ref state);
            _tradeOfferLookup.Update(ref state);
            _priceLookup.Update(ref state);

            var config = SystemAPI.GetSingleton<Space4XTradeBarterConfig>();
            var barterEntity = SystemAPI.GetSingletonEntity<Space4XTradeBarterState>();
            var barterState = state.EntityManager.GetComponentData<Space4XTradeBarterState>(barterEntity);
            var commands = state.EntityManager.GetBuffer<Space4XTradeBarterCommand>(barterEntity);
            if (commands.Length == 0)
            {
                return;
            }

            var offers = state.EntityManager.GetBuffer<Space4XTradeBarterOfferEntry>(barterEntity);
            var events = state.EntityManager.GetBuffer<Space4XTradeBarterEvent>(barterEntity);
            var view = state.EntityManager.GetBuffer<Space4XTradeBarterViewEntry>(barterEntity);
            var resolved = state.EntityManager.GetBuffer<Space4XTradeBarterResolvedLine>(barterEntity);

            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (barterState.IsOpen == 0)
                {
                    continue;
                }

                if (command.SessionId != 0 && command.SessionId != barterState.SessionId)
                {
                    AppendEvent(ref events, in barterState, Space4XTradeBarterStatus.RejectedInvalidSession, 0f, 0f, new FixedString128Bytes("trade.barter.invalid_session"));
                    continue;
                }

                switch (command.Kind)
                {
                    case Space4XTradeBarterCommandKind.Offer:
                        ApplyOfferDelta(in barterState, ref offers, ref events, in command, math.abs(command.Quantity), false);
                        break;

                    case Space4XTradeBarterCommandKind.Retract:
                        ApplyOfferDelta(in barterState, ref offers, ref events, in command, math.abs(command.Quantity), true);
                        break;

                    case Space4XTradeBarterCommandKind.ClearOffers:
                        if (command.Side == Space4XTradeBarterSide.PartyA || command.Side == Space4XTradeBarterSide.PartyB)
                        {
                            ClearOffers(ref offers, command.Side);
                        }
                        else
                        {
                            offers.Clear();
                        }
                        break;

                    case Space4XTradeBarterCommandKind.Cancel:
                        var closingSnapshot = barterState;
                        CloseSession(ref barterState, ref view, ref offers, ref commands, ref resolved);
                        closingSnapshot.IsOpen = 0;
                        AppendEvent(ref events, in closingSnapshot, Space4XTradeBarterStatus.Cancelled, 0f, 0f, new FixedString128Bytes("trade.barter.cancelled"));
                        break;

                    case Space4XTradeBarterCommandKind.Commit:
                        CommitOffers(ref barterState, ref offers, ref events, ref resolved, in config);
                        break;
                }
            }

            commands.Clear();
            state.EntityManager.SetComponentData(barterEntity, barterState);
        }

        private void ApplyOfferDelta(
            in Space4XTradeBarterState state,
            ref DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            ref DynamicBuffer<Space4XTradeBarterEvent> events,
            in Space4XTradeBarterCommand command,
            float quantityDelta,
            bool retract)
        {
            if (quantityDelta <= 0f)
            {
                return;
            }

            var index = FindOfferIndex(offers, in command);
            if (retract)
            {
                if (index < 0)
                {
                    return;
                }

                var entry = offers[index];
                entry.Quantity = math.max(0f, entry.Quantity - quantityDelta);
                if (entry.Quantity <= 0.0001f)
                {
                    offers.RemoveAt(index);
                }
                else
                {
                    offers[index] = entry;
                }
                return;
            }

            if (!TryResolveAvailabilityAndPricing(in state, in command, out var available, out var unitValue, out var quality, out var durability))
            {
                AppendEvent(ref events, in state, Space4XTradeBarterStatus.RejectedAvailability, 0f, 0f, new FixedString128Bytes("trade.barter.unavailable"));
                return;
            }

            var currencyId = command.CurrencyId != Space4XCurrencyId.None
                ? command.CurrencyId
                : Space4XCurrencyId.Credits;
            var unitPriceMicros = Space4XMoneyMath.ToMicros(math.max(0f, unitValue));

            var alreadyOffered = SumOffered(offers, in command);
            var remaining = math.max(0f, available - alreadyOffered);
            if (remaining <= 0f)
            {
                AppendEvent(ref events, in state, Space4XTradeBarterStatus.RejectedAvailability, 0f, 0f, new FixedString128Bytes("trade.barter.no_remaining_quantity"));
                return;
            }

            var add = math.min(quantityDelta, remaining);
            if (add <= 0f)
            {
                return;
            }

            if (index < 0)
            {
                offers.Add(new Space4XTradeBarterOfferEntry
                {
                    Side = command.Side,
                    SourceKind = command.SourceKind,
                    ItemId = command.ItemId,
                    Quantity = add,
                    CurrencyId = currencyId,
                    UnitPriceMicros = unitPriceMicros,
                    UnitValue = unitValue,
                    Quality = quality,
                    Durability = durability,
                    CargoResourceType = command.CargoResourceType,
                    MarketResourceType = command.MarketResourceType
                });
                return;
            }

            var updated = offers[index];
            updated.Quantity += add;
            updated.CurrencyId = currencyId;
            updated.UnitPriceMicros = unitPriceMicros;
            updated.UnitValue = unitValue;
            updated.Quality = quality;
            updated.Durability = durability;
            offers[index] = updated;
        }

        private void CommitOffers(
            ref Space4XTradeBarterState state,
            ref DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            ref DynamicBuffer<Space4XTradeBarterEvent> events,
            ref DynamicBuffer<Space4XTradeBarterResolvedLine> resolved,
            in Space4XTradeBarterConfig config)
        {
            if (offers.Length == 0)
            {
                AppendEvent(ref events, in state, Space4XTradeBarterStatus.RejectedValueMismatch, 0f, 0f, new FixedString128Bytes("trade.barter.empty_offer"));
                return;
            }

            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                var commandLike = new Space4XTradeBarterCommand
                {
                    Side = offer.Side,
                    SourceKind = offer.SourceKind,
                    ItemId = offer.ItemId,
                    CargoResourceType = offer.CargoResourceType,
                    MarketResourceType = offer.MarketResourceType
                };

                if (!TryResolveAvailabilityAndPricing(in state, in commandLike, out var available, out _, out _, out _))
                {
                    AppendEvent(ref events, in state, Space4XTradeBarterStatus.RejectedAvailability, 0f, 0f, new FixedString128Bytes("trade.barter.unavailable_during_commit"));
                    return;
                }

                var required = SumOffered(offers, in commandLike);
                if (required > available + 0.0001f)
                {
                    AppendEvent(ref events, in state, Space4XTradeBarterStatus.RejectedAvailability, 0f, 0f, new FixedString128Bytes("trade.barter.quantity_changed"));
                    return;
                }
            }

            long valueAMicros = 0;
            long valueBMicros = 0;
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                var unitPriceMicros = ResolveUnitPriceMicros(offer.UnitPriceMicros, offer.UnitValue);
                var lineValueMicros = Space4XMoneyMath.ComputeLineMicros(offer.Quantity, unitPriceMicros);
                if (offer.Side == Space4XTradeBarterSide.PartyA)
                {
                    valueAMicros += lineValueMicros;
                }
                else
                {
                    valueBMicros += lineValueMicros;
                }
            }

            if (config.RequireBalancedValue != 0)
            {
                var toleranceBps = config.RelativeValueToleranceBps != 0
                    ? config.RelativeValueToleranceBps
                    : (ushort)math.max(0, (int)math.round(config.RelativeValueTolerance * 10000f));
                var minimumToleranceMicros = config.MinimumAbsoluteToleranceMicros > 0
                    ? config.MinimumAbsoluteToleranceMicros
                    : Space4XMoneyMath.ToMicros(config.MinimumAbsoluteTolerance);
                var relativeToleranceMicros = (long)math.round(math.max(valueAMicros, valueBMicros) * (double)toleranceBps / 10000d);
                var toleranceMicros = math.max(minimumToleranceMicros, relativeToleranceMicros);
                var deltaMicros = (long)math.abs((double)(valueAMicros - valueBMicros));
                if (deltaMicros > toleranceMicros)
                {
                    AppendEvent(
                        ref events,
                        in state,
                        Space4XTradeBarterStatus.RejectedValueMismatch,
                        Space4XMoneyMath.FromMicros(valueAMicros),
                        Space4XMoneyMath.FromMicros(valueBMicros),
                        new FixedString128Bytes("trade.barter.value_mismatch"));
                    return;
                }
            }

            resolved.Clear();
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                resolved.Add(new Space4XTradeBarterResolvedLine
                {
                    SessionId = state.SessionId,
                    Side = offer.Side,
                    SourceKind = offer.SourceKind,
                    ItemId = offer.ItemId,
                    Quantity = offer.Quantity,
                    CurrencyId = offer.CurrencyId != Space4XCurrencyId.None ? offer.CurrencyId : config.CurrencyId,
                    UnitPriceMicros = ResolveUnitPriceMicros(offer.UnitPriceMicros, offer.UnitValue),
                    UnitValue = offer.UnitValue,
                    Quality = offer.Quality,
                    Durability = offer.Durability,
                    CargoResourceType = offer.CargoResourceType,
                    MarketResourceType = offer.MarketResourceType
                });
            }

            offers.Clear();
            if (config.AutoCloseOnCommit != 0)
            {
                state.IsOpen = 0;
            }
            AppendEvent(
                ref events,
                in state,
                Space4XTradeBarterStatus.Accepted,
                Space4XMoneyMath.FromMicros(valueAMicros),
                Space4XMoneyMath.FromMicros(valueBMicros),
                new FixedString128Bytes("trade.barter.accepted"));
        }

        private static long ResolveUnitPriceMicros(long unitPriceMicros, float fallbackUnitValue)
        {
            return unitPriceMicros > 0
                ? unitPriceMicros
                : math.max(1L, Space4XMoneyMath.ToMicros(math.max(0f, fallbackUnitValue)));
        }

        private static int FindOfferIndex(DynamicBuffer<Space4XTradeBarterOfferEntry> offers, in Space4XTradeBarterCommand command)
        {
            for (int i = 0; i < offers.Length; i++)
            {
                if (!IsSameOfferKey(in offers[i], in command))
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        private static bool IsSameOfferKey(in Space4XTradeBarterOfferEntry offer, in Space4XTradeBarterCommand command)
        {
            return offer.Side == command.Side &&
                   offer.SourceKind == command.SourceKind &&
                   offer.ItemId.Equals(command.ItemId) &&
                   offer.CargoResourceType == command.CargoResourceType &&
                   offer.MarketResourceType == command.MarketResourceType;
        }

        private static float SumOffered(DynamicBuffer<Space4XTradeBarterOfferEntry> offers, in Space4XTradeBarterCommand command)
        {
            float sum = 0f;
            for (int i = 0; i < offers.Length; i++)
            {
                if (IsSameOfferKey(in offers[i], in command))
                {
                    sum += math.max(0f, offers[i].Quantity);
                }
            }

            return sum;
        }

        private void ClearOffers(ref DynamicBuffer<Space4XTradeBarterOfferEntry> offers, Space4XTradeBarterSide side)
        {
            for (int i = offers.Length - 1; i >= 0; i--)
            {
                if (offers[i].Side == side)
                {
                    offers.RemoveAt(i);
                }
            }
        }

        private bool TryResolveAvailabilityAndPricing(
            in Space4XTradeBarterState state,
            in Space4XTradeBarterCommand command,
            out float available,
            out float unitValue,
            out float quality,
            out float durability)
        {
            available = 0f;
            unitValue = 1f;
            quality = 1f;
            durability = 1f;

            var sideEntity = ResolveSideEntity(in state, command.Side);
            var inventoryEntity = ResolveSideInventoryEntity(in state, command.Side);

            switch (command.SourceKind)
            {
                case Space4XTradeBarterSourceKind.InventoryItem:
                    if (inventoryEntity == Entity.Null || !_inventoryItemsLookup.HasBuffer(inventoryEntity))
                    {
                        return false;
                    }

                    var items = _inventoryItemsLookup[inventoryEntity];
                    var weightedQuality = 0f;
                    var weightedDurability = 0f;
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (!items[i].ItemId.Equals(command.ItemId))
                        {
                            continue;
                        }

                        var qty = math.max(0f, items[i].Quantity);
                        available += qty;
                        weightedQuality += qty * math.max(0f, items[i].Quality);
                        weightedDurability += qty * math.max(0f, items[i].Durability);
                    }

                    if (available <= 0f)
                    {
                        return false;
                    }

                    quality = weightedQuality / available;
                    durability = weightedDurability / available;
                    unitValue = ResolveItemUnitValue(in state, command.ItemId, command.Side);
                    return true;

                case Space4XTradeBarterSourceKind.CargoResource:
                    if (sideEntity == Entity.Null || !_cargoLookup.HasBuffer(sideEntity))
                    {
                        return false;
                    }

                    var storage = _cargoLookup[sideEntity];
                    for (int i = 0; i < storage.Length; i++)
                    {
                        if (storage[i].Type != command.CargoResourceType)
                        {
                            continue;
                        }

                        available += math.max(0f, storage[i].Amount);
                    }

                    if (available <= 0f)
                    {
                        return false;
                    }

                    unitValue = ResolveCargoUnitValue(in state, command.CargoResourceType, command.Side);
                    quality = 1f;
                    durability = 1f;
                    return true;

                case Space4XTradeBarterSourceKind.MarketSellOffer:
                case Space4XTradeBarterSourceKind.MarketBuyDemand:
                    if (sideEntity == Entity.Null || !_tradeOfferLookup.HasBuffer(sideEntity))
                    {
                        return false;
                    }

                    var offers = _tradeOfferLookup[sideEntity];
                    float weightedPrice = 0f;
                    var offerType = command.SourceKind == Space4XTradeBarterSourceKind.MarketSellOffer
                        ? TradeOfferType.Sell
                        : TradeOfferType.Buy;

                    for (int i = 0; i < offers.Length; i++)
                    {
                        var offer = offers[i];
                        if (offer.IsFulfilled != 0 || offer.Type != offerType || offer.ResourceType != command.MarketResourceType)
                        {
                            continue;
                        }

                        var qty = math.max(0f, offer.Quantity);
                        available += qty;
                        weightedPrice += qty * math.max(0.01f, offer.PricePerUnit);
                    }

                    if (available <= 0f)
                    {
                        return false;
                    }

                    unitValue = weightedPrice / available;
                    quality = 1f;
                    durability = 1f;
                    return true;

                default:
                    return false;
            }
        }

        private float ResolveItemUnitValue(in Space4XTradeBarterState state, in FixedString64Bytes itemId, Space4XTradeBarterSide side)
        {
            if (TryResolveMarketEntity(in state, side, out var marketEntity))
            {
                if (_priceLookup.HasBuffer(marketEntity))
                {
                    var prices = _priceLookup[marketEntity];
                    if (TryMapItemIdToMarketResource(itemId, out var resource))
                    {
                        if (TryGetMarketMidPrice(prices, resource, out var price))
                        {
                            return price;
                        }
                    }
                }
            }

            return 1f;
        }

        private float ResolveCargoUnitValue(in Space4XTradeBarterState state, ResourceType resource, Space4XTradeBarterSide side)
        {
            if (!TryMapResourceToMarket(resource, out var marketResource))
            {
                return 1f;
            }

            if (TryResolveMarketEntity(in state, side, out var marketEntity))
            {
                if (_priceLookup.HasBuffer(marketEntity))
                {
                    var prices = _priceLookup[marketEntity];
                    if (TryGetMarketMidPrice(prices, marketResource, out var price))
                    {
                        return price;
                    }
                }
            }

            return 1f;
        }

        private bool TryResolveMarketEntity(in Space4XTradeBarterState state, Space4XTradeBarterSide side, out Entity marketEntity)
        {
            var opposite = side == Space4XTradeBarterSide.PartyA ? state.PartyB : state.PartyA;
            if (opposite != Entity.Null && _priceLookup.HasBuffer(opposite))
            {
                marketEntity = opposite;
                return true;
            }

            if (state.ContextEntity != Entity.Null && _priceLookup.HasBuffer(state.ContextEntity))
            {
                marketEntity = state.ContextEntity;
                return true;
            }

            marketEntity = Entity.Null;
            return false;
        }

        private static Entity ResolveSideEntity(in Space4XTradeBarterState state, Space4XTradeBarterSide side)
        {
            return side == Space4XTradeBarterSide.PartyA ? state.PartyA : state.PartyB;
        }

        private static Entity ResolveSideInventoryEntity(in Space4XTradeBarterState state, Space4XTradeBarterSide side)
        {
            return side == Space4XTradeBarterSide.PartyA ? state.PartyAInventoryEntity : state.PartyBInventoryEntity;
        }

        private static bool TryMapResourceToMarket(ResourceType resource, out MarketResourceType marketResource)
        {
            switch (resource)
            {
                case ResourceType.Ore:
                    marketResource = MarketResourceType.Ore;
                    return true;
                case ResourceType.Minerals:
                    marketResource = MarketResourceType.RefinedMetal;
                    return true;
                case ResourceType.RareMetals:
                case ResourceType.TransplutonicOre:
                    marketResource = MarketResourceType.RareEarth;
                    return true;
                case ResourceType.EnergyCrystals:
                case ResourceType.Fuel:
                case ResourceType.Isotopes:
                    marketResource = MarketResourceType.Energy;
                    return true;
                case ResourceType.Food:
                    marketResource = MarketResourceType.Food;
                    return true;
                case ResourceType.Water:
                case ResourceType.HeavyWater:
                    marketResource = MarketResourceType.Water;
                    return true;
                case ResourceType.Supplies:
                case ResourceType.Volatiles:
                case ResourceType.VolatileMotes:
                case ResourceType.IndustrialCrystals:
                    marketResource = MarketResourceType.Industrial;
                    return true;
                case ResourceType.ExoticGases:
                case ResourceType.RelicData:
                    marketResource = MarketResourceType.Tech;
                    return true;
                default:
                    marketResource = MarketResourceType.Consumer;
                    return false;
            }
        }

        private static bool TryMapItemIdToMarketResource(in FixedString64Bytes itemId, out MarketResourceType marketResource)
        {
            if (itemId.Equals(new FixedString64Bytes("space4x_ingot")))
            {
                marketResource = MarketResourceType.RefinedMetal;
                return true;
            }

            if (itemId.Equals(new FixedString64Bytes("space4x_alloy")))
            {
                marketResource = MarketResourceType.Industrial;
                return true;
            }

            if (itemId.Equals(new FixedString64Bytes("space4x_parts")))
            {
                marketResource = MarketResourceType.Industrial;
                return true;
            }

            marketResource = default;
            return false;
        }

        private static bool TryGetMarketMidPrice(DynamicBuffer<MarketPriceEntry> prices, MarketResourceType resource, out float price)
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
                price = math.max(0.01f, mid > 0f ? mid : math.max(buy, sell));
                return true;
            }

            price = 0f;
            return false;
        }

        private static void CloseSession(
            ref Space4XTradeBarterState state,
            ref DynamicBuffer<Space4XTradeBarterViewEntry> view,
            ref DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            ref DynamicBuffer<Space4XTradeBarterCommand> commands,
            ref DynamicBuffer<Space4XTradeBarterResolvedLine> resolved)
        {
            state.IsOpen = 0;
            state.EntryPointSlot = 0;
            state.SessionId = 0;
            state.ContextEntity = Entity.Null;
            state.PartyA = Entity.Null;
            state.PartyB = Entity.Null;
            state.PartyAInventoryEntity = Entity.Null;
            state.PartyBInventoryEntity = Entity.Null;
            state.PartyAFactionId = 0;
            state.PartyBFactionId = 0;
            view.Clear();
            offers.Clear();
            commands.Clear();
            resolved.Clear();
        }

        private static void AppendEvent(
            ref DynamicBuffer<Space4XTradeBarterEvent> events,
            in Space4XTradeBarterState state,
            Space4XTradeBarterStatus status,
            float valueA,
            float valueB,
            in FixedString128Bytes message)
        {
            var valueAMicros = Space4XMoneyMath.ToMicros(valueA);
            var valueBMicros = Space4XMoneyMath.ToMicros(valueB);
            events.Add(new Space4XTradeBarterEvent
            {
                SessionId = state.SessionId,
                Status = status,
                ValueOfferedByAMicros = valueAMicros,
                ValueOfferedByBMicros = valueBMicros,
                NetDeltaMicros = valueAMicros - valueBMicros,
                FeePaidByAMicros = 0,
                FeePaidByBMicros = 0,
                ValueOfferedByA = valueA,
                ValueOfferedByB = valueB,
                NetDelta = valueA - valueB,
                IsOpenAfter = state.IsOpen,
                Message = message
            });
        }
    }

    /// <summary>
    /// Applies accepted resolved lines into source/target inventories, cargo, and market offers.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XTradeBarterCommandSystem))]
    [UpdateBefore(typeof(Space4XTradeBarterViewBuildSystem))]
    public partial struct Space4XTradeBarterSettlementSystem : ISystem
    {
        private BufferLookup<InventoryItem> _inventoryItemsLookup;
        private BufferLookup<ResourceStorage> _cargoLookup;
        private BufferLookup<TradeOffer> _tradeOfferLookup;
        private BufferLookup<Space4XCurrencyBalance> _currencyLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTradeBarterState>();
            _inventoryItemsLookup = state.GetBufferLookup<InventoryItem>(false);
            _cargoLookup = state.GetBufferLookup<ResourceStorage>(false);
            _tradeOfferLookup = state.GetBufferLookup<TradeOffer>(false);
            _currencyLookup = state.GetBufferLookup<Space4XCurrencyBalance>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            _inventoryItemsLookup.Update(ref state);
            _cargoLookup.Update(ref state);
            _tradeOfferLookup.Update(ref state);
            _currencyLookup.Update(ref state);

            var barterEntity = SystemAPI.GetSingletonEntity<Space4XTradeBarterState>();
            var barterState = state.EntityManager.GetComponentData<Space4XTradeBarterState>(barterEntity);
            var resolved = state.EntityManager.GetBuffer<Space4XTradeBarterResolvedLine>(barterEntity);
            if (resolved.Length == 0)
            {
                return;
            }

            var events = state.EntityManager.GetBuffer<Space4XTradeBarterEvent>(barterEntity);
            var ledger = state.EntityManager.GetBuffer<Space4XEconomyLedgerEvent>(barterEntity);
            var feePolicy = SystemAPI.TryGetSingleton<Space4XTradeFeePolicyConfig>(out var policy)
                ? policy
                : Space4XTradeFeePolicyConfig.Default;
            var sessionId = resolved[0].SessionId;
            var currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            if (sessionId == 0 || (barterState.SessionId != 0 && sessionId != barterState.SessionId))
            {
                resolved.Clear();
                AppendEvent(
                    ref events,
                    sessionId != 0 ? sessionId : barterState.SessionId,
                    Space4XTradeBarterStatus.RejectedInvalidSession,
                    new FixedString128Bytes("trade.barter.settle.invalid_session"),
                    0f,
                    0f,
                    0,
                    0,
                    barterState.IsOpen);
                return;
            }

            for (int i = 0; i < resolved.Length; i++)
            {
                var line = resolved[i];
                if (line.Quantity <= 0f)
                {
                    continue;
                }

                if (!CanApplyLine(in barterState, in line))
                {
                    resolved.Clear();
                    AppendEvent(
                        ref events,
                        sessionId,
                        Space4XTradeBarterStatus.RejectedAvailability,
                        new FixedString128Bytes("trade.barter.settle.rejected"),
                        0f,
                        0f,
                        0,
                        0,
                        barterState.IsOpen);
                    return;
                }
            }

            long valueAMicros = 0;
            long valueBMicros = 0;
            for (int i = 0; i < resolved.Length; i++)
            {
                var line = resolved[i];
                if (line.Quantity <= 0f)
                {
                    continue;
                }

                if (!ApplyLine(in barterState, in line, currentTick))
                {
                    resolved.Clear();
                    AppendEvent(
                        ref events,
                        sessionId,
                        Space4XTradeBarterStatus.RejectedAvailability,
                        new FixedString128Bytes("trade.barter.settle.apply_failed"),
                        0f,
                        0f,
                        0,
                        0,
                        barterState.IsOpen);
                    return;
                }

                var unitPriceMicros = line.UnitPriceMicros > 0
                    ? line.UnitPriceMicros
                    : Space4XMoneyMath.ToMicros(math.max(0f, line.UnitValue));
                var lineValueMicros = Space4XMoneyMath.ComputeLineMicros(line.Quantity, unitPriceMicros);
                if (line.Side == Space4XTradeBarterSide.PartyA)
                {
                    valueAMicros += lineValueMicros;
                }
                else
                {
                    valueBMicros += lineValueMicros;
                }
            }

            var feeA = ComputeFeeMicros(valueAMicros, in feePolicy);
            var feeB = ComputeFeeMicros(valueBMicros, in feePolicy);
            var sameActor = barterState.PartyA == barterState.PartyB && barterState.PartyA != Entity.Null;
            var totalFee = feeA + feeB;
            if (totalFee < 0)
            {
                totalFee = 0;
            }
            var canDebit = sameActor
                ? CanDebitCurrency(barterState.PartyA, feePolicy.CurrencyId, totalFee)
                : CanDebitCurrency(barterState.PartyA, feePolicy.CurrencyId, feeA) &&
                  CanDebitCurrency(barterState.PartyB, feePolicy.CurrencyId, feeB);
            if (!canDebit)
            {
                resolved.Clear();
                AppendEvent(
                    ref events,
                    sessionId,
                    Space4XTradeBarterStatus.RejectedAvailability,
                    new FixedString128Bytes("trade.barter.settle.fee_unfunded"),
                    0f,
                    0f,
                    0,
                    0,
                    barterState.IsOpen);
                return;
            }

            var debited = sameActor
                ? TryDebitCurrency(barterState.PartyA, feePolicy.CurrencyId, totalFee)
                : TryDebitCurrency(barterState.PartyA, feePolicy.CurrencyId, feeA) &&
                  TryDebitCurrency(barterState.PartyB, feePolicy.CurrencyId, feeB);
            if (!debited)
            {
                resolved.Clear();
                AppendEvent(
                    ref events,
                    sessionId,
                    Space4XTradeBarterStatus.RejectedAvailability,
                    new FixedString128Bytes("trade.barter.settle.fee_debit_failed"),
                    0f,
                    0f,
                    0,
                    0,
                    barterState.IsOpen);
                return;
            }

            resolved.Clear();
            AppendLedgerEvent(
                ref ledger,
                currentTick,
                sessionId,
                barterState.PartyA,
                barterState.PartyB,
                feePolicy.CurrencyId,
                valueAMicros,
                feeA,
                new FixedString64Bytes("trade.settle.partyA"));
            AppendLedgerEvent(
                ref ledger,
                currentTick,
                sessionId,
                barterState.PartyB,
                barterState.PartyA,
                feePolicy.CurrencyId,
                valueBMicros,
                feeB,
                new FixedString64Bytes("trade.settle.partyB"));
            AppendEvent(
                ref events,
                sessionId,
                Space4XTradeBarterStatus.Accepted,
                new FixedString128Bytes("trade.barter.settled"),
                Space4XMoneyMath.FromMicros(valueAMicros),
                Space4XMoneyMath.FromMicros(valueBMicros),
                feeA,
                feeB,
                barterState.IsOpen);
        }

        private bool CanApplyLine(in Space4XTradeBarterState state, in Space4XTradeBarterResolvedLine line)
        {
            var sourceEntity = ResolveSideEntity(in state, line.Side);
            var sourceInventory = ResolveSideInventoryEntity(in state, line.Side);
            var destinationSide = OppositeSide(line.Side);
            var destinationEntity = ResolveSideEntity(in state, destinationSide);
            var destinationInventory = ResolveSideInventoryEntity(in state, destinationSide);
            var quantity = math.max(0f, line.Quantity);
            if (quantity <= 0f)
            {
                return true;
            }

            if (!HasSourceAvailability(sourceEntity, sourceInventory, in line, quantity))
            {
                return false;
            }

            return CanDestinationAccept(destinationEntity, destinationInventory, in line, quantity);
        }

        private bool HasSourceAvailability(
            Entity sourceEntity,
            Entity sourceInventory,
            in Space4XTradeBarterResolvedLine line,
            float quantity)
        {
            switch (line.SourceKind)
            {
                case Space4XTradeBarterSourceKind.InventoryItem:
                    if (sourceInventory == Entity.Null || !_inventoryItemsLookup.HasBuffer(sourceInventory))
                    {
                        return false;
                    }
                    return SumInventoryItem(_inventoryItemsLookup[sourceInventory], line.ItemId) + 0.0001f >= quantity;

                case Space4XTradeBarterSourceKind.CargoResource:
                    if (sourceEntity == Entity.Null || !_cargoLookup.HasBuffer(sourceEntity))
                    {
                        return false;
                    }
                    return SumCargo(_cargoLookup[sourceEntity], line.CargoResourceType) + 0.0001f >= quantity;

                case Space4XTradeBarterSourceKind.MarketSellOffer:
                    if (sourceEntity == Entity.Null || !_tradeOfferLookup.HasBuffer(sourceEntity))
                    {
                        return false;
                    }
                    return SumMarketOffer(_tradeOfferLookup[sourceEntity], TradeOfferType.Sell, line.MarketResourceType) + 0.0001f >= quantity;

                case Space4XTradeBarterSourceKind.MarketBuyDemand:
                    if (sourceEntity == Entity.Null || !_tradeOfferLookup.HasBuffer(sourceEntity))
                    {
                        return false;
                    }
                    return SumMarketOffer(_tradeOfferLookup[sourceEntity], TradeOfferType.Buy, line.MarketResourceType) + 0.0001f >= quantity;

                default:
                    return false;
            }
        }

        private bool CanDestinationAccept(
            Entity destinationEntity,
            Entity destinationInventory,
            in Space4XTradeBarterResolvedLine line,
            float quantity)
        {
            if (line.SourceKind == Space4XTradeBarterSourceKind.MarketBuyDemand)
            {
                return true;
            }

            if (destinationInventory != Entity.Null && _inventoryItemsLookup.HasBuffer(destinationInventory))
            {
                return true;
            }

            if (destinationEntity != Entity.Null && _cargoLookup.HasBuffer(destinationEntity))
            {
                var resource = ResolveTransferredResource(in line);
                if (resource.HasValue && HasCargoCapacity(_cargoLookup[destinationEntity], resource.Value, quantity))
                {
                    return true;
                }
            }

            if (destinationEntity != Entity.Null && _tradeOfferLookup.HasBuffer(destinationEntity))
            {
                // Market-like sink: accepted even if no concrete inventory is represented.
                return true;
            }

            return false;
        }

        private bool ApplyLine(in Space4XTradeBarterState session, in Space4XTradeBarterResolvedLine line, uint tick)
        {
            var sourceSide = line.Side;
            var destinationSide = OppositeSide(sourceSide);
            var sourceEntity = ResolveSideEntity(in session, sourceSide);
            var destinationEntity = ResolveSideEntity(in session, destinationSide);
            var sourceInventory = ResolveSideInventoryEntity(in session, sourceSide);
            var destinationInventory = ResolveSideInventoryEntity(in session, destinationSide);
            var amount = math.max(0f, line.Quantity);
            float consumed;

            switch (line.SourceKind)
            {
                case Space4XTradeBarterSourceKind.InventoryItem:
                    if (sourceInventory == Entity.Null || !_inventoryItemsLookup.HasBuffer(sourceInventory))
                    {
                        return false;
                    }
                    var sourceItems = _inventoryItemsLookup[sourceInventory];
                    if (!TryConsumeInventoryItem(ref sourceItems, line.ItemId, amount, out consumed, out var quality, out var durability))
                    {
                        return false;
                    }
                    return DeliverAsInventoryOrSink(
                        destinationEntity,
                        destinationInventory,
                        line.ItemId,
                        consumed,
                        quality,
                        durability,
                        tick);

                case Space4XTradeBarterSourceKind.CargoResource:
                    if (sourceEntity == Entity.Null || !_cargoLookup.HasBuffer(sourceEntity))
                    {
                        return false;
                    }
                    var sourceCargo = _cargoLookup[sourceEntity];
                    if (!TryConsumeCargo(ref sourceCargo, line.CargoResourceType, amount, out consumed))
                    {
                        return false;
                    }
                    return DeliverResource(
                        destinationEntity,
                        destinationInventory,
                        line.CargoResourceType,
                        consumed,
                        tick);

                case Space4XTradeBarterSourceKind.MarketSellOffer:
                    if (sourceEntity == Entity.Null || !_tradeOfferLookup.HasBuffer(sourceEntity))
                    {
                        return false;
                    }
                    var offers = _tradeOfferLookup[sourceEntity];
                    if (!TryConsumeMarketOffer(ref offers, TradeOfferType.Sell, line.MarketResourceType, amount, out consumed))
                    {
                        return false;
                    }
                    if (!TryMapMarketToResource(line.MarketResourceType, out var mappedResource))
                    {
                        return DeliverAsInventoryOrSink(
                            destinationEntity,
                            destinationInventory,
                            BuildMarketItemId(line.MarketResourceType),
                            consumed,
                            line.Quality,
                            line.Durability,
                            tick);
                    }
                    return DeliverResource(
                        destinationEntity,
                        destinationInventory,
                        mappedResource,
                        consumed,
                        tick);

                case Space4XTradeBarterSourceKind.MarketBuyDemand:
                    if (sourceEntity == Entity.Null || !_tradeOfferLookup.HasBuffer(sourceEntity))
                    {
                        return false;
                    }
                    var buyOffers = _tradeOfferLookup[sourceEntity];
                    return TryConsumeMarketOffer(ref buyOffers, TradeOfferType.Buy, line.MarketResourceType, amount, out _);

                default:
                    return false;
            }
        }

        private bool DeliverResource(
            Entity destinationEntity,
            Entity destinationInventory,
            ResourceType resource,
            float amount,
            uint tick)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (destinationEntity != Entity.Null && _cargoLookup.HasBuffer(destinationEntity))
            {
                var cargo = _cargoLookup[destinationEntity];
                if (TryDepositCargo(ref cargo, resource, amount))
                {
                    return true;
                }
            }

            var itemId = BuildCargoItemId(resource);
            return DeliverAsInventoryOrSink(destinationEntity, destinationInventory, itemId, amount, 1f, 1f, tick);
        }

        private bool DeliverAsInventoryOrSink(
            Entity destinationEntity,
            Entity destinationInventory,
            in FixedString64Bytes itemId,
            float amount,
            float quality,
            float durability,
            uint tick)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (destinationInventory != Entity.Null && _inventoryItemsLookup.HasBuffer(destinationInventory))
            {
                var items = _inventoryItemsLookup[destinationInventory];
                AddInventoryItem(ref items, itemId, amount, quality, durability, tick);
                return true;
            }

            return destinationEntity != Entity.Null && _tradeOfferLookup.HasBuffer(destinationEntity);
        }

        private static Space4XTradeBarterSide OppositeSide(Space4XTradeBarterSide side)
        {
            return side == Space4XTradeBarterSide.PartyA ? Space4XTradeBarterSide.PartyB : Space4XTradeBarterSide.PartyA;
        }

        private static Entity ResolveSideEntity(in Space4XTradeBarterState state, Space4XTradeBarterSide side)
        {
            return side == Space4XTradeBarterSide.PartyA ? state.PartyA : state.PartyB;
        }

        private static Entity ResolveSideInventoryEntity(in Space4XTradeBarterState state, Space4XTradeBarterSide side)
        {
            return side == Space4XTradeBarterSide.PartyA ? state.PartyAInventoryEntity : state.PartyBInventoryEntity;
        }

        private static ResourceType? ResolveTransferredResource(in Space4XTradeBarterResolvedLine line)
        {
            return line.SourceKind switch
            {
                Space4XTradeBarterSourceKind.CargoResource => line.CargoResourceType,
                Space4XTradeBarterSourceKind.MarketSellOffer => TryMapMarketToResource(line.MarketResourceType, out var mapped) ? mapped : null,
                _ => null
            };
        }

        private static float SumInventoryItem(DynamicBuffer<InventoryItem> items, in FixedString64Bytes itemId)
        {
            var total = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ItemId.Equals(itemId))
                {
                    total += math.max(0f, items[i].Quantity);
                }
            }
            return total;
        }

        private static float SumCargo(DynamicBuffer<ResourceStorage> cargo, ResourceType resource)
        {
            var total = 0f;
            for (int i = 0; i < cargo.Length; i++)
            {
                if (cargo[i].Type == resource)
                {
                    total += math.max(0f, cargo[i].Amount);
                }
            }
            return total;
        }

        private static float SumMarketOffer(DynamicBuffer<TradeOffer> offers, TradeOfferType type, MarketResourceType resource)
        {
            var total = 0f;
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i].IsFulfilled == 0 && offers[i].Type == type && offers[i].ResourceType == resource)
                {
                    total += math.max(0f, offers[i].Quantity);
                }
            }
            return total;
        }

        private static bool HasCargoCapacity(DynamicBuffer<ResourceStorage> cargo, ResourceType resource, float amount)
        {
            for (int i = 0; i < cargo.Length; i++)
            {
                if (cargo[i].Type != resource)
                {
                    continue;
                }
                var remaining = math.max(0f, cargo[i].Capacity - cargo[i].Amount);
                return remaining + 0.0001f >= amount;
            }
            return false;
        }

        private static bool TryConsumeInventoryItem(
            ref DynamicBuffer<InventoryItem> items,
            in FixedString64Bytes itemId,
            float amount,
            out float consumed,
            out float quality,
            out float durability)
        {
            consumed = 0f;
            quality = 1f;
            durability = 1f;
            if (amount <= 0f)
            {
                return true;
            }

            float weightedQuality = 0f;
            float weightedDurability = 0f;
            var remaining = amount;
            for (int i = items.Length - 1; i >= 0 && remaining > 0f; i--)
            {
                if (!items[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = items[i];
                var take = math.min(math.max(0f, item.Quantity), remaining);
                if (take <= 0f)
                {
                    continue;
                }

                remaining -= take;
                consumed += take;
                weightedQuality += take * math.max(0f, item.Quality);
                weightedDurability += take * math.max(0f, item.Durability);

                item.Quantity -= take;
                if (item.Quantity <= 0.0001f)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = item;
                }
            }

            if (consumed <= 0f)
            {
                return false;
            }

            quality = weightedQuality / consumed;
            durability = weightedDurability / consumed;
            return consumed + 0.0001f >= amount;
        }

        private static void AddInventoryItem(
            ref DynamicBuffer<InventoryItem> items,
            in FixedString64Bytes itemId,
            float amount,
            float quality,
            float durability,
            uint tick)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].ItemId.Equals(itemId))
                {
                    continue;
                }

                var item = items[i];
                item.Quantity += amount;
                item.Quality = math.max(item.Quality, quality);
                item.Durability = math.max(item.Durability, durability);
                items[i] = item;
                return;
            }

            items.Add(new InventoryItem
            {
                ItemId = itemId,
                Quantity = amount,
                Quality = quality,
                Durability = durability,
                CreatedTick = tick
            });
        }

        private static bool TryConsumeCargo(
            ref DynamicBuffer<ResourceStorage> cargo,
            ResourceType resource,
            float amount,
            out float consumed)
        {
            consumed = 0f;
            if (amount <= 0f)
            {
                return true;
            }

            var remaining = amount;
            for (int i = 0; i < cargo.Length && remaining > 0f; i++)
            {
                if (cargo[i].Type != resource)
                {
                    continue;
                }

                var entry = cargo[i];
                var take = math.min(math.max(0f, entry.Amount), remaining);
                if (take <= 0f)
                {
                    continue;
                }

                entry.Amount -= take;
                cargo[i] = entry;
                remaining -= take;
                consumed += take;
            }

            return consumed + 0.0001f >= amount;
        }

        private static bool TryDepositCargo(
            ref DynamicBuffer<ResourceStorage> cargo,
            ResourceType resource,
            float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            for (int i = 0; i < cargo.Length; i++)
            {
                if (cargo[i].Type != resource)
                {
                    continue;
                }

                var entry = cargo[i];
                var remaining = math.max(0f, entry.Capacity - entry.Amount);
                if (remaining + 0.0001f < amount)
                {
                    return false;
                }

                entry.Amount += amount;
                cargo[i] = entry;
                return true;
            }

            return false;
        }

        private static bool TryConsumeMarketOffer(
            ref DynamicBuffer<TradeOffer> offers,
            TradeOfferType type,
            MarketResourceType resource,
            float amount,
            out float consumed)
        {
            consumed = 0f;
            if (amount <= 0f)
            {
                return true;
            }

            var remaining = amount;
            for (int i = 0; i < offers.Length && remaining > 0f; i++)
            {
                var offer = offers[i];
                if (offer.IsFulfilled != 0 || offer.Type != type || offer.ResourceType != resource)
                {
                    continue;
                }

                var take = math.min(math.max(0f, offer.Quantity), remaining);
                if (take <= 0f)
                {
                    continue;
                }

                offer.Quantity -= take;
                if (offer.Quantity <= 0.0001f)
                {
                    offer.Quantity = 0f;
                    offer.IsFulfilled = 1;
                }
                offers[i] = offer;

                consumed += take;
                remaining -= take;
            }

            return consumed + 0.0001f >= amount;
        }

        private static bool TryMapMarketToResource(MarketResourceType market, out ResourceType resource)
        {
            switch (market)
            {
                case MarketResourceType.Ore:
                    resource = ResourceType.Ore;
                    return true;
                case MarketResourceType.RefinedMetal:
                    resource = ResourceType.Minerals;
                    return true;
                case MarketResourceType.RareEarth:
                    resource = ResourceType.RareMetals;
                    return true;
                case MarketResourceType.Energy:
                    resource = ResourceType.Fuel;
                    return true;
                case MarketResourceType.Food:
                    resource = ResourceType.Food;
                    return true;
                case MarketResourceType.Water:
                    resource = ResourceType.Water;
                    return true;
                case MarketResourceType.Industrial:
                    resource = ResourceType.Supplies;
                    return true;
                case MarketResourceType.Tech:
                    resource = ResourceType.RelicData;
                    return true;
                case MarketResourceType.Luxury:
                    resource = ResourceType.BoosterGas;
                    return true;
                case MarketResourceType.Military:
                    resource = ResourceType.StrontiumClathrates;
                    return true;
                case MarketResourceType.Consumer:
                    resource = ResourceType.OrganicMatter;
                    return true;
                default:
                    resource = default;
                    return false;
            }
        }

        private static FixedString64Bytes BuildCargoItemId(ResourceType resource)
        {
            var id = new FixedString64Bytes("cargo.");
            id.Append((int)resource);
            return id;
        }

        private static FixedString64Bytes BuildMarketItemId(MarketResourceType resource)
        {
            var id = new FixedString64Bytes("market.sell.");
            id.Append((int)resource);
            return id;
        }

        private bool TryDebitCurrency(Entity actor, Space4XCurrencyId currencyId, long amountMicros)
        {
            if (amountMicros <= 0 || actor == Entity.Null || !_currencyLookup.HasBuffer(actor))
            {
                return true;
            }

            var balances = _currencyLookup[actor];
            for (int i = 0; i < balances.Length; i++)
            {
                if (balances[i].CurrencyId != currencyId)
                {
                    continue;
                }

                var balance = balances[i];
                if (balance.AmountMicros < amountMicros)
                {
                    return false;
                }

                balance.AmountMicros -= amountMicros;
                balances[i] = balance;
                return true;
            }

            return false;
        }

        private bool CanDebitCurrency(Entity actor, Space4XCurrencyId currencyId, long amountMicros)
        {
            if (amountMicros <= 0 || actor == Entity.Null || !_currencyLookup.HasBuffer(actor))
            {
                return true;
            }

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

        private static long ComputeFeeMicros(long grossMicros, in Space4XTradeFeePolicyConfig policy)
        {
            if (policy.Enabled == 0 || grossMicros <= 0)
            {
                return 0;
            }

            var bps = policy.BrokerFeeBps + policy.SalesTaxBps;
            var variableFee = Space4XMoneyMath.ApplyBps(grossMicros, bps);
            return math.max(0L, variableFee + policy.FlatDockingFeeMicros);
        }

        private static void AppendLedgerEvent(
            ref DynamicBuffer<Space4XEconomyLedgerEvent> ledger,
            uint tick,
            uint sessionId,
            Entity actor,
            Entity counterparty,
            Space4XCurrencyId currencyId,
            long grossMicros,
            long feeMicros,
            in FixedString64Bytes reasonId)
        {
            ledger.Add(new Space4XEconomyLedgerEvent
            {
                Tick = tick,
                SessionId = sessionId,
                Actor = actor,
                Counterparty = counterparty,
                CurrencyId = currencyId,
                GrossMicros = grossMicros,
                FeeMicros = feeMicros,
                NetMicros = grossMicros - feeMicros,
                ReasonId = reasonId
            });

            const int maxLedgerEvents = 256;
            while (ledger.Length > maxLedgerEvents)
            {
                ledger.RemoveAt(0);
            }
        }

        private static void AppendEvent(
            ref DynamicBuffer<Space4XTradeBarterEvent> events,
            uint sessionId,
            Space4XTradeBarterStatus status,
            in FixedString128Bytes message,
            float valueA = 0f,
            float valueB = 0f,
            long feeA = 0,
            long feeB = 0,
            byte isOpenAfter = 0)
        {
            var valueAMicros = Space4XMoneyMath.ToMicros(valueA);
            var valueBMicros = Space4XMoneyMath.ToMicros(valueB);
            events.Add(new Space4XTradeBarterEvent
            {
                SessionId = sessionId,
                Status = status,
                ValueOfferedByAMicros = valueAMicros,
                ValueOfferedByBMicros = valueBMicros,
                NetDeltaMicros = valueAMicros - valueBMicros,
                FeePaidByAMicros = feeA,
                FeePaidByBMicros = feeB,
                ValueOfferedByA = valueA,
                ValueOfferedByB = valueB,
                NetDelta = valueA - valueB,
                IsOpenAfter = isOpenAfter,
                Message = message
            });
        }
    }

    /// <summary>
    /// Rebuilds 4-column barter view rows from inventories/cargo/offers.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XTradeBarterSettlementSystem))]
    public partial struct Space4XTradeBarterViewBuildSystem : ISystem
    {
        private BufferLookup<InventoryItem> _inventoryItemsLookup;
        private BufferLookup<ResourceStorage> _cargoLookup;
        private BufferLookup<TradeOffer> _tradeOfferLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTradeBarterState>();
            state.RequireForUpdate<Space4XTradeBarterConfig>();
            _inventoryItemsLookup = state.GetBufferLookup<InventoryItem>(true);
            _cargoLookup = state.GetBufferLookup<ResourceStorage>(true);
            _tradeOfferLookup = state.GetBufferLookup<TradeOffer>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _inventoryItemsLookup.Update(ref state);
            _cargoLookup.Update(ref state);
            _tradeOfferLookup.Update(ref state);

            var config = SystemAPI.GetSingleton<Space4XTradeBarterConfig>();
            var barterEntity = SystemAPI.GetSingletonEntity<Space4XTradeBarterState>();
            var barterState = state.EntityManager.GetComponentData<Space4XTradeBarterState>(barterEntity);
            var view = state.EntityManager.GetBuffer<Space4XTradeBarterViewEntry>(barterEntity);
            var offers = state.EntityManager.GetBuffer<Space4XTradeBarterOfferEntry>(barterEntity);

            if (barterState.IsOpen == 0)
            {
                if (view.Length > 0)
                {
                    view.Clear();
                }
                return;
            }

            view.Clear();
            var includeMarketSell = barterState.EntryPointSlot != 2;
            var includeMarketBuy = barterState.EntryPointSlot != 1;
            AddInventoryRows(ref view, offers, barterState.PartyAInventoryEntity, Space4XTradeBarterSide.PartyA, Space4XTradeBarterColumn.PartyInventory);
            AddCargoRows(ref view, offers, barterState.PartyA, Space4XTradeBarterSide.PartyA, Space4XTradeBarterColumn.PartyInventory);
            AddInventoryRows(ref view, offers, barterState.PartyBInventoryEntity, Space4XTradeBarterSide.PartyB, Space4XTradeBarterColumn.CounterpartyInventory);
            AddCargoRows(ref view, offers, barterState.PartyB, Space4XTradeBarterSide.PartyB, Space4XTradeBarterColumn.CounterpartyInventory);
            AddMarketRows(
                ref view,
                offers,
                barterState.PartyB,
                Space4XTradeBarterSide.PartyB,
                Space4XTradeBarterColumn.CounterpartyInventory,
                includeMarketSell,
                includeMarketBuy);

            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                var column = offer.Side == Space4XTradeBarterSide.PartyA
                    ? Space4XTradeBarterColumn.PartyOffer
                    : Space4XTradeBarterColumn.CounterpartyOffer;

                view.Add(new Space4XTradeBarterViewEntry
                {
                    Column = column,
                    Side = offer.Side,
                    SourceKind = offer.SourceKind,
                    ItemId = offer.ItemId,
                    Label = offer.ItemId,
                    QuantityAvailable = 0f,
                    QuantityOffered = math.max(0f, offer.Quantity),
                    CurrencyId = offer.CurrencyId != Space4XCurrencyId.None ? offer.CurrencyId : config.CurrencyId,
                    UnitPriceMicros = offer.UnitPriceMicros > 0 ? offer.UnitPriceMicros : Space4XMoneyMath.ToMicros(math.max(0f, offer.UnitValue)),
                    UnitValue = math.max(0.01f, offer.UnitValue),
                    Quality = math.max(0f, offer.Quality),
                    Durability = math.max(0f, offer.Durability),
                    CargoResourceType = offer.CargoResourceType,
                    MarketResourceType = offer.MarketResourceType
                });
            }

            TrimByColumn(ref view, Space4XTradeBarterColumn.PartyInventory, config.MaxRowsPerColumn);
            TrimByColumn(ref view, Space4XTradeBarterColumn.PartyOffer, config.MaxRowsPerColumn);
            TrimByColumn(ref view, Space4XTradeBarterColumn.CounterpartyInventory, config.MaxRowsPerColumn);
            TrimByColumn(ref view, Space4XTradeBarterColumn.CounterpartyOffer, config.MaxRowsPerColumn);
        }

        private void AddInventoryRows(
            ref DynamicBuffer<Space4XTradeBarterViewEntry> view,
            DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            Entity inventoryEntity,
            Space4XTradeBarterSide side,
            Space4XTradeBarterColumn column)
        {
            if (inventoryEntity == Entity.Null || !_inventoryItemsLookup.HasBuffer(inventoryEntity))
            {
                return;
            }

            var items = _inventoryItemsLookup[inventoryEntity];
            for (int i = 0; i < items.Length; i++)
            {
                var quantity = math.max(0f, items[i].Quantity);
                if (quantity <= 0f)
                {
                    continue;
                }

                var offered = SumOffered(offers, side, Space4XTradeBarterSourceKind.InventoryItem, items[i].ItemId, default, default);
                view.Add(new Space4XTradeBarterViewEntry
                {
                    Column = column,
                    Side = side,
                    SourceKind = Space4XTradeBarterSourceKind.InventoryItem,
                    ItemId = items[i].ItemId,
                    Label = items[i].ItemId,
                    QuantityAvailable = quantity,
                    QuantityOffered = offered,
                    CurrencyId = Space4XCurrencyId.Credits,
                    UnitPriceMicros = Space4XMoneyMath.ToMicros(1f),
                    UnitValue = 1f,
                    Quality = math.max(0f, items[i].Quality),
                    Durability = math.max(0f, items[i].Durability),
                    CargoResourceType = default,
                    MarketResourceType = default
                });
            }
        }

        private void AddCargoRows(
            ref DynamicBuffer<Space4XTradeBarterViewEntry> view,
            DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            Entity sideEntity,
            Space4XTradeBarterSide side,
            Space4XTradeBarterColumn column)
        {
            if (sideEntity == Entity.Null || !_cargoLookup.HasBuffer(sideEntity))
            {
                return;
            }

            var storage = _cargoLookup[sideEntity];
            for (int i = 0; i < storage.Length; i++)
            {
                var amount = math.max(0f, storage[i].Amount);
                if (amount <= 0f)
                {
                    continue;
                }

                var itemId = BuildCargoItemId(storage[i].Type);
                var offered = SumOffered(offers, side, Space4XTradeBarterSourceKind.CargoResource, itemId, storage[i].Type, default);
                view.Add(new Space4XTradeBarterViewEntry
                {
                    Column = column,
                    Side = side,
                    SourceKind = Space4XTradeBarterSourceKind.CargoResource,
                    ItemId = itemId,
                    Label = itemId,
                    QuantityAvailable = amount,
                    QuantityOffered = offered,
                    CurrencyId = Space4XCurrencyId.Credits,
                    UnitPriceMicros = Space4XMoneyMath.ToMicros(1f),
                    UnitValue = 1f,
                    Quality = 1f,
                    Durability = 1f,
                    CargoResourceType = storage[i].Type,
                    MarketResourceType = default
                });
            }
        }

        private void AddMarketRows(
            ref DynamicBuffer<Space4XTradeBarterViewEntry> view,
            DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            Entity sideEntity,
            Space4XTradeBarterSide side,
            Space4XTradeBarterColumn column,
            bool includeSell,
            bool includeBuy)
        {
            if (sideEntity == Entity.Null || !_tradeOfferLookup.HasBuffer(sideEntity))
            {
                return;
            }

            var marketOffers = _tradeOfferLookup[sideEntity];
            for (int i = 0; i < marketOffers.Length; i++)
            {
                var marketOffer = marketOffers[i];
                if (marketOffer.IsFulfilled != 0 || marketOffer.Quantity <= 0f)
                {
                    continue;
                }
                if (marketOffer.Type == TradeOfferType.Sell && !includeSell)
                {
                    continue;
                }
                if (marketOffer.Type == TradeOfferType.Buy && !includeBuy)
                {
                    continue;
                }

                var sourceKind = marketOffer.Type == TradeOfferType.Buy
                    ? Space4XTradeBarterSourceKind.MarketBuyDemand
                    : Space4XTradeBarterSourceKind.MarketSellOffer;
                var itemId = BuildMarketItemId(marketOffer.ResourceType, sourceKind == Space4XTradeBarterSourceKind.MarketBuyDemand);
                var offered = SumOffered(offers, side, sourceKind, itemId, default, marketOffer.ResourceType);
                view.Add(new Space4XTradeBarterViewEntry
                {
                    Column = column,
                    Side = side,
                    SourceKind = sourceKind,
                    ItemId = itemId,
                    Label = itemId,
                    QuantityAvailable = math.max(0f, marketOffer.Quantity),
                    QuantityOffered = offered,
                    CurrencyId = Space4XCurrencyId.Credits,
                    UnitPriceMicros = Space4XMoneyMath.ToMicros(math.max(0.01f, marketOffer.PricePerUnit)),
                    UnitValue = math.max(0.01f, marketOffer.PricePerUnit),
                    Quality = 1f,
                    Durability = 1f,
                    CargoResourceType = default,
                    MarketResourceType = marketOffer.ResourceType
                });
            }
        }

        private static float SumOffered(
            DynamicBuffer<Space4XTradeBarterOfferEntry> offers,
            Space4XTradeBarterSide side,
            Space4XTradeBarterSourceKind sourceKind,
            in FixedString64Bytes itemId,
            ResourceType cargoType,
            MarketResourceType marketType)
        {
            float sum = 0f;
            for (int i = 0; i < offers.Length; i++)
            {
                if (offers[i].Side != side ||
                    offers[i].SourceKind != sourceKind ||
                    !offers[i].ItemId.Equals(itemId) ||
                    offers[i].CargoResourceType != cargoType ||
                    offers[i].MarketResourceType != marketType)
                {
                    continue;
                }

                sum += math.max(0f, offers[i].Quantity);
            }

            return sum;
        }

        private static void TrimByColumn(
            ref DynamicBuffer<Space4XTradeBarterViewEntry> view,
            Space4XTradeBarterColumn column,
            int maxRows)
        {
            if (maxRows <= 0)
            {
                return;
            }

            var kept = 0;
            for (int i = view.Length - 1; i >= 0; i--)
            {
                if (view[i].Column != column)
                {
                    continue;
                }

                kept++;
                if (kept > maxRows)
                {
                    view.RemoveAt(i);
                }
            }
        }

        private static FixedString64Bytes BuildCargoItemId(ResourceType type)
        {
            var id = new FixedString64Bytes("cargo.");
            id.Append((int)type);
            return id;
        }

        private static FixedString64Bytes BuildMarketItemId(MarketResourceType type, bool demand)
        {
            var id = demand
                ? new FixedString64Bytes("market.buy.")
                : new FixedString64Bytes("market.sell.");
            id.Append((int)type);
            return id;
        }
    }
}
