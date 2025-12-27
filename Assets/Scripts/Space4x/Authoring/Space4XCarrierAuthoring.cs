using Space4X.Registry;
using Space4X.Runtime;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Communication;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Standalone authoring component for Space4X carrier ships.
    /// Creates Carrier, PatrolBehavior, and MovementCommand components that work with Space4X simulation systems.
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

        [Header("Motion Profile")]
        [Range(0.05f, 10f)]
        [Tooltip("Acceleration in units per second squared")]
        public float acceleration = 0.6f;

        [Range(0.05f, 10f)]
        [Tooltip("Deceleration in units per second squared")]
        public float deceleration = 0.9f;

        [Range(0.05f, 5f)]
        [Tooltip("Turn speed in radians per second")]
        public float turnSpeed = 0.35f;

        [Range(1f, 100f)]
        [Tooltip("Distance to start slowing down when approaching targets")]
        public float slowdownDistance = 12f;

        [Range(0.5f, 20f)]
        [Tooltip("Arrival threshold for stopping at targets")]
        public float arrivalDistance = 2.5f;

        [Header("Physics")]
        [Range(0.2f, 20f)]
        public float collisionRadius = 2.6f;
        [Range(1f, 1000f)]
        public float baseMass = 120f;
        [Range(0.1f, 5f)]
        public float hullDensity = 1.2f;
        [Range(0.0f, 1f)]
        public float cargoMassPerUnit = 0.02f;
        [Range(0f, 1f)]
        public float restitution = 0.08f;
        [Range(0f, 1f)]
        public float tangentialDamping = 0.25f;

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
                    Acceleration = math.max(0.01f, authoring.acceleration),
                    Deceleration = math.max(0.01f, authoring.deceleration),
                    TurnSpeed = math.max(0.01f, authoring.turnSpeed),
                    SlowdownDistance = math.max(0.1f, authoring.slowdownDistance),
                    ArrivalDistance = math.max(0.1f, authoring.arrivalDistance),
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

                AddComponent(entity, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = math.max(0.1f, authoring.speed),
                    CurrentSpeed = 0f,
                    Acceleration = math.max(0.01f, authoring.acceleration),
                    Deceleration = math.max(0.01f, authoring.deceleration),
                    TurnSpeed = math.max(0.01f, authoring.turnSpeed),
                    SlowdownDistance = math.max(0.1f, authoring.slowdownDistance),
                    ArrivalDistance = math.max(0.1f, authoring.arrivalDistance),
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = 0
                });

                AddComponent(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.None,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    StateTimer = 0f,
                    StateStartTick = 0
                });

                AddComponent(entity, new EntityIntent
                {
                    Mode = IntentMode.Idle,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    TriggeringInterrupt = InterruptType.None,
                    IntentSetTick = 0,
                    Priority = InterruptPriority.Low,
                    IsValid = 0
                });

                AddBuffer<Interrupt>(entity);

                AddComponent(entity, new PostTransformMatrix
                {
                    Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f))
                });

                AddComponent(entity, new VesselPhysicalProperties
                {
                    Radius = math.max(0.1f, authoring.collisionRadius),
                    BaseMass = math.max(1f, authoring.baseMass),
                    HullDensity = math.max(0.1f, authoring.hullDensity),
                    CargoMassPerUnit = math.max(0f, authoring.cargoMassPerUnit),
                    Restitution = math.clamp(authoring.restitution, 0f, 1f),
                    TangentialDamping = math.clamp(authoring.tangentialDamping, 0f, 1f)
                });

                // Add ResourceStorage buffer
                var storageBuffer = AddBuffer<ResourceStorage>(entity);
                foreach (var config in authoring.resourceStorages)
                {
                    storageBuffer.Add(ResourceStorage.Create(config.type, config.capacity));
                }

                AddComponent(entity, CommDecisionConfig.Default);
                AddComponent(entity, CommDecodeFactors.Default);

                // Add LocalTransform will be synced automatically
            }
        }
    }
}
