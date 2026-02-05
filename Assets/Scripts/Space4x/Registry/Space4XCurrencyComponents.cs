using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Currency issuer classification.
    /// </summary>
    public enum CurrencyIssuerType : byte
    {
        Empire = 0,
        Guild = 1,
        Faction = 2
    }

    /// <summary>
    /// Declares an entity that issues a currency.
    /// </summary>
    public struct CurrencyIssuer : IComponentData
    {
        public FixedString64Bytes CurrencyId;
        public CurrencyIssuerType IssuerType;
        public half MemberDiscount;
        public half RequiredStanding;
    }

    /// <summary>
    /// Primary currency used by an entity.
    /// </summary>
    public struct PrimaryCurrency : IComponentData
    {
        public FixedString64Bytes CurrencyId;
    }

    /// <summary>
    /// Currency balance entry for multi-currency holders.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct CurrencyBalanceEntry : IBufferElementData
    {
        public FixedString64Bytes CurrencyId;
        public float Balance;
    }

    /// <summary>
    /// Optional override for business currency selection.
    /// </summary>
    public struct BusinessCurrencyOverride : IComponentData
    {
        public FixedString64Bytes CurrencyId;
    }
}
