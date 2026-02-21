using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Hand;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using Space4X.Runtime.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Space4X.Systems.Interaction
{
    /// <summary>
    /// Handles throw queue release: one-by-one or all-together via hotkeys.
    /// </summary>
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct Space4XThrowQueueSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Unity.Physics.PhysicsVelocity> _physicsVelocityLookup;
        private EntityQuery _godHandQuery;
        private uint _lastInputSampleId;
        private NativeParallelHashSet<Entity> _loggedFallbackEntities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HandInputFrame>();
            state.RequireForUpdate<Space4XControlModeRuntimeState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _physicsVelocityLookup = state.GetComponentLookup<Unity.Physics.PhysicsVelocity>(false);

            _godHandQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XGodHandTag>()
                .Build();
            _loggedFallbackEntities = new NativeParallelHashSet<Entity>(128, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_loggedFallbackEntities.IsCreated)
            {
                _loggedFallbackEntities.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var modeState = SystemAPI.GetSingleton<Space4XControlModeRuntimeState>();
            if (modeState.IsDivineHandEnabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Only mutate during record mode (play)
            if (rewindState.Mode != RewindMode.Record)
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

            var inputFrame = SystemAPI.GetSingleton<HandInputFrame>();
            var interactionPolicy = InteractionPolicy.CreateDefault();
            if (SystemAPI.TryGetSingleton(out InteractionPolicy policyValue))
            {
                interactionPolicy = policyValue;
            }
            bool isNewSample = inputFrame.SampleId != _lastInputSampleId;
            bool releaseOnePressed = isNewSample && inputFrame.ReleaseOnePressed;
            bool releaseAllPressed = isNewSample && inputFrame.ReleaseAllPressed;

            if (!releaseOnePressed && !releaseAllPressed)
            {
                return;
            }

            if (releaseOnePressed)
            {
                // Release one throw
                if (queue.Length > 0)
                {
                    var entry = queue[0].Value;
                    ApplyThrow(ref state, entry, interactionPolicy);
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
                    ApplyThrow(ref state, entry, interactionPolicy);
                }
                queue.Clear();
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }

            if (isNewSample)
            {
                _lastInputSampleId = inputFrame.SampleId;
            }
        }

        [BurstDiscard]
        private void ApplyThrow(ref SystemState state, ThrowQueueEntry entry, InteractionPolicy interactionPolicy)
        {
            if (entry.Target == Entity.Null || !state.EntityManager.Exists(entry.Target))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Disable MovementSuppressed if present
            if (state.EntityManager.HasComponent<MovementSuppressed>(entry.Target))
            {
                ecb.SetComponentEnabled<MovementSuppressed>(entry.Target, false);
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

            var prevPosition = float3.zero;
            var prevRotation = quaternion.identity;
            if (_transformLookup.HasComponent(entry.Target))
            {
                var transform = _transformLookup[entry.Target];
                prevPosition = transform.Position;
                prevRotation = transform.Rotation;
            }

            bool hasBeingThrown = state.EntityManager.HasComponent<BeingThrown>(entry.Target);
            if (!hasBeingThrown && interactionPolicy.AllowStructuralFallback == 0)
            {
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(entry.Target, "BeingThrown", skipped: true);
                }
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                return;
            }

            // Enable BeingThrown component
            var thrown = new BeingThrown
            {
                InitialVelocity = throwVelocity,
                TimeSinceThrow = 0f,
                PrevPosition = prevPosition,
                PrevRotation = prevRotation
            };
            if (hasBeingThrown)
            {
                ecb.SetComponent(entry.Target, thrown);
                ecb.SetComponentEnabled<BeingThrown>(entry.Target, true);
            }
            else
            {
                ecb.AddComponent(entry.Target, thrown);
                if (interactionPolicy.LogStructuralFallback != 0)
                {
                    LogFallbackOnce(entry.Target, "BeingThrown", skipped: false);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstDiscard]
        private void LogFallbackOnce(Entity target, string missingComponents, bool skipped)
        {
            if (!_loggedFallbackEntities.IsCreated || !_loggedFallbackEntities.Add(target))
            {
                return;
            }

            var action = skipped ? "skipping throw tag (strict policy)" : "using structural fallback";
            UnityEngine.Debug.LogWarning($"[Space4XThrowQueueSystem] Missing {missingComponents} on entity {target.Index}:{target.Version}; {action}.");
        }
    }
}
