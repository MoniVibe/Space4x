using PureDOTS.Input;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Applies sustained miracle effects each tick while channeling.
    /// Updates target point to follow cursor and delegates to game-specific effect systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(MiracleActivationSystem))]
    public partial struct MiracleSustainedTickSystem : ISystem
    {
        private ComponentLookup<DivineHandInput> _handInputLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _handInputLookup = state.GetComponentLookup<DivineHandInput>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check time state
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState) || timeState.IsPaused)
            {
                return;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _handInputLookup.Update(ref state);

            foreach (var (sustainedRef, transformRef, entity) in SystemAPI
                         .Query<RefRW<MiracleSustainedEffect>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                ref var sustained = ref sustainedRef.ValueRW;
                if (sustained.IsChanneling == 0)
                {
                    continue;
                }

                // Validate owner exists - if not, cleanup orphaned effect
                if (!SystemAPI.Exists(sustained.Owner))
                {
                    // Owner destroyed - destroy orphaned effect entity
                    // Channel state on owner is already gone (component destroyed with owner)
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Update target point if owner has cursor position
                float3 targetPoint = sustained.TargetPoint;
                if (_handInputLookup.HasComponent(sustained.Owner))
                {
                    var handInput = _handInputLookup[sustained.Owner];
                    targetPoint = handInput.CursorWorldPosition;
                    sustained.TargetPoint = targetPoint;
                    
                    // Update transform position to follow cursor
                    var transform = transformRef.ValueRO;
                    transform.Position = targetPoint;
                    transformRef.ValueRW = transform;
                }

                // Apply effect based on miracle ID
                ApplySustainedEffect(ref state, sustained, targetPoint, deltaTime);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void ApplySustainedEffect(
            ref SystemState state,
            in MiracleSustainedEffect sustained,
            float3 targetPoint,
            float deltaTime)
        {
            // Delegate to game-specific effect systems based on miracle ID
            // For now, this is a placeholder that can be extended by game-specific systems
            switch (sustained.Id)
            {
                case MiracleId.Rain:
                    // Rain sustained effect will be handled by game-specific rain system
                    // This system provides the framework for continuous application
                    break;
                case MiracleId.Heal:
                    // Heal sustained effect will be handled by game-specific heal system
                    ApplyHealSustained(ref state, sustained, targetPoint, deltaTime);
                    break;
                case MiracleId.Fire:
                    // Fire sustained effect will be handled by game-specific fire system
                    break;
                case MiracleId.TemporalVeil:
                    // Temporal veil sustained effect will be handled by game-specific time system
                    break;
                default:
                    // Unknown miracle ID, skip
                    break;
            }
        }

        [BurstCompile]
        private void ApplyHealSustained(
            ref SystemState state,
            in MiracleSustainedEffect sustained,
            float3 targetPoint,
            float deltaTime)
        {
            // Simple heal implementation: heal entities within radius over time
            // This is a basic example - game-specific systems should provide full implementations
            float healPerSecond = sustained.Intensity * 10f; // Base heal rate scaled by intensity
            float healAmount = healPerSecond * deltaTime;
            float radiusSq = sustained.Radius * sustained.Radius;

            // Query entities with health within radius
            foreach (var (healthRef, transform, targetEntity) in SystemAPI
                         .Query<RefRW<Health>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                float distanceSq = math.lengthsq(transform.ValueRO.Position - targetPoint);
                if (distanceSq <= radiusSq)
                {
                    ref var health = ref healthRef.ValueRW;
                    health.Current = math.min(health.Current + healAmount, health.Max);
                }
            }
        }
    }
}

