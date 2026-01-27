using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Formation;
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
    /// System that computes desired formation positions for group members.
    /// Updates FormationMember target positions based on group FormationState.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GroupFormationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();
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

            // Only process for Godgame (formations are primarily for ground units)
            if (!scenarioState.EnableGodgame)
            {
                return;
            }

            // Query groups with FormationState and GroupMember buffer
            var formationMemberLookup = state.GetComponentLookup<FormationMember>(false);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (formationState, groupMembers, transform, groupEntity) in SystemAPI.Query<
                RefRO<FormationState>,
                DynamicBuffer<GroupMember>,
                RefRO<LocalTransform>>()
                .WithAll<GroupTag>()
                .WithEntityAccess())
            {
                if (formationState.ValueRO.Type == PureDOTS.Runtime.Formation.FormationType.None)
                {
                    continue;
                }

                // Compute desired positions for each member
                int memberCount = groupMembers.Length;
                if (memberCount == 0)
                {
                    continue;
                }

                var anchorPos = formationState.ValueRO.AnchorPosition;
                var anchorRot = formationState.ValueRO.AnchorRotation;
                var spacing = formationState.ValueRO.Spacing;

                // Assign slots to members
                for (int i = 0; i < memberCount && i < formationState.ValueRO.MaxSlots; i++)
                {
                    var memberEntity = groupMembers[i].MemberEntity;
                    if (!state.EntityManager.Exists(memberEntity))
                    {
                        continue;
                    }

                    // Calculate slot offset using FormationLayout helper
                    float3 localOffset = FormationLayout.GetSlotOffset(
                        (PureDOTS.Runtime.Formation.FormationType)formationState.ValueRO.Type,
                        i,
                        memberCount,
                        spacing
                    );

                    // Transform to world space
                    float3 worldOffset = math.mul(anchorRot, localOffset);
                    float3 targetPosition = anchorPos + worldOffset;

                    // Update FormationMember component if it exists
                    if (formationMemberLookup.HasComponent(memberEntity))
                    {
                        var formationMember = formationMemberLookup[memberEntity];
                        formationMember.TargetPosition = targetPosition;
                        formationMember.FormationEntity = groupEntity;
                        formationMember.SlotIndex = (byte)i;
                        formationMemberLookup[memberEntity] = formationMember;
                    }
                    else
                    {
                        // Create FormationMember component
                        ecb.AddComponent(memberEntity, new FormationMember
                        {
                            FormationEntity = groupEntity,
                            SlotIndex = (byte)i,
                            TargetPosition = targetPosition,
                            ArrivalThreshold = 1f,
                            IsInPosition = false,
                            AssignedTick = timeState.Tick
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
