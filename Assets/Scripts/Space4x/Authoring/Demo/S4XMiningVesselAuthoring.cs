using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring.Demo
{
    /// <summary>
    /// Simplified authoring component for Demo 0 mining vessels.
    /// Bakes MiningVessel, MiningJob, VesselAIState, VesselMovement components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class S4XMiningVesselAuthoring : MonoBehaviour
    {
        [Header("Vessel Identity")]
        [Tooltip("Unique identifier for this vessel")]
        public string vesselId = "Vessel_01";

        [Header("Carrier Assignment")]
        [Tooltip("ID of the carrier this vessel delivers to (optional, can be assigned at runtime)")]
        public string carrierId = "";

        [Header("Mining Properties")]
        [Range(0f, 1f)]
        [Tooltip("Mining efficiency multiplier")]
        public float miningEfficiency = 0.5f;

        [Header("Movement")]
        [Range(0.1f, 20f)]
        [Tooltip("Movement speed")]
        public float speed = 5f;

        [Header("Cargo")]
        [Range(1f, 1000f)]
        [Tooltip("Maximum cargo capacity")]
        public float cargoCapacity = 50f;

        public sealed class Baker : Unity.Entities.Baker<S4XMiningVesselAuthoring>
        {
            public override void Bake(S4XMiningVesselAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

                // Add MiningVessel component
                AddComponent(entity, new MiningVessel
                {
                    VesselId = new FixedString64Bytes(authoring.vesselId),
                    CarrierEntity = Entity.Null, // Will be linked at runtime if needed
                    MiningEfficiency = math.clamp(authoring.miningEfficiency, 0f, 1f),
                    Speed = math.max(0.1f, authoring.speed),
                    CargoCapacity = math.max(1f, authoring.cargoCapacity),
                    CurrentCargo = 0f,
                    CargoResourceType = ResourceType.Minerals
                });

                // Add MiningJob component
                AddComponent(entity, new MiningJob
                {
                    State = MiningJobState.None,
                    TargetAsteroid = Entity.Null,
                    MiningProgress = 0f
                });

                // Add vessel AI state
                AddComponent(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.None,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    StateTimer = 0f,
                    StateStartTick = 0
                });

                // Add vessel movement
                AddComponent(entity, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = math.max(0.1f, authoring.speed),
                    CurrentSpeed = 0f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = 0
                });

                // Add spawn resource request buffer
                AddBuffer<SpawnResourceRequest>(entity);

                // LocalTransform is automatically added by Unity's conversion system
            }
        }
    }
}



