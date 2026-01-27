using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component that exposes the Divine Hand influence ring in scenes.
    /// Attach to village centers, temples, or any gameplay object that should bestow interaction rights.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InfluenceSourceAuthoring : MonoBehaviour
    {
        [Min(0.1f)] public float radius = 25f;
        [Min(0)] public byte playerId = 0;
        public InfluenceSourceFlags flags = InfluenceSourceFlags.Village;

        private sealed class Baker : Unity.Entities.Baker<InfluenceSourceAuthoring>
        {
            public override void Bake(InfluenceSourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new InfluenceSource
                {
                    Radius = Mathf.Max(0.1f, authoring.radius),
                    PlayerId = authoring.playerId,
                    Flags = authoring.flags
                });
            }
        }
    }
}
