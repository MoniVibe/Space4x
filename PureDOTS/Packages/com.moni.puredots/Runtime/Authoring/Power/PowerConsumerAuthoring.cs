#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Power
{
    /// <summary>
    /// Authoring component for power consumers (buildings, ship modules, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerConsumerAuthoring : MonoBehaviour
    {
        [Header("Consumer Definition")]
        public int consumerDefId = 0;
        public float baseDemand = 10f;
        public float minOperationalFraction = 0.5f;
        public byte priorityTier = 0;

        [Header("Network")]
        public PowerDomain domain = PowerDomain.GroundLocal;
        public int networkId = 0;

        [Header("Node")]
        public float localLoss = 0f;
        public float quality = 1f;

        [Header("State")]
        public float requestedDemand = 10f;
    }

    public sealed class PowerConsumerBaker : Baker<PowerConsumerAuthoring>
    {
        public override void Bake(PowerConsumerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var position = authoring.transform.position;

            // Power consumer state
            AddComponent(entity, new PowerConsumerState
            {
                ConsumerDefId = authoring.consumerDefId,
                RequestedDemand = authoring.requestedDemand,
                Supplied = 0f,
                Online = 0
            });

            // Power node
            AddComponent(entity, new PowerNode
            {
                NodeId = entity.Index,
                Network = new PowerNetworkRef
                {
                    NetworkId = authoring.networkId,
                    Domain = authoring.domain
                },
                Type = PowerNodeType.Consumer,
                WorldPosition = position,
                LocalLoss = authoring.localLoss,
                Quality = authoring.quality
            });

            // Standard tags
            AddComponent<RewindableTag>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Default,
                OverrideStrideSeconds = 0f
            });
        }
    }
}
#endif

