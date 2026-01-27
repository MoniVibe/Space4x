using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Movement;
using PureDOTS.Runtime.Traversal;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Traversal
{
    /// <summary>
    /// Executes traversal motion (Phase 1: jump links only) using deterministic kinematic arcs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Movement.MovementIntegrateSystem))]
    public partial struct TraversalExecutionSystem : ISystem
    {
        private ComponentLookup<MovementState> _movementLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _movementLookup = state.GetComponentLookup<MovementState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var deltaTime = math.max(timeState.DeltaTime, 1e-4f);
            _movementLookup.Update(ref state);

            foreach (var (exec, transform, entity) in SystemAPI.Query<RefRW<TraversalExecutionState>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                if (exec.ValueRO.IsActive == 0 || exec.ValueRO.Type != TraversalType.Jump)
                {
                    continue;
                }

                var duration = math.max(exec.ValueRO.Duration, 1e-4f);
                var elapsed = math.min(exec.ValueRO.Elapsed + deltaTime, duration);
                var t = math.saturate(elapsed / duration);

                var start = exec.ValueRO.StartPosition;
                var end = exec.ValueRO.EndPosition;
                var arcHeight = math.max(0f, exec.ValueRO.ArcHeight);
                var pos = EvaluateJumpArc(start, end, arcHeight, t);

                var previous = transform.ValueRO.Position;
                transform.ValueRW.Position = pos;

                if (_movementLookup.HasComponent(entity))
                {
                    var movement = _movementLookup[entity];
                    movement.Vel = (pos - previous) / deltaTime;
                    movement.Desired = float3.zero;
                    _movementLookup[entity] = movement;
                }

                exec.ValueRW.Elapsed = elapsed;

                if (elapsed >= duration)
                {
                    var finalPos = end;
                    if (exec.ValueRO.LandingSnapDistance > 0f &&
                        math.distance(finalPos, pos) > exec.ValueRO.LandingSnapDistance)
                    {
                        finalPos = pos;
                    }

                    transform.ValueRW.Position = finalPos;
                    exec.ValueRW.IsActive = 0;
                }
            }
        }

        private static float3 EvaluateJumpArc(float3 start, float3 end, float arcHeight, float t)
        {
            var pos = math.lerp(start, end, t);
            var height = 4f * arcHeight * t * (1f - t);
            pos.y += height;
            return pos;
        }
    }
}
