using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Hand;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Runtime.Interaction
{
    /// <summary>
    /// Tag component marking the god hand entity (camera/player controller).
    /// Used to identify the entity that can pick up and throw objects.
    /// </summary>
    public struct Space4XGodHandTag : IComponentData { }

    /// <summary>
    /// Ensures a singleton god hand entity exists with Space4XGodHandTag.
    /// Falls back to querying for camera transform if no tagged entity exists.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XGodHandBootstrapSystem : ISystem
    {
        private Entity _godHandEntity;

        public void OnCreate(ref SystemState state)
        {
            _godHandEntity = Entity.Null;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Try to find existing god hand entity
            var godHandQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag>()
                .Build();

            if (godHandQuery.IsEmpty)
            {
                // Create singleton god hand entity if it doesn't exist
                if (_godHandEntity == Entity.Null || !state.EntityManager.Exists(_godHandEntity))
                {
                    _godHandEntity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponent<Space4XGodHandTag>(_godHandEntity);
                    
                    // Add transform for position tracking
                    state.EntityManager.AddComponent<LocalTransform>(_godHandEntity);
                    state.EntityManager.SetComponentData(_godHandEntity, new LocalTransform
                    {
                        Position = float3.zero,
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });

                    // Add pickup state
                    state.EntityManager.AddComponent<PickupState>(_godHandEntity);
                    state.EntityManager.SetComponentData(_godHandEntity, new PickupState
                    {
                        State = PickupStateType.Empty,
                        LastRaycastPosition = float3.zero,
                        CursorMovementAccumulator = 0f,
                        HoldTime = 0f,
                        AccumulatedVelocity = float3.zero,
                        IsMoving = false,
                        TargetEntity = Entity.Null,
                        LastHolderPosition = float3.zero
                    });

                    // Add throw queue buffer
                    state.EntityManager.AddBuffer<ThrowQueue>(_godHandEntity);
                }
            }
            else
            {
                // Use existing entity
                _godHandEntity = godHandQuery.GetSingletonEntity();
            }
        }
    }

    /// <summary>
    /// Mirrors hand input ray pose into the god hand entity transform while divine mode is active.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(Space4XGodHandBootstrapSystem))]
    public partial struct Space4XGodHandPoseSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandInputFrame>();
            state.RequireForUpdate<Space4XControlModeRuntimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var mode = SystemAPI.GetSingleton<Space4XControlModeRuntimeState>();
            if (mode.IsDivineHandEnabled == 0)
            {
                return;
            }

            var handQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag, LocalTransform>()
                .Build();

            if (handQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var handEntity = handQuery.GetSingletonEntity();
            var input = SystemAPI.GetSingleton<HandInputFrame>();
            var direction = math.normalizesafe(input.RayDirection, new float3(0f, 0f, 1f));
            var rotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));
            var transform = SystemAPI.GetComponent<LocalTransform>(handEntity);
            transform.Position = input.RayOrigin;
            transform.Rotation = rotation;
            SystemAPI.SetComponent(handEntity, transform);
        }
    }
}
