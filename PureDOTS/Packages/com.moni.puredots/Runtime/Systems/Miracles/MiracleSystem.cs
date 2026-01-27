using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Environment;
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
    /// Processes MiracleCastEvent and implements Heal, Smite, and Rain miracles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(MiracleActivationSystem))]
    public partial struct MiracleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<MiracleCastEvent>>().WithEntityAccess())
            {
                for (int i = 0; i < events.Length; i++)
                {
                    var castEvent = events[i];
                    ProcessMiracleCast(ref state, castEvent, ref ecb);
                }
                events.Clear();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void ProcessMiracleCast(ref SystemState state, MiracleCastEvent castEvent, ref EntityCommandBuffer ecb)
        {
            var miracleId = (MiracleId)castEvent.MiracleId;

            switch (miracleId)
            {
                case MiracleId.Heal:
                    ProcessHeal(ref state, castEvent, ref ecb);
                    break;

                case MiracleId.Fire:
                    ProcessSmite(ref state, castEvent, ref ecb);
                    break;

                case MiracleId.Rain:
                    ProcessRain(ref state, castEvent, ref ecb);
                    break;

                default:
                    // Other miracles (e.g., Siphon) are handled by other systems
                    break;
            }
        }

        [BurstCompile]
        private void ProcessHeal(ref SystemState state, MiracleCastEvent castEvent, ref EntityCommandBuffer ecb)
        {
            if (castEvent.TargetEntity == Entity.Null || !state.EntityManager.Exists(castEvent.TargetEntity))
            {
                return;
            }

            if (state.EntityManager.HasComponent<Health>(castEvent.TargetEntity))
            {
                var health = state.EntityManager.GetComponentData<Health>(castEvent.TargetEntity);
                health.Current = math.min(health.Current + 50f, health.Max); // Heal 50 HP, cap at max
                ecb.SetComponent(castEvent.TargetEntity, health);
            }
        }

        [BurstCompile]
        private void ProcessSmite(ref SystemState state, MiracleCastEvent castEvent, ref EntityCommandBuffer ecb)
        {
            float damageRadius = 10f;
            float damageAmount = 25f;

            // Find all entities within radius
            foreach (var (health, transform, entity) in SystemAPI.Query<RefRW<Health>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                float distance = math.distance(transform.ValueRO.Position, castEvent.TargetPosition);
                if (distance <= damageRadius)
                {
                    health.ValueRW.Current = math.max(0f, health.ValueRW.Current - damageAmount);
                }
            }
        }

        [BurstCompile]
        private void ProcessRain(ref SystemState state, MiracleCastEvent castEvent, ref EntityCommandBuffer ecb)
        {
            float rainRadius = 15f;
            float moistureIncrease = 0.3f;

            if (rainRadius <= 0f || moistureIncrease <= 0f)
            {
                return;
            }

            // Find environment cells within radius and increase moisture
            // For now, we'll use a simple spatial query or update EnvCellDynamic if available
            // This is a simplified version - full implementation would query the environment grid
            
            // TODO: Query EnvCellDynamic entities within radius and increase moisture
            // For now, this is a placeholder that shows the structure
        }
    }
}
