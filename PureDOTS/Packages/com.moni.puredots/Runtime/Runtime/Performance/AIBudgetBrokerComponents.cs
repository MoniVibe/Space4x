using Unity.Entities;

namespace PureDOTS.Runtime.Performance
{
    /// <summary>
    /// Per-tick credit state for expensive AI/perception work (LOS, decision depth, path requests).
    /// Reset once per tick by the broker system; consumed by systems during the frame.
    /// </summary>
    public struct AIBudgetBrokerState : IComponentData
    {
        public uint Tick;

        public int RemainingLosRays;
        public int RemainingDecisionUpdates;
        public int RemainingPathRequests;

        // Soft-cap tracking (patience/debt), globally aggregated for now.
        public int DeferredLosRays;
        public int DeferredDecisions;
        public int DeferredPathRequests;
    }
}


