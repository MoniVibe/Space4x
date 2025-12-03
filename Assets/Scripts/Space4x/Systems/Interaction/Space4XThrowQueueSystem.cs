using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Handles throw queue release: one-by-one or all-together via hotkeys.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XThrowSystem))]
    public partial struct Space4XThrowQueueSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Unity.Physics.PhysicsVelocity> _physicsVelocityLookup;
        private EntityQuery _godHandQuery;

        // Hotkey mappings (can be configured)
        private const Key ReleaseOneKey = Key.Digit1;
        private const Key ReleaseAllKey = Key.Digit2;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _physicsVelocityLookup = state.GetComponentLookup<Unity.Physics.PhysicsVelocity>(false);

            _godHandQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Skip during rewind playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return;
            }

            if (_godHandQuery.IsEmpty)
            {
                return;
            }

            var godHandEntity = _godHandQuery.GetSingletonEntity();

            if (!state.EntityManager.HasBuffer<ThrowQueue>(godHandEntity))
            {
                return;
            }

            var queue = state.EntityManager.GetBuffer<ThrowQueue>(godHandEntity);
            if (queue.Length == 0)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);

            // Read input
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool releaseOnePressed = keyboard[ReleaseOneKey].wasPressedThisFrame;
            bool releaseAllPressed = keyboard[ReleaseAllKey].wasPressedThisFrame;

            if (releaseOnePressed)
            {
                // Release one throw
                if (queue.Length > 0)
                {
                    var entry = queue[0].Value;
                    ApplyThrow(ref state, entry);
                    queue.RemoveAt(0);
                }
            }
            else if (releaseAllPressed)
            {
                // Release all throws
                var ecb = new EntityCommandBuffer(Allocator.TempJob);
                for (int i = 0; i < queue.Length; i++)
                {
                    var entry = queue[i].Value;
                    ApplyThrow(ref state, entry);
                }
                queue.Clear();
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }

        [BurstDiscard]
        private void ApplyThrow(ref SystemState state, ThrowQueueEntry entry)
        {
            if (entry.Target == Entity.Null || !state.EntityManager.Exists(entry.Target))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Remove MovementSuppressed if present
            if (state.EntityManager.HasComponent<MovementSuppressed>(entry.Target))
            {
                ecb.RemoveComponent<MovementSuppressed>(entry.Target);
            }

            // Set physics velocity
            float3 throwVelocity = entry.Direction * entry.Force;
            if (_physicsVelocityLookup.HasComponent(entry.Target))
            {
                var velocity = _physicsVelocityLookup[entry.Target];
                velocity.Linear = throwVelocity;
                velocity.Angular = float3.zero;
                ecb.SetComponent(entry.Target, velocity);
            }

            // Add BeingThrown component
            ecb.AddComponent(entry.Target, new BeingThrown
            {
                InitialVelocity = throwVelocity,
                TimeSinceThrow = 0f
            });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

