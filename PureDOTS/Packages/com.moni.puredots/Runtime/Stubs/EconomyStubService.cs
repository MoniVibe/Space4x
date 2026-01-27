// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Economy
{
    public static class EconomyServiceStub
    {
        public static void RegisterFacility(int facilityId) { }

        public static void EnqueueProduction(in Entity entity, int recipeId) { }

        public static InventorySummary GetInventorySummary(in Entity entity) => default;
    }
}
