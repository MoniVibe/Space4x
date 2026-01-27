using PureDOTS.Runtime;
using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// System that determines individual combat intent based on personality and group stance.
    /// Bold/chaotic individuals may flank; craven/peaceful may flee.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GroupFormationSystem))]
    public partial struct GroupCombatIntentSystem : ISystem
    {
        private ComponentLookup<SquadTacticOrder> _tacticLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<SquadTacticOrder>();
            _tacticLookup = state.GetComponentLookup<SquadTacticOrder>(true);
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

            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenarioState))
            {
                return;
            }

            // Only process for Godgame (combat intent is primarily for ground units)
            if (!scenarioState.EnableGodgame)
            {
                return;
            }

            _tacticLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Query group members with CombatIntent
            foreach (var (groupMembership, health, combatIntent, entity) in SystemAPI.Query<
                RefRO<GroupMembership>,
                RefRO<Health>,
                RefRW<CombatIntent>>()
                .WithEntityAccess())
            {
                if (!state.EntityManager.Exists(groupMembership.ValueRO.Group))
                {
                    continue;
                }

                var groupStance = state.EntityManager.GetComponentData<GroupStanceState>(groupMembership.ValueRO.Group);

                // Only process when group is in Attack stance
                if (groupStance.Stance != GroupStance.Attack)
                {
                    // Reset to FollowGroup when not attacking
                    combatIntent.ValueRW.State = (byte)CombatIntentState.FollowGroup;
                    combatIntent.ValueRW.Target = Entity.Null;
                    continue;
                }

                if (_tacticLookup.HasComponent(groupMembership.ValueRO.Group))
                {
                    ApplyTacticIntent(_tacticLookup[groupMembership.ValueRO.Group], ref combatIntent.ValueRW);
                    continue;
                }

                var healthPercent = health.ValueRO.Current / math.max(1f, health.ValueRO.Max);
                var hash = math.hash(new uint3(
                    (uint)entity.Index,
                    (uint)groupMembership.ValueRO.Group.Index,
                    (uint)(timeState.Tick >> 2)));
                var roll = hash % 100u;

                if (healthPercent < 0.25f && roll < 35u)
                {
                    combatIntent.ValueRW.State = (byte)CombatIntentState.Flee;
                    combatIntent.ValueRW.Target = Entity.Null;
                }
                else if (healthPercent > 0.65f && roll % 3u == 0u)
                {
                    combatIntent.ValueRW.State = (byte)CombatIntentState.Flank;
                    combatIntent.ValueRW.Target = Entity.Null;
                }
                else
                {
                    combatIntent.ValueRW.State = (byte)CombatIntentState.FollowGroup;
                    combatIntent.ValueRW.Target = Entity.Null;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void ApplyTacticIntent(in SquadTacticOrder tactic, ref CombatIntent intent)
        {
            switch (tactic.Kind)
            {
                case SquadTacticKind.FlankLeft:
                case SquadTacticKind.FlankRight:
                    intent.State = (byte)CombatIntentState.Flank;
                    intent.Target = tactic.Target;
                    break;
                case SquadTacticKind.Retreat:
                    intent.State = (byte)CombatIntentState.Flee;
                    intent.Target = tactic.Target;
                    break;
                case SquadTacticKind.Collapse:
                    intent.State = (byte)CombatIntentState.HoldPosition;
                    intent.Target = tactic.Target;
                    break;
                default:
                    intent.State = (byte)CombatIntentState.FollowGroup;
                    intent.Target = tactic.Target;
                    break;
            }
        }
    }
}
