// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Reputation
{
    public static class ReputationServiceStub
    {
        public static void ModifyReputation(in Entity entity, in Entity observer, ReputationDomain domain, float delta) { }

        public static void SpreadGossip(in Entity entity, in Entity observer, ReputationDomain domain, float amount) { }

        public static ReputationTier CalculateReputationTier(in Entity entity, in Entity observer, ReputationDomain domain) => ReputationTier.Neutral;

        public static float GetReputationScore(in Entity entity, in Entity observer, ReputationDomain domain) => 0f;
    }
}

