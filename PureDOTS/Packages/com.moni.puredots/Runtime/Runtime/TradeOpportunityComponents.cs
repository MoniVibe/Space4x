using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Economy
{
    public struct InventoryFlowState : IComponentData
    {
        public float LastUnits;
        public float SmoothedInflow;
        public float SmoothedOutflow;
        public uint LastUpdateTick;
    }

    public struct InventoryFlowSettings : IComponentData
    {
        public float Smoothing;

        public static InventoryFlowSettings CreateDefault()
        {
            return new InventoryFlowSettings
            {
                Smoothing = 0.25f
            };
        }
    }

    public struct TradeOpportunity : IBufferElementData, System.IComparable<TradeOpportunity>
    {
        public FixedString64Bytes ResourceId;
        public Entity Source;
        public Entity Destination;
        public float PriceSpread;
        public float AvailableUnits;
        public uint Tick;

        public int CompareTo(TradeOpportunity other)
        {
            var resCompare = ResourceId.CompareTo(other.ResourceId);
            if (resCompare != 0)
            {
                return resCompare;
            }

            var spreadCompare = other.PriceSpread.CompareTo(PriceSpread);
            if (spreadCompare != 0)
            {
                return spreadCompare;
            }

            return Source.Index.CompareTo(other.Source.Index);
        }
    }

    public struct TradeOpportunityState : IComponentData
    {
        public uint Version;
        public uint LastUpdateTick;
        public int OpportunityCount;
    }

    public struct TradeOpportunitySettings : IComponentData
    {
        public float MinSpread;
        public float SupplyFillThreshold;
        public float DemandFillThreshold;
        public float MinTradeUnits;
        public int MaxOpportunities;

        public static TradeOpportunitySettings CreateDefault()
        {
            return new TradeOpportunitySettings
            {
                MinSpread = 0.05f,
                SupplyFillThreshold = 0.65f,
                DemandFillThreshold = 0.4f,
                MinTradeUnits = 1f,
                MaxOpportunities = 32
            };
        }
    }

    public struct TradeRouteRequestTag : IComponentData
    {
        public uint Version;
    }

    public struct TradeRoutingState : IComponentData
    {
        public uint LastProcessedVersion;
    }
}
