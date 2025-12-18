using PureDOTS.Rendering;
using PureDOTS.Runtime.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Space4X.Rendering;

public class Space4XDebugSpawnerAuthoring : MonoBehaviour
{
    public ushort ArchetypeId;
    
    public class Baker : Baker<Space4XDebugSpawnerAuthoring>
    {
        public override void Bake(Space4XDebugSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new RenderKey
            {
                ArchetypeId = authoring.ArchetypeId,
                LOD = 0
            });

            AddComponent(entity, new RenderFlags
            {
                Visible = 1,
                ShadowCaster = 1,
                HighlightMask = 0
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
