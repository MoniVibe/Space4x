using Space4X.Registry;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring.Demo
{
    /// <summary>
    /// Simplified authoring component for Demo 0 asteroid resource nodes.
    /// Bakes Asteroid, ResourceSourceConfig, ResourceSourceState components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class S4XAsteroidResourceAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Asteroid ID")]
        public string AsteroidId = "Asteroid_01";

        [Header("Resources")]
        [Tooltip("Resource type")]
        public ResourceType ResourceType = ResourceType.Minerals;

        [Tooltip("Initial resource amount")]
        public float ResourceAmount = 1000f;

        [Tooltip("Maximum resource amount")]
        public float MaxResourceAmount = 1000f;

        [Tooltip("Mining rate per worker per second")]
        public float MiningRate = 10f;

        public sealed class Baker : Unity.Entities.Baker<S4XAsteroidResourceAuthoring>
        {
            public override void Bake(S4XAsteroidResourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

                // Add Asteroid component
                AddComponent(entity, new Asteroid
                {
                    AsteroidId = new FixedString64Bytes(authoring.AsteroidId),
                    ResourceType = authoring.ResourceType,
                    ResourceAmount = math.max(0f, authoring.ResourceAmount),
                    MaxResourceAmount = math.max(authoring.ResourceAmount, authoring.MaxResourceAmount),
                    MiningRate = math.max(0.1f, authoring.MiningRate)
                });

                // Add resource type ID
                var resourceTypeId = GetResourceTypeId(authoring.ResourceType);
                if (!resourceTypeId.IsEmpty)
                {
                    AddComponent(entity, new ResourceTypeId { Value = resourceTypeId });
                }

                // Add resource source configuration
                var maxWorkers = math.clamp((int)math.ceil(authoring.MiningRate / 5f), 1, 16);
                AddComponent(entity, new ResourceSourceConfig
                {
                    GatherRatePerWorker = math.max(0.1f, authoring.MiningRate),
                    MaxSimultaneousWorkers = (ushort)maxWorkers,
                    RespawnSeconds = 0f,
                    LessonId = default,
                    Flags = 0
                });

                // Add resource source state
                AddComponent(entity, new ResourceSourceState
                {
                    SourceType = ResourceSourceType.Natural,
                    UnitsRemaining = math.max(0f, authoring.ResourceAmount),
                    QualityTier = ResourceQualityTier.Normal,
                    BaseQuality = 50,
                    QualityVariance = 10
                });

                // LocalTransform is automatically added by Unity's conversion system
            }

            private static FixedString64Bytes GetResourceTypeId(ResourceType resourceType)
            {
                switch (resourceType)
                {
                    case ResourceType.Minerals:
                        return new FixedString64Bytes("space4x.resource.minerals");
                    case ResourceType.RareMetals:
                        return new FixedString64Bytes("space4x.resource.rare_metals");
                    case ResourceType.EnergyCrystals:
                        return new FixedString64Bytes("space4x.resource.energy_crystals");
                    case ResourceType.OrganicMatter:
                        return new FixedString64Bytes("space4x.resource.organic_matter");
                    default:
                        return new FixedString64Bytes("space4x.resource.unknown");
                }
            }
        }
    }
}



