#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using PureDOTS.Runtime.Power.Environment;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Power
{
    /// <summary>
    /// Authoring component for power generators (solar, wind, reactors, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerSourceAuthoring : MonoBehaviour
    {
        [Header("Source Definition")]
        public int sourceDefId = 0;
        public PowerSourceKind kind = PowerSourceKind.Solar;

        [Header("Network")]
        public PowerDomain domain = PowerDomain.GroundLocal;
        public int networkId = 0;

        [Header("Node")]
        public float localLoss = 0.05f;
        public float quality = 1f;

        [Header("Environment (Solar)")]
        public Entity starEntity = Entity.Null;
        public float distanceAU = 1f;
        public float exposureFactor = 1f;

        [Header("Environment (Wind)")]
        public float windIntensity = 1f;
        public float terrainModifier = 1f;

        [Header("State")]
        public bool online = true;
        public float wear = 0f;
    }

    public sealed class PowerSourceBaker : Baker<PowerSourceAuthoring>
    {
        public override void Bake(PowerSourceAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var position = authoring.transform.position;

            // Power source state
            AddComponent(entity, new PowerSourceState
            {
                SourceDefId = authoring.sourceDefId,
                CurrentOutput = 0f,
                MaxOutput = 0f,
                Wear = authoring.wear,
                MaintenanceDebt = 0f,
                Online = authoring.online ? (byte)1 : (byte)0
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
                Type = PowerNodeType.Source,
                WorldPosition = position,
                LocalLoss = authoring.localLoss,
                Quality = authoring.quality
            });

            // Environment components based on kind
            if (authoring.kind == PowerSourceKind.Solar)
            {
                if (authoring.starEntity != Entity.Null)
                {
                    AddComponent(entity, new LocalSunExposure
                    {
                        Star = authoring.starEntity,
                        DistanceAU = authoring.distanceAU,
                        ExposureFactor = authoring.exposureFactor
                    });
                }
            }
            else if (authoring.kind == PowerSourceKind.Wind)
            {
                AddComponent(entity, new WindCell
                {
                    Intensity = authoring.windIntensity,
                    Direction = math.up()
                });

                AddComponent(entity, new TerrainWindModifier
                {
                    BaseModifier = authoring.terrainModifier
                });
            }

            // Infrastructure condition
            AddComponent(entity, new InfrastructureCondition
            {
                Wear = authoring.wear,
                FaultRisk = 0f,
                State = InfrastructureState.Normal
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

