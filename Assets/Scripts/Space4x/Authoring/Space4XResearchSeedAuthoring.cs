using Space4X.Systems.Research;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Research Seed")]
    public sealed class Space4XResearchSeedAuthoring : MonoBehaviour
    {
        public bool clearExisting = true;

        public sealed class Baker : Baker<Space4XResearchSeedAuthoring>
        {
            public override void Bake(Space4XResearchSeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Space4XResearchSeedRequest
                {
                    ClearExisting = (byte)(authoring.clearExisting ? 1 : 0)
                });
            }
        }
    }
}
