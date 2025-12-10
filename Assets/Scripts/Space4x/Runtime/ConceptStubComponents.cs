using Unity.Entities;

namespace Space4X.Runtime
{
    // STUB: placeholder tags/data to unblock scene wiring while full systems are authored.

    public struct MiningOrderTag : IComponentData { }

    public struct HaulOrderTag : IComponentData { }

    public struct ExplorationProbeTag : IComponentData { }

    public struct FleetInterceptIntent : IComponentData
    {
        public Entity Target;
        public byte Priority;
    }

    public struct ComplianceBreachEvent : IComponentData
    {
        public Entity Source;
        public byte Severity;
    }

    public struct TechDiffusionNode : IComponentData
    {
        public int NodeId;
        public byte Tier;
    }

    public struct StationConstructionSite : IComponentData
    {
        public int SiteId;
        public byte Phase; // planned/foundation/build/finish
    }

    public struct AnomalyTag : IComponentData { }

    public struct TradeContractHandle : IComponentData
    {
        public int ContractId;
    }
}
