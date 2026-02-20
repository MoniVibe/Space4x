using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XTradeBarterColumn : byte
    {
        PartyInventory = 0,
        PartyOffer = 1,
        CounterpartyInventory = 2,
        CounterpartyOffer = 3
    }

    public enum Space4XTradeBarterSide : byte
    {
        PartyA = 0,
        PartyB = 1
    }

    public enum Space4XTradeBarterSourceKind : byte
    {
        InventoryItem = 0,
        CargoResource = 1,
        MarketSellOffer = 2,
        MarketBuyDemand = 3
    }

    public enum Space4XTradeBarterCommandKind : byte
    {
        Offer = 0,
        Retract = 1,
        ClearOffers = 2,
        Commit = 3,
        Cancel = 4
    }

    public enum Space4XTradeBarterStatus : byte
    {
        None = 0,
        Opened = 1,
        Cancelled = 2,
        Accepted = 3,
        RejectedValueMismatch = 4,
        RejectedAvailability = 5,
        RejectedInvalidSession = 6
    }

    /// <summary>
    /// Singleton-like state for the active barter session.
    /// </summary>
    public struct Space4XTradeBarterState : IComponentData
    {
        public byte IsOpen;
        public uint SessionId;
        public uint LastProcessedGateTick;
        public byte EntryPointSlot;
        public Entity ContextEntity;
        public Entity PartyA;
        public Entity PartyB;
        public Entity PartyAInventoryEntity;
        public Entity PartyBInventoryEntity;
        public ushort PartyAFactionId;
        public ushort PartyBFactionId;
    }

    public struct Space4XTradeBarterConfig : IComponentData
    {
        public byte ColumnCount;
        public ushort MaxRowsPerColumn;
        public Space4XCurrencyId CurrencyId;
        public float RelativeValueTolerance;
        public float MinimumAbsoluteTolerance;
        public ushort RelativeValueToleranceBps;
        public long MinimumAbsoluteToleranceMicros;
        public byte RequireBalancedValue;
        public byte AutoCloseOnCommit;

        public static Space4XTradeBarterConfig Default => new Space4XTradeBarterConfig
        {
            ColumnCount = 4,
            MaxRowsPerColumn = 64,
            CurrencyId = Space4XCurrencyId.Credits,
            RelativeValueTolerance = 0.02f,
            MinimumAbsoluteTolerance = 0.5f,
            RelativeValueToleranceBps = 200,
            MinimumAbsoluteToleranceMicros = 500000,
            RequireBalancedValue = 1,
            AutoCloseOnCommit = 1
        };
    }

    /// <summary>
    /// View model rows consumed by desktop UI. Four columns are represented via Column.
    /// </summary>
    [InternalBufferCapacity(128)]
    public struct Space4XTradeBarterViewEntry : IBufferElementData
    {
        public Space4XTradeBarterColumn Column;
        public Space4XTradeBarterSide Side;
        public Space4XTradeBarterSourceKind SourceKind;
        public FixedString64Bytes ItemId;
        public FixedString64Bytes Label;
        public float QuantityAvailable;
        public float QuantityOffered;
        public Space4XCurrencyId CurrencyId;
        public long UnitPriceMicros;
        public float UnitValue;
        public float Quality;
        public float Durability;
        public ResourceType CargoResourceType;
        public MarketResourceType MarketResourceType;
    }

    [InternalBufferCapacity(64)]
    public struct Space4XTradeBarterOfferEntry : IBufferElementData
    {
        public Space4XTradeBarterSide Side;
        public Space4XTradeBarterSourceKind SourceKind;
        public FixedString64Bytes ItemId;
        public float Quantity;
        public Space4XCurrencyId CurrencyId;
        public long UnitPriceMicros;
        public float UnitValue;
        public float Quality;
        public float Durability;
        public ResourceType CargoResourceType;
        public MarketResourceType MarketResourceType;
    }

    [InternalBufferCapacity(16)]
    public struct Space4XTradeBarterCommand : IBufferElementData
    {
        public uint SessionId;
        public Space4XTradeBarterCommandKind Kind;
        public Space4XTradeBarterSide Side;
        public Space4XTradeBarterSourceKind SourceKind;
        public FixedString64Bytes ItemId;
        public float Quantity;
        public Space4XCurrencyId CurrencyId;
        public ResourceType CargoResourceType;
        public MarketResourceType MarketResourceType;
    }

    [InternalBufferCapacity(16)]
    public struct Space4XTradeBarterEvent : IBufferElementData
    {
        public uint SessionId;
        public Space4XTradeBarterStatus Status;
        public long ValueOfferedByAMicros;
        public long ValueOfferedByBMicros;
        public long NetDeltaMicros;
        public long FeePaidByAMicros;
        public long FeePaidByBMicros;
        public float ValueOfferedByA;
        public float ValueOfferedByB;
        public float NetDelta;
        public byte IsOpenAfter;
        public FixedString128Bytes Message;
    }

    /// <summary>
    /// Accepted lines from the most recent commit, consumed by downstream transfer systems.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct Space4XTradeBarterResolvedLine : IBufferElementData
    {
        public uint SessionId;
        public Space4XTradeBarterSide Side;
        public Space4XTradeBarterSourceKind SourceKind;
        public FixedString64Bytes ItemId;
        public float Quantity;
        public Space4XCurrencyId CurrencyId;
        public long UnitPriceMicros;
        public float UnitValue;
        public float Quality;
        public float Durability;
        public ResourceType CargoResourceType;
        public MarketResourceType MarketResourceType;
    }
}
