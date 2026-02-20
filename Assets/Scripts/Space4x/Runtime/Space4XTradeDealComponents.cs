using Unity.Collections;
using Unity.Entities;
using Space4X.Registry;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum Space4XTradeDealStatus : byte
    {
        None = 0,
        PendingExecution = 1,
        DeferredBusy = 2,
        DeferredRange = 3,
        Executing = 4,
        Completed = 5,
        Failed = 6,
        Expired = 7,
        Cancelled = 8
    }

    public enum Space4XTradeDealFailureReason : byte
    {
        None = 0,
        NoOffer = 1,
        InvalidEntity = 2,
        Busy = 3,
        OutOfRange = 4,
        OfferUnavailable = 5,
        SellerSupplyUnavailable = 6,
        BuyerCapacityUnavailable = 7,
        BuyerFundsUnavailable = 8,
        CurrencyTransferFailed = 9,
        Expired = 10
    }

    public struct Space4XTradeDealState : IComponentData
    {
        public uint NextDealId;
        public uint LastProposalTick;
    }

    public struct Space4XTradeDealConfig : IComponentData
    {
        public byte Enabled;
        public Space4XCurrencyId CurrencyId;
        public float ExecutionRange;
        public float TransferRatePerTick;
        public uint RetryBusyTicks;
        public uint RetryRangeTicks;
        public uint ExpiryTicks;
        public float BaseLotQuantity;
        public float DesperationLotScale;
        public half BaseAcceptableMarkup;
        public half DesperationMarkupScale;
        public half ScarcityMarkupScale;
        public half RelationBaseGoodwill;
        public half RelationGoodPriceWeight;
        public half RelationExtortionWeight;
        public half RelationDesperationScale;
        public float RelationValueScaleUnits;
        public sbyte RelationDeltaClampPerStep;

        public static Space4XTradeDealConfig Default => new Space4XTradeDealConfig
        {
            Enabled = 1,
            CurrencyId = Space4XCurrencyId.Credits,
            ExecutionRange = 8f,
            TransferRatePerTick = 5f,
            RetryBusyTicks = 15u,
            RetryRangeTicks = 12u,
            ExpiryTicks = 900u,
            BaseLotQuantity = 4f,
            DesperationLotScale = 3f,
            BaseAcceptableMarkup = (half)0.2f,
            DesperationMarkupScale = (half)1.2f,
            ScarcityMarkupScale = (half)0.8f,
            RelationBaseGoodwill = (half)0.6f,
            RelationGoodPriceWeight = (half)3f,
            RelationExtortionWeight = (half)4f,
            RelationDesperationScale = (half)1.25f,
            RelationValueScaleUnits = 20f,
            RelationDeltaClampPerStep = 8
        };
    }

    [InternalBufferCapacity(16)]
    public struct Space4XTradeDealContract : IBufferElementData
    {
        public uint DealId;
        public uint SourceCorrelationId;
        public Space4XInteractionIntentAction SourceAction;
        public Space4XTradeDealStatus Status;
        public Space4XTradeDealFailureReason LastFailure;
        public Entity Buyer;
        public Entity Seller;
        public ushort BuyerFactionId;
        public ushort SellerFactionId;
        public MarketResourceType MarketResource;
        public ResourceType CargoResource;
        public Space4XCurrencyId CurrencyId;
        public long UnitPriceMicros;
        public long ReferenceUnitPriceMicros;
        public float QuantityTotal;
        public float QuantityRemaining;
        public float Desperation;
        public float Scarcity;
        public uint CreatedTick;
        public uint ExpiresTick;
        public uint NextAttemptTick;
    }

    [InternalBufferCapacity(64)]
    public struct Space4XTradeDealEvent : IBufferElementData
    {
        public uint Tick;
        public uint DealId;
        public Space4XTradeDealStatus Status;
        public Space4XTradeDealFailureReason Failure;
        public Entity Buyer;
        public Entity Seller;
        public MarketResourceType MarketResource;
        public float QuantityDelta;
        public float QuantityRemaining;
        public long ValueMicros;
        public FixedString64Bytes MessageId;
    }
}
