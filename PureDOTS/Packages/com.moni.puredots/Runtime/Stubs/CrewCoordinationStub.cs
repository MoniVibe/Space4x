// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Cooperation
{
    public static class CrewCoordinationStub
    {
        public static void CreateOperatorPilotLink(in Entity operatorEntity, in Entity pilotEntity) { }

        public static float GetLinkQuality(in Entity operatorEntity, in Entity pilotEntity) => 0f;

        public static void CreateHangarOperation(in Entity hangarBay) { }

        public static float GetOperationalEfficiency(in Entity hangarBay) => 0f;
    }
}

