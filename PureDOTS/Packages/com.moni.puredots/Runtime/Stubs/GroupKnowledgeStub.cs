// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Cooperation
{
    public static class GroupKnowledgeStub
    {
        public static void ShareKnowledge(in Entity group, FixedString64Bytes knowledgeId, KnowledgeType type) { }

        public static bool HasKnowledge(in Entity group, FixedString64Bytes knowledgeId) => false;

        public static void DiffuseKnowledge(in Entity sourceGroup, in Entity targetGroup, FixedString64Bytes knowledgeId) { }
    }
}

