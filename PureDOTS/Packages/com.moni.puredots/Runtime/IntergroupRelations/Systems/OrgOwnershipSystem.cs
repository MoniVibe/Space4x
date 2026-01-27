#if TRI_ENABLE_INTERGROUP_RELATIONS
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.IntergroupRelations
{
    // [TRI-STUB] Org ownership logic disabled in MVP baseline.
    [BurstCompile]
    public partial struct OrgOwnershipSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            return;
        }
    }
}
#else
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.IntergroupRelations
{
    // [TRI-STUB] Org ownership logic disabled in MVP baseline.
    [BurstCompile]
    public partial struct OrgOwnershipSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            return;
        }
    }
}
#endif