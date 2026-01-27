// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    public static class RelationDecayStub
    {
        public static void DecayRelations(float deltaTime) { }

        public static float CalculateDecayAmount(float relationValue, float decayRate, float timeSinceInteraction) => 0f;

        public static void UpdateInteractionTimestamp(in Entity source, in Entity target) { }
    }
}

