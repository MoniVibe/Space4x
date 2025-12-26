using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Dire Tactics Policy")]
    public sealed class Space4XDireTacticsPolicyAuthoring : MonoBehaviour
    {
        public bool allowKamikaze = false;
        public bool allowExtremeOrders = false;

        public sealed class Baker : Baker<Space4XDireTacticsPolicyAuthoring>
        {
            public override void Bake(Space4XDireTacticsPolicyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new DireTacticsPolicy
                {
                    AllowKamikaze = (byte)(authoring.allowKamikaze ? 1 : 0),
                    AllowExtremeOrders = (byte)(authoring.allowExtremeOrders ? 1 : 0)
                });
            }
        }
    }
}
