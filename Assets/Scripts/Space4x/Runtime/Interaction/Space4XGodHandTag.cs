using PureDOTS.Runtime.Interaction;
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
}

