using Space4X.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Rendering.Authoring
{
    /// <summary>
    /// Assigns RenderKey + RenderFlags to GameObjects so ApplyRenderCatalogSystem can bind BRG data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RenderKeyAuthoring : MonoBehaviour
    {
        [Tooltip("Catalog ArchetypeId for this visual (carrier, miner, asteroid, etc.).")]
        public ushort ArchetypeId;

        [Tooltip("LOD bucket to request: 0 = full, 1 = mid, 2 = impostor.")]
        [Range(0, 2)]
        public byte LOD;

        [Header("Visibility Flags")]
        public bool Visible = true;
        public bool ShadowCaster = true;
        [Tooltip("Optional selection/highlight mask.")]
        public byte HighlightMask;

        private sealed class Baker : Unity.Entities.Baker<RenderKeyAuthoring>
        {
            public override void Bake(RenderKeyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);

                AddComponent(entity, new RenderKey
                {
                    ArchetypeId = authoring.ArchetypeId,
                    LOD = (byte)math.clamp(authoring.LOD, 0, 2)
                });

                AddComponent(entity, new RenderFlags
                {
                    Visible = (byte)(authoring.Visible ? 1 : 0),
                    ShadowCaster = (byte)(authoring.ShadowCaster ? 1 : 0),
                    HighlightMask = authoring.HighlightMask
                });
            }
        }
    }
}







