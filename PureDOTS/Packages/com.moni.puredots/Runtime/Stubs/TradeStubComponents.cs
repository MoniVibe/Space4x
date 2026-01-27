// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Trade
{
    public struct MerchantInventory : IComponentData
    {
        public float Capacity;
        public float CurrentMass;
    }

    public struct TradeOffer : IComponentData
    {
        public int ProductId;
        public float Price;
        public float Quantity;
    }

    public struct TradeIntent : IComponentData
    {
        public int TargetEntityId;
        public byte Action; // 0 = none, 1 = buy, 2 = sell
    }

    public struct TradeLedgerEntry : IBufferElementData
    {
        public int TransactionId;
        public int ProductId;
        public float Quantity;
        public float UnitPrice;
    }
}
