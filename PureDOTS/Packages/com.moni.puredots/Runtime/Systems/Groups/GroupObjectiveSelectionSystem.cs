using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Perception;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Selects or updates GroupObjective based on metrics and environment.
    /// Phase 1: Simple decision rules (threat-based, resource-based).
    /// Phase 2: Advanced decision trees, utility functions, etc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    [UpdateAfter(typeof(GroupMetricsSystem))]
    public partial struct GroupObjectiveSelectionSystem : ISystem
    {
        private ComponentLookup<PerceptionState> _perceptionLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _perceptionLookup = state.GetComponentLookup<PerceptionState>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
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

            _perceptionLookup.Update(ref state);
            _transformLookup.Update(ref state);

            // Update objectives for all groups
            foreach (var (metrics, objective, groupIdentity, transform, entity) in
                SystemAPI.Query<RefRO<GroupMetrics>, RefRW<GroupObjective>, RefRO<GroupIdentity>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Skip if group is not active
                if (groupIdentity.ValueRO.Status != GroupStatus.Active)
                {
                    continue;
                }

                // Check if objective needs update (Phase 1: simple time-based or condition-based)
                var shouldUpdate = false;

                // Update if objective expired
                if (objective.ValueRO.IsActive != 0 && objective.ValueRO.ExpirationTick > 0)
                {
                    if (timeState.Tick >= objective.ValueRO.ExpirationTick)
                    {
                        shouldUpdate = true;
                    }
                }

                // Update if no objective set
                if (objective.ValueRO.IsActive == 0 || objective.ValueRO.ObjectiveType == GroupObjectiveType.None)
                {
                    shouldUpdate = true;
                }

                // Update if threat detected and current objective doesn't handle it
                if (metrics.ValueRO.ThreatLevel > 100 && objective.ValueRO.ObjectiveType != GroupObjectiveType.Defend)
                {
                    shouldUpdate = true;
                }

                if (!shouldUpdate)
                {
                    continue;
                }

                // Select new objective based on metrics and environment
                var newObjective = SelectObjective(
                    metrics.ValueRO,
                    entity,
                    transform.ValueRO.Position,
                    timeState.Tick);

                // Update objective
                objective.ValueRW = newObjective;
            }
        }

        /// <summary>
        /// Selects appropriate objective based on group metrics.
        /// Phase 1: Simple priority-based selection.
        /// Phase 2: Utility-based selection with multiple factors.
        /// </summary>
        [BurstCompile]
        private GroupObjective SelectObjective(
            GroupMetrics metrics,
            Entity groupEntity,
            float3 groupPosition,
            uint currentTick)
        {
            var objective = new GroupObjective
            {
                SetTick = currentTick,
                IsActive = 1,
                Priority = 50, // Default priority
                TargetPosition = groupPosition // Default to current position
            };

            // Priority 1: Defend if under threat
            if (metrics.ThreatLevel > 100)
            {
                objective.ObjectiveType = GroupObjectiveType.Defend;
                objective.Priority = 200; // High priority

                // Try to get threat position from group's perception (if group has perception)
                if (_perceptionLookup.HasComponent(groupEntity))
                {
                    var perception = _perceptionLookup[groupEntity];
                    if (perception.HighestThreatEntity != Entity.Null)
                    {
                        objective.TargetEntity = perception.HighestThreatEntity;
                        if (_transformLookup.HasComponent(perception.HighestThreatEntity))
                        {
                            objective.TargetPosition = _transformLookup[perception.HighestThreatEntity].Position;
                        }
                    }
                }

                return objective;
            }

            // Priority 2: Gather resources if low
            if (metrics.ResourceCount0 < 10f) // Food/Supplies low
            {
                objective.ObjectiveType = GroupObjectiveType.Forage;
                objective.Priority = 150;
                return objective;
            }

            // Priority 3: Default to patrol/idle
            objective.ObjectiveType = GroupObjectiveType.Patrol;
            objective.Priority = 50;

            return objective;
        }
    }
}

