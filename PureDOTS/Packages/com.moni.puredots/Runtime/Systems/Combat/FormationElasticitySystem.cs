using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Scales FormationAnchor.LocalOffset toward LooseSpacingMax when risk > threshold.
    /// Breaks formation (sets Leader = Entity.Null) when very high risk.
    /// Enforces GroupBreakCooldown hysteresis via LastAvoidanceDecision.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AvoidanceSenseSystem))]
    public partial struct FormationElasticitySystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = timeState.DeltaTime;

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            transformLookup.Update(ref state);

            var job = new FormationElasticityJob
            {
                CurrentTick = currentTick,
                DeltaTime = deltaTime,
                TransformLookup = transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct FormationElasticityJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            void Execute(
                Entity entity,
                ref FormationAnchor anchor,
                ref LastAvoidanceDecision decision,
                in AvoidanceProfile profile,
                in CommandPolicy policy,
                in HazardAvoidanceState avoidanceState)
            {
                float risk = avoidanceState.AvoidanceUrgency;
                var currentMode = (AvoidanceMode)decision.Mode;

                // Determine new mode based on risk
                var newMode = AvoidanceMode.Hold;
                if (risk > profile.BreakFormationThresh)
                {
                    // Check cooldown before breaking
                    float timeSinceDecision = (CurrentTick - decision.Tick) * DeltaTime;
                    if (timeSinceDecision >= policy.GroupBreakCooldown)
                    {
                        newMode = AvoidanceMode.Break;
                    }
                    else
                    {
                        newMode = AvoidanceMode.Loose;
                    }
                }
                else if (risk > 0f)
                {
                    newMode = AvoidanceMode.Loose;
                }

                // Update mode if changed
                if (newMode != currentMode)
                {
                    decision.Mode = (byte)newMode;
                    decision.Tick = CurrentTick;
                }

                // Apply spacing elasticity
                float currentSpacing = math.length(anchor.LocalOffset);
                float targetSpacing = newMode == AvoidanceMode.Break
                    ? profile.LooseSpacingMax
                    : newMode == AvoidanceMode.Loose
                        ? profile.LooseSpacingMax
                        : profile.LooseSpacingMin;

                // Interpolate spacing toward target
                float newSpacing = math.lerp(currentSpacing, targetSpacing, policy.SpacingElasticity * DeltaTime);

                // Update local offset
                if (currentSpacing > 1e-6f)
                {
                    anchor.LocalOffset = math.normalize(anchor.LocalOffset) * newSpacing;
                }
                else
                {
                    // Default offset if zero
                    anchor.LocalOffset = new float3(1f, 0f, 0f) * newSpacing;
                }

                // Break formation if mode is Break
                if (newMode == AvoidanceMode.Break)
                {
                    anchor.Leader = Entity.Null;
                }
            }
        }
    }
}

