using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public enum Space4XCurrencyId : byte
    {
        None = 0,
        Credits = 1
    }

    [InternalBufferCapacity(4)]
    public struct Space4XCurrencyBalance : IBufferElementData
    {
        public Space4XCurrencyId CurrencyId;
        public long AmountMicros;
    }

    public struct Space4XTradeFeePolicyConfig : IComponentData
    {
        public byte Enabled;
        public Space4XCurrencyId CurrencyId;
        public ushort BrokerFeeBps;
        public ushort SalesTaxBps;
        public ushort HostileSurchargeBps;
        public ushort RelationDiscountBpsAt100;
        public long FlatDockingFeeMicros;

        public static Space4XTradeFeePolicyConfig Default => new Space4XTradeFeePolicyConfig
        {
            Enabled = 1,
            CurrencyId = Space4XCurrencyId.Credits,
            BrokerFeeBps = 100,
            SalesTaxBps = 75,
            HostileSurchargeBps = 125,
            RelationDiscountBpsAt100 = 80,
            FlatDockingFeeMicros = 50000
        };
    }

    [InternalBufferCapacity(64)]
    public struct Space4XEconomyLedgerEvent : IBufferElementData
    {
        public uint Tick;
        public uint SessionId;
        public Entity Actor;
        public Entity Counterparty;
        public Space4XCurrencyId CurrencyId;
        public long GrossMicros;
        public long FeeMicros;
        public long NetMicros;
        public FixedString64Bytes ReasonId;
    }

    public static class Space4XMoneyMath
    {
        public const long MicrosPerUnit = 1000000L;

        public static long ToMicros(float units)
        {
            return (long)math.round((double)units * MicrosPerUnit);
        }

        public static float FromMicros(long micros)
        {
            return (float)((double)micros / MicrosPerUnit);
        }

        public static long ComputeLineMicros(float quantity, long unitPriceMicros)
        {
            if (quantity <= 0f || unitPriceMicros <= 0)
            {
                return 0;
            }

            return (long)math.round((double)quantity * unitPriceMicros);
        }

        public static long ApplyBps(long valueMicros, int bps)
        {
            if (valueMicros == 0 || bps == 0)
            {
                return 0;
            }

            return (long)math.round((double)valueMicros * bps / 10000d);
        }
    }
}
