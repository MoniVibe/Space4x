using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public struct CarrierModuleTelemetry : IComponentData
    {
        public int CarrierCount;
        public int ActiveModules;
        public int DamagedModules;
        public int DestroyedModules;
        public int RepairTicketCount;
        public float TotalPowerDraw;
        public float TotalPowerGeneration;
        public float NetPower;
        public bool AnyOverBudget;
    }
}
