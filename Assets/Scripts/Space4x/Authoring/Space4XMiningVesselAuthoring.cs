using Space4X.Registry;
using Space4X.Runtime;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Interrupts;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Standalone authoring component for Space4X mining vessels.
    /// Creates MiningVessel and MiningJob components that work with Space4X mining systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XMiningVesselAuthoring : MonoBehaviour
    {
        [Header("Vessel Identity")]
        [Tooltip("Unique identifier for this vessel")]
        public string vesselId = "Vessel_01";

        [Header("Carrier Assignment")]
        [Tooltip("ID of the carrier this vessel delivers to (must match CarrierId on CarrierAuthoring)")]
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

        [Header("Physics")]
        [Range(0.1f, 5f)]
        public float collisionRadius = 0.6f;
        [Range(0.5f, 100f)]
        public float baseMass = 6f;
        [Range(0.1f, 5f)]
        public float hullDensity = 1.05f;
        [Range(0f, 1f)]
        public float cargoMassPerUnit = 0.04f;
        [Range(0f, 1f)]
        public float restitution = 0.15f;
        [Range(0f, 1f)]
        public float tangentialDamping = 0.3f;

        public class Baker : Unity.Entities.Baker<Space4XMiningVesselAuthoring>
        {
            public override void Bake(Space4XMiningVesselAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add MiningVessel component (Registry namespace)
                AddComponent(entity, new MiningVessel
                {
                    VesselId = new Unity.Collections.FixedString64Bytes(authoring.vesselId),
                    CarrierEntity = Entity.Null, // Will be linked by Space4XMiningScenarioAuthoring or manual setup
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

                // Seed AI+movement data so the Burst vessel systems can take over immediately
                AddComponent(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.None,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    StateTimer = 0f,
                    StateStartTick = 0
                });

                // Add EntityIntent component (defaults to Idle)
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

                // Add Interrupt buffer for interrupt-driven intent system
                AddBuffer<Interrupt>(entity);

                AddComponent(entity, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = math.max(0.1f, authoring.speed),
                    CurrentSpeed = 0f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = 0
                });

                AddComponent(entity, new VesselPhysicalProperties
                {
                    Radius = math.max(0.1f, authoring.collisionRadius),
                    BaseMass = math.max(0.5f, authoring.baseMass),
                    HullDensity = math.max(0.1f, authoring.hullDensity),
                    CargoMassPerUnit = math.max(0f, authoring.cargoMassPerUnit),
                    Restitution = math.clamp(authoring.restitution, 0f, 1f),
                    TangentialDamping = math.clamp(authoring.tangentialDamping, 0f, 1f)
                });

                AddBuffer<SpawnResourceRequest>(entity);

                AddComponent(entity, CommDecisionConfig.Default);
                AddComponent(entity, CommDecodeFactors.Default);

                // Add LocalTransform will be synced automatically
            }
        }
    }
}
