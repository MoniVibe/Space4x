using PureDOTS.Runtime.Orders;
using TimeState = PureDOTS.Runtime.Components.TimeState;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Orders
{
    /// <summary>
    /// Applies empire directives to faction goals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XFactionGoalSystem))]
    public partial struct Space4XEmpireDirectiveGoalSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            foreach (var (faction, goals, directives) in
                     SystemAPI.Query<RefRO<Space4XFaction>, DynamicBuffer<Space4XFactionGoal>, DynamicBuffer<EmpireDirective>>())
            {
                if (faction.ValueRO.Type != FactionType.Empire && faction.ValueRO.Type != FactionType.Player)
                    continue;

                for (int i = 0; i < directives.Length; i++)
                {
                    var directive = directives[i];
                    if (directive.ExpiryTick > 0 && currentTick > directive.ExpiryTick)
                        continue;

                    if (!TryMapDirectiveToGoal(directive.DirectiveType, out var goalType))
                        continue;

                    byte priority = PriorityFromDirective(directive);
                    UpsertGoal(goals, goalType, priority, directive.TargetEntity, currentTick);
                }
            }
        }

        private static bool TryMapDirectiveToGoal(in FixedString32Bytes directiveType, out FactionGoalType goalType)
        {
            if (directiveType.Equals(EmpireDirectiveKeys.SecureResources))
            {
                goalType = FactionGoalType.ExploitResource;
                return true;
            }
            if (directiveType.Equals(EmpireDirectiveKeys.Expand))
            {
                goalType = FactionGoalType.ColonizeSystem;
                return true;
            }
            if (directiveType.Equals(EmpireDirectiveKeys.ResearchFocus))
            {
                goalType = FactionGoalType.ResearchTech;
                return true;
            }
            if (directiveType.Equals(EmpireDirectiveKeys.MilitaryPosture))
            {
                goalType = FactionGoalType.DefendTerritory;
                return true;
            }
            if (directiveType.Equals(EmpireDirectiveKeys.TradeBias))
            {
                goalType = FactionGoalType.EstablishRoute;
                return true;
            }

            goalType = FactionGoalType.None;
            return false;
        }

        private static byte PriorityFromDirective(in EmpireDirective directive)
        {
            float weight = directive.PriorityWeight <= 0f ? 1f : directive.PriorityWeight;
            float score = math.clamp(directive.BasePriority * weight, 0f, 100f);
            float priority = math.clamp(100f - score, 1f, 100f);
            return (byte)priority;
        }

        private static void UpsertGoal(DynamicBuffer<Space4XFactionGoal> goals, FactionGoalType goalType, byte priority, Entity target, uint currentTick)
        {
            for (int i = 0; i < goals.Length; i++)
            {
                if (goals[i].Type != goalType)
                    continue;

                var goal = goals[i];
                goal.Priority = goal.Priority < priority ? goal.Priority : priority;
                if (target != Entity.Null)
                    goal.TargetEntity = target;
                goals[i] = goal;
                return;
            }

            if (goals.Length < goals.Capacity)
            {
                goals.Add(new Space4XFactionGoal
                {
                    Type = goalType,
                    Priority = priority,
                    TargetEntity = target,
                    TargetLocation = float3.zero,
                    Progress = (half)0f,
                    ResourcesAllocated = 0f,
                    CreatedTick = currentTick,
                    DeadlineTick = 0
                });
            }
        }
    }
}
