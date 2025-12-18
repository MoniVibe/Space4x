using PureDOTS.Rendering;
using PureDOTS.Runtime.Rendering;
using Space4X.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Space4X.Rendering.Authoring
{
    /// <summary>
    /// Assigns presentation components so ApplyRenderVariantSystem can bind BRG data.
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

                AddComponent(entity, new RenderSemanticKey
                {
                    Value = authoring.ArchetypeId
                });

                AddComponent(entity, new RenderVariantKey
                {
                    Value = 0
                });

                AddComponent<RenderThemeOverride>(entity);
                SetComponentEnabled<RenderThemeOverride>(entity, false);

                AddComponent<MeshPresenter>(entity);
                AddComponent<SpritePresenter>(entity);
                SetComponentEnabled<SpritePresenter>(entity, false);
                AddComponent<DebugPresenter>(entity);
                SetComponentEnabled<DebugPresenter>(entity, false);

                AddComponent(entity, new RenderLODData
                {
                    CameraDistance = 0f,
                    ImportanceScore = 1f,
                    RecommendedLOD = 0,
                    LastUpdateTick = 0
                });

                AddComponent(entity, new RenderCullable
                {
                    CullDistance = 1000f,
                    Priority = 100
                });

                AddComponent(entity, new RenderSampleIndex
                {
                    SampleIndex = 0,
                    SampleModulus = 1,
                    ShouldRender = 1
                });

                AddComponent(entity, new RenderTint { Value = new float4(1f, 1f, 1f, 1f) });
                AddComponent(entity, new RenderTexSlice { Value = 0 });
                AddComponent(entity, new RenderUvTransform { Value = new float4(1f, 1f, 0f, 0f) });
            }
        }
    }
}








