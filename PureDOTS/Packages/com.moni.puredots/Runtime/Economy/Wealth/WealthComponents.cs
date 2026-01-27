using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Wealth
{
    /// <summary>
    /// Wealth tier enum for social stratification.
    /// </summary>
    public enum WealthTier : byte
    {
        UltraPoor = 0,
        Poor = 1,
        Mid = 2,
        High = 3,
        UltraHigh = 4
    }

    /// <summary>
    /// Individual villager/agent wealth component.
    /// </summary>
    public struct VillagerWealth : IComponentData
    {
        public float Balance;
        public WealthTier Tier;
        public FixedString64Bytes LastChangeSource;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Family/household shared wealth component.
    /// </summary>
    public struct FamilyWealth : IComponentData
    {
        public float Balance;
        public WealthTier Tier;
        public FixedString64Bytes LastChangeSource;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Dynasty/lineage wealth component.
    /// </summary>
    public struct DynastyWealth : IComponentData
    {
        public float Balance;
        public WealthTier Tier;
        public FixedString64Bytes LastChangeSource;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Business operating capital and retained earnings.
    /// </summary>
    public struct BusinessBalance : IComponentData
    {
        public float Cash;
        public float AccountsReceivable;
        public float AccountsPayable;
        public WealthTier Tier;
        public FixedString64Bytes LastChangeSource;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Guild/order/faction treasury component.
    /// </summary>
    public struct GuildTreasury : IComponentData
    {
        public float Balance;
        public WealthTier Tier;
        public FixedString64Bytes LastChangeSource;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Village/settlement public treasury component.
    /// </summary>
    public struct VillageTreasury : IComponentData
    {
        public float Balance;
        public WealthTier Tier;
        public FixedString64Bytes LastChangeSource;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Transaction type enum for categorization.
    /// </summary>
    public enum TransactionType : byte
    {
        Income = 0,
        Expense = 1,
        Transfer = 2,
        Exceptional = 3
    }

    /// <summary>
    /// Wealth transaction record buffer element.
    /// Every balance change must be represented as a transaction.
    /// </summary>
    public struct WealthTransaction : IBufferElementData
    {
        public Entity From;
        public Entity To;
        public float Amount;
        public TransactionType Type;
        public FixedString64Bytes Reason;
        public uint Tick;
        public FixedString128Bytes Context;
    }
}

