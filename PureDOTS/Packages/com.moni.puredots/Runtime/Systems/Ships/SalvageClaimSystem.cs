using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Processes claim intents and validates salvage/claim operations.
    /// Sets Salvageable.Grade based on hull/module condition.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(DerelictClassifierSystem))]
    public partial struct SalvageClaimSystem : ISystem
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

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            transformLookup.Update(ref state);

            var job = new SalvageClaimJob
            {
                CurrentTick = currentTick,
                TransformLookup = transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct SalvageClaimJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            void Execute(
                Entity entity,
                ref Salvageable salvageable,
                in DerelictState derelict,
                in HullState hull,
                in DynamicBuffer<ModuleRuntimeStateElement> modules,
                in ClaimIntent claimIntent)
            {
                // Only process derelicts
                if (derelict.Stage < 2)
                {
                    salvageable.Grade = 0; // Not salvageable
                    return;
                }

                // Calculate salvage grade based on condition
                byte grade = CalculateSalvageGrade(hull, modules);

                // Check claim intent
                if (claimIntent.Claimer != Entity.Null)
                {
                    // Validate proximity (simplified - would use spatial queries)
                    if (TransformLookup.HasComponent(claimIntent.Claimer))
                    {
                        var claimerPos = TransformLookup[claimIntent.Claimer].Position;
                        var derelictPos = TransformLookup.HasComponent(entity)
                            ? TransformLookup[entity].Position
                            : float3.zero;

                        float distance = math.length(claimerPos - derelictPos);
                        float claimRange = 100f; // meters

                        if (distance <= claimRange)
                        {
                            // Check for contest (simplified - would check other claim intents)
                            // For now, allow claim if within range
                            if (grade >= 3)
                            {
                                salvageable.Grade = 3; // Claimable
                            }
                            else
                            {
                                salvageable.Grade = grade;
                            }
                        }
                        else
                        {
                            salvageable.Grade = grade; // Too far, not claimable yet
                        }
                    }
                    else
                    {
                        salvageable.Grade = grade;
                    }
                }
                else
                {
                    salvageable.Grade = grade;
                }
            }

            private static byte CalculateSalvageGrade(HullState hull, in DynamicBuffer<ModuleRuntimeStateElement> modules)
            {
                // Grade 0 = none, 1 = scrap, 2 = refit, 3 = claimable

                float hullRatio = hull.MaxHP > 0f ? hull.HP / hull.MaxHP : 0f;
                int destroyedModules = 0;
                int totalModules = modules.Length;

                for (int i = 0; i < modules.Length; i++)
                {
                    if (modules[i].Destroyed != 0)
                    {
                        destroyedModules++;
                    }
                }

                float moduleRatio = totalModules > 0 ? 1f - (destroyedModules / (float)totalModules) : 1f;

                // Calculate overall condition
                float condition = (hullRatio + moduleRatio) * 0.5f;

                if (condition < 0.2f)
                {
                    return 1; // Scrap
                }
                else if (condition < 0.6f)
                {
                    return 2; // Refit
                }
                else
                {
                    return 3; // Claimable
                }
            }
        }
    }
}

