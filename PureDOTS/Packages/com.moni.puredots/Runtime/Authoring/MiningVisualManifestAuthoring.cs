using PureDOTS.Runtime.Visuals;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class MiningVisualManifestAuthoring : MonoBehaviour
    {
        private sealed class Baker : Unity.Entities.Baker<MiningVisualManifestAuthoring>
        {
            public override void Bake(MiningVisualManifestAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);

                AddComponent(entity, new MiningVisualManifest());
                AddBuffer<MiningVisualRequest>(entity);
            }
        }
    }
}

