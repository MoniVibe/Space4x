using Space4X.Registry;
using PureDOTS.Runtime.Communication;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that marks an entity as a capital ship.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Capital Ship")]
    public sealed class CapitalShipAuthoring : MonoBehaviour
    {
        public sealed class Baker : Unity.Entities.Baker<CapitalShipAuthoring>
        {
            public override void Bake(CapitalShipAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Registry.CapitalShipTag>(entity);

                if (!HasComponent<CommDecisionConfig>(entity))
                {
                    AddComponent(entity, CommDecisionConfig.Default);
                }

                if (!HasComponent<CommDecodeFactors>(entity))
                {
                    AddComponent(entity, CommDecodeFactors.Default);
                }
            }
        }
    }
}
