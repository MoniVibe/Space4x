using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Space4X.Rendering;

using Space4XRenderKey = Space4X.Rendering.RenderKey;
using Space4XRenderFlags = Space4X.Rendering.RenderFlags;

public class Space4XDebugSpawnerAuthoring : MonoBehaviour
{
    public ushort ArchetypeId;
    
    public class Baker : Baker<Space4XDebugSpawnerAuthoring>
    {
        public override void Bake(Space4XDebugSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new Space4XRenderKey
            {
                ArchetypeId = authoring.ArchetypeId,
                LOD = 0
            });
            
            AddComponent(entity, new Space4XRenderFlags
            {
                Visible = 1,
                ShadowCaster = 1,
                HighlightMask = 0
            });
        }
    }
}
