using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for service traits (ReactorWhisperer, StrikeWingMentor, TacticalSavant, etc.).
    /// Adds ServiceTrait buffer to entity.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Service Traits")]
    public sealed class ServiceTraitsAuthoring : MonoBehaviour
    {
        [Tooltip("List of service trait IDs")]
        public ServiceTraitId[] Traits = new ServiceTraitId[0];

        public sealed class Baker : Unity.Entities.Baker<ServiceTraitsAuthoring>
        {
            public override void Bake(ServiceTraitsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<ServiceTrait>(entity);

                if (authoring.Traits != null)
                {
                    foreach (var traitId in authoring.Traits)
                    {
                        if (traitId != ServiceTraitId.None)
                        {
                            buffer.Add(new ServiceTrait
                            {
                                TraitId = traitId
                            });
                        }
                    }
                }
            }
        }
    }
}
