using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public struct CarrierModuleTelemetryLogState : IComponentData
    {
        public uint LastLoggedTick;
        public int LastTicketCount;
        public bool LastOverBudget;
    }
}
