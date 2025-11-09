using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Standalone authoring component for Space4X carrier ships.
    /// Creates Carrier, PatrolBehavior, and MovementCommand components that work with Space4XDemoSystems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCarrierAuthoring : MonoBehaviour
    {
        [Header("Carrier Identity")]
        [Tooltip("Unique identifier for this carrier")]
        public string carrierId = "Carrier_01";

        [Header("Patrol Settings")]
        [Tooltip("Center point for patrol area")]
        public Vector3 patrolCenter = Vector3.zero;

        [Range(1f, 100f)]
        [Tooltip("Radius of patrol area")]
        public float patrolRadius = 20f;

        [Range(0f, 10f)]
        [Tooltip("Time to wait at each waypoint")]
        public float waitTime = 2f;

        [Header("Movement")]
        [Range(0.1f, 20f)]
        [Tooltip("Movement speed")]
        public float speed = 3f;

        [Header("Storage")]
        [Tooltip("Resource storage configurations")]
        public ResourceStorageConfig[] resourceStorages = new ResourceStorageConfig[]
        {
            new ResourceStorageConfig { type = ResourceType.Minerals, capacity = 10000f },
            new ResourceStorageConfig { type = ResourceType.RareMetals, capacity = 10000f },
            new ResourceStorageConfig { type = ResourceType.EnergyCrystals, capacity = 10000f },
            new ResourceStorageConfig { type = ResourceType.OrganicMatter, capacity = 10000f }
        };

        [System.Serializable]
        public class ResourceStorageConfig
        {
            public ResourceType type;
            [Min(0f)] public float capacity = 10000f;
        }

        public class Baker : Unity.Entities.Baker<Space4XCarrierAuthoring>
        {
            public override void Bake(Space4XCarrierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add Carrier component (Registry namespace)
                AddComponent(entity, new Carrier
                {
                    CarrierId = new FixedString64Bytes(authoring.carrierId),
                    AffiliationEntity = Entity.Null, // Can be set up separately if needed
                    Speed = math.max(0.1f, authoring.speed),
                    PatrolCenter = authoring.patrolCenter,
                    PatrolRadius = math.max(1f, authoring.patrolRadius)
                });

                // Add PatrolBehavior component
                AddComponent(entity, new PatrolBehavior
                {
                    CurrentWaypoint = float3.zero, // Will be initialized by CarrierPatrolSystem
                    WaitTime = math.max(0f, authoring.waitTime),
                    WaitTimer = 0f
                });

                // Add MovementCommand component
                AddComponent(entity, new MovementCommand
                {
                    TargetPosition = float3.zero, // Will be set by CarrierPatrolSystem
                    ArrivalThreshold = 1f
                });

                // Add ResourceStorage buffer
                var storageBuffer = AddBuffer<ResourceStorage>(entity);
                foreach (var config in authoring.resourceStorages)
                {
                    storageBuffer.Add(ResourceStorage.Create(config.type, config.capacity));
                }

                // Add LocalTransform will be synced automatically
            }
        }
    }
}

