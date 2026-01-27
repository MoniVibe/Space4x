#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Power
{
    /// <summary>
    /// Authoring component for power network entities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerNetworkAuthoring : MonoBehaviour
    {
        [Header("Network")]
        public int networkId = 0;
        public PowerDomain domain = PowerDomain.GroundLocal;
    }

    public sealed class PowerNetworkBaker : Baker<PowerNetworkAuthoring>
    {
        public override void Bake(PowerNetworkAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);

            // Power network component
            AddComponent(entity, new PowerNetwork
            {
                NetworkId = authoring.networkId,
                Domain = authoring.domain
            });

            // Edge buffer
            AddBuffer<PowerEdge>(entity);

            // Standard tags
            AddComponent<RewindableTag>(entity);
        }
    }
}
#endif

