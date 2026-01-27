// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Crafting
{
    public static class CraftingServiceStub
    {
        public static CraftingJobTicket ScheduleJob(in Entity facility, int recipeId) => default;

        public static void ConsumeMaterials(in Entity facility, int recipeId) { }

        public static float EvaluateQuality(in CraftingFormulaParams formula, float skill, float materialQuality) => 0f;
    }
}
