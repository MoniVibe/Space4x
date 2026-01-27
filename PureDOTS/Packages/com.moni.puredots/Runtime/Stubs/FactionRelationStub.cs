// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    public static class FactionRelationStub
    {
        public static void UpdateFactionRelation(in Entity factionA, in Entity factionB, float delta) { }

        public static FactionRelationType GetFactionRelationType(in Entity factionA, in Entity factionB) => FactionRelationType.Neutral;

        public static void DeclareWar(in Entity factionA, in Entity factionB) { }

        public static void FormAlliance(in Entity factionA, in Entity factionB) { }
    }
}

