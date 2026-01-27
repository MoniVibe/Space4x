using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Formation steering system - keeps individuals around group path with local obstacle avoidance.
    /// Individuals follow group's NavPath with formation/steering logic (no per-unit A*).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HotPathSystemGroup))]
    // Removed invalid UpdateAfter: GroupNavPlannerSystem runs in ColdPath; data is consumed next tick.
    public partial struct FormationSteeringSystem : ISystem
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

            // Process band members following group navigation
            foreach (var (bandMembers, groupNav, navPath, pathSegments, bandEntity) in
                SystemAPI.Query<DynamicBuffer<BandMember>, RefRO<GroupNavComponent>, RefRO<NavPath>, DynamicBuffer<NavPathSegment>>()
                .WithEntityAccess())
            {
                if (groupNav.ValueRO.IsActive == 0 || navPath.ValueRO.IsValid == 0)
                {
                    continue;
                }

                float3 groupTarget = groupNav.ValueRO.CurrentTargetPosition;

                // Update individual members to follow group target
                for (int i = 0; i < bandMembers.Length; i++)
                {
                    var memberEntity = bandMembers[i].Villager;
                    if (!state.EntityManager.Exists(memberEntity))
                    {
                        continue;
                    }

                    // Check if member has LocalTransform
                    if (!SystemAPI.HasComponent<LocalTransform>(memberEntity))
                    {
                        continue;
                    }

                    var memberTransform = SystemAPI.GetComponent<LocalTransform>(memberEntity);

                    // Calculate desired direction toward group target
                    float3 toTarget = groupTarget - memberTransform.Position;
                    float distanceToTarget = math.length(toTarget);

                    // If member is far from group target, steer toward it
                    // Otherwise, use local steering/formation logic
                    if (distanceToTarget > 10f) // Threshold for following group target
                    {
                        // TODO: Update member's movement target to group target
                        // This would integrate with VillagerMovementSystem
                        // For now, this is a placeholder
                    }
                }
            }

            // TODO: Process army members similarly
            // TODO: Process fleet members similarly
            // TODO: Add formation spacing/cohesion logic
            // TODO: Add local obstacle avoidance for individuals
        }
    }
}

