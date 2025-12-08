using Space4X.Registry;
using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Authoring.Demo
{
    /// <summary>
    /// Simplified authoring component for Demo 0 carriers.
    /// Bakes CarrierTag and optional movement components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class S4XCarrierAuthoring : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Base movement speed (0 = stationary)")]
        public float speed = 0f;

        [Tooltip("Enable movement components")]
        public bool enableMovement = false;

        public sealed class Baker : Unity.Entities.Baker<S4XCarrierAuthoring>
        {
            public override void Bake(S4XCarrierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

                // Add carrier tag
                AddComponent<CarrierTag>(entity);

                // Add movement components if enabled
                if (authoring.enableMovement && authoring.speed > 0f)
                {
                    AddComponent(entity, new VesselMovement
                    {
                        Velocity = float3.zero,
                        BaseSpeed = math.max(0.1f, authoring.speed),
                        CurrentSpeed = 0f,
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
                }

                // LocalTransform is automatically added by Unity's conversion system
            }
        }
    }
}



