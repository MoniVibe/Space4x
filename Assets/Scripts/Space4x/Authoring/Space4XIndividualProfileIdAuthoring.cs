using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Individual Profile Id")]
    public sealed class Space4XIndividualProfileIdAuthoring : MonoBehaviour
    {
        public string profileId = "baseline";

        public sealed class Baker : Unity.Entities.Baker<Space4XIndividualProfileIdAuthoring>
        {
            public override void Bake(Space4XIndividualProfileIdAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var id = new FixedString64Bytes(authoring.profileId ?? string.Empty);
                AddComponent(entity, new IndividualProfileId { Id = id });
            }
        }
    }
}
