using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Formation;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Calculates formation integrity and applies combat bonuses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FormationCombatSystem : ISystem
    {
        private ComponentLookup<FormationMember> _formationMemberLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _formationMemberLookup = state.GetComponentLookup<FormationMember>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            _formationMemberLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Calculate integrity for formations
            foreach (var (integrity, formationState, formationSlots, entity) in SystemAPI.Query<
                RefRW<FormationIntegrity>,
                RefRO<FormationState>,
                DynamicBuffer<FormationSlot>>()
                .WithEntityAccess())
            {
                if (formationState.ValueRO.Type == FormationType.None)
                    continue;

                // Count members in position
                byte membersInPosition = 0;
                byte totalMembers = 0;

                for (int i = 0; i < formationSlots.Length; i++)
                {
                    var slot = formationSlots[i];
                    if (slot.AssignedEntity != Entity.Null)
                    {
                        totalMembers++;
                        if (_formationMemberLookup.HasComponent(slot.AssignedEntity))
                        {
                            var member = _formationMemberLookup[slot.AssignedEntity];
                            if (member.IsInPosition)
                            {
                                membersInPosition++;
                            }
                        }
                    }
                }

                // Calculate integrity
                float integrityPercent = FormationCombatService.GetFormationIntegrity(
                    membersInPosition,
                    totalMembers);

                integrity.ValueRW.IntegrityPercent = integrityPercent;
                integrity.ValueRW.MembersInPosition = membersInPosition;
                integrity.ValueRW.TotalMembers = totalMembers;
                integrity.ValueRW.LastCalculatedTick = currentTick;

                // Check if formation bonus needs to be created or refreshed
                bool needsRefresh = false;
                FormationType currentType = formationState.ValueRO.Type;

                if (!SystemAPI.HasComponent<FormationBonus>(entity))
                {
                    needsRefresh = true;
                }
                else if (SystemAPI.HasComponent<FormationCombatConfig>(entity))
                {
                    var existingConfig = SystemAPI.GetComponent<FormationCombatConfig>(entity);
                    if (existingConfig.AppliedType != currentType)
                    {
                        needsRefresh = true;
                    }
                }

                if (needsRefresh)
                {
                    var config = FormationCombatService.GetBaseConfig(currentType);
                    
                    if (SystemAPI.HasComponent<FormationBonus>(entity))
                    {
                        ecb.SetComponent(entity, new FormationBonus
                        {
                            DefenseMultiplier = config.BaseDefenseMultiplier,
                            AttackMultiplier = config.BaseAttackMultiplier,
                            MoraleMultiplier = config.BaseMoraleMultiplier
                        });
                    }
                    else
                    {
                        ecb.AddComponent(entity, new FormationBonus
                        {
                            DefenseMultiplier = config.BaseDefenseMultiplier,
                            AttackMultiplier = config.BaseAttackMultiplier,
                            MoraleMultiplier = config.BaseMoraleMultiplier
                        });
                    }

                    if (SystemAPI.HasComponent<FormationCombatConfig>(entity))
                    {
                        ecb.SetComponent(entity, new FormationCombatConfig
                        {
                            BaseDefenseMultiplier = config.BaseDefenseMultiplier,
                            BaseAttackMultiplier = config.BaseAttackMultiplier,
                            BaseMoraleMultiplier = config.BaseMoraleMultiplier,
                            IntegrityThreshold = config.IntegrityThreshold,
                            AppliedType = currentType
                        });
                    }
                    else
                    {
                        ecb.AddComponent(entity, new FormationCombatConfig
                        {
                            BaseDefenseMultiplier = config.BaseDefenseMultiplier,
                            BaseAttackMultiplier = config.BaseAttackMultiplier,
                            BaseMoraleMultiplier = config.BaseMoraleMultiplier,
                            IntegrityThreshold = config.IntegrityThreshold,
                            AppliedType = currentType
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

