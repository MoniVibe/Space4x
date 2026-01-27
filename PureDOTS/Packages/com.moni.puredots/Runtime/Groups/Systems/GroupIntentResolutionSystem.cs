using PureDOTS.Runtime.Focus;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Groups
{
    /// <summary>
    /// Resolves individual combat intent for group members.
    /// Blends group orders with individual traits (alignment, personality, focus, morale).
    /// Includes companion-based modifiers for bonds, rivals, and nemeses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GroupIntentResolutionSystem : ISystem
    {
        ComponentLookup<CompanionBond> _bondLookup;
        BufferLookup<CompanionLink> _companionLinkLookup;
        BufferLookup<GroupMember> _groupMemberBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _bondLookup = state.GetComponentLookup<CompanionBond>(true);
            _companionLinkLookup = state.GetBufferLookup<CompanionLink>(true);
            _groupMemberBufferLookup = state.GetBufferLookup<GroupMember>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bondLookup.Update(ref state);
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            _companionLinkLookup.Update(ref state);
            _groupMemberBufferLookup.Update(ref state);

            var job = new ResolveIntentJob
            {
                CurrentTick = timeState.Tick,
                BondLookup = _bondLookup,
                CompanionLinkLookup = _companionLinkLookup,
                GroupMemberBufferLookup = _groupMemberBufferLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ResolveIntentJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<CompanionBond> BondLookup;
            [ReadOnly] public BufferLookup<CompanionLink> CompanionLinkLookup;
            [ReadOnly] public BufferLookup<GroupMember> GroupMemberBufferLookup;

            void Execute(
                ref IndividualCombatIntent intent,
                in GroupBehaviorParams behaviorParams,
                in GroupStanceState groupStance,
                in MoraleState morale,
                in PersonalityAxes personality,
                in AlignmentTriplet alignment,
                in FocusState focus,
                Entity entity)
            {
                // Skip if mental break is active (handled by MentalBreakSystem)
                // This system only resolves normal intent

                // Read group stance and discipline
                float groupDiscipline = groupStance.Discipline;

                // Score probabilities
                float groupObeyScore = behaviorParams.Obedience * groupDiscipline * (1f - behaviorParams.Independence);
                float breakScore = behaviorParams.Independence * math.max(0f, personality.Boldness) * math.max(0f, personality.RiskTolerance);
                float fleeScore = (1f - morale.Current) * math.max(0f, -personality.Boldness) * (1f - math.max(0f, personality.Boldness));
                
                // Mutiny score: low morale + vengefulness against leader
                // For now, use general vengefulness (would need leader-specific tracking)
                float mutinyScore = (1f - morale.Current) * math.max(0f, personality.Vengefulness) * 0.5f;

                // Companion-based modifiers
                float companionObeyScore = 0f;
                float companionBreakScore = 0f;
                float companionFleeScore = 0f;
                float companionBerserkScore = 0f;
                float companionTargetOverrideScore = 0f;
                Entity nemesisTarget = Entity.Null;

                if (CompanionLinkLookup.HasBuffer(entity))
                {
                    var links = CompanionLinkLookup[entity];
                    for (int i = 0; i < links.Length; i++)
                    {
                        Entity bondEntity = links[i].Bond;
                        if (!BondLookup.HasComponent(bondEntity))
                            continue;

                        var bond = BondLookup[bondEntity];
                        Entity other = (bond.A == entity) ? bond.B : bond.A;

                        if (other == Entity.Null)
                            continue;

                        // Check companion status
                        bool companionNearby = IsInSameGroup(entity, other);
                        bool companionThreatened = IsThreatened(other);
                        bool companionDead = !BondLookup.HasComponent(bondEntity);

                        if (bond.State == CompanionState.Active && bond.Kind != CompanionKind.Rival && bond.Kind != CompanionKind.Nemesis)
                        {
                            // Positive bond effects
                            if (companionNearby)
                            {
                                companionObeyScore += bond.Intensity * behaviorParams.CohesionPreference * 0.3f;
                            }

                            if (companionThreatened)
                            {
                                companionBreakScore += bond.Intensity * math.max(0f, personality.Boldness) * 0.4f;
                            }

                            if (companionDead)
                            {
                                companionFleeScore += bond.Intensity * (1f - math.max(0f, personality.Boldness)) * 0.5f;
                                companionBerserkScore += bond.Intensity * math.max(0f, personality.Vengefulness) * 0.6f;
                            }
                        }
                        else if (bond.Kind == CompanionKind.Nemesis || bond.Kind == CompanionKind.Rival)
                        {
                            // Nemesis/rival effects
                            if (companionNearby)
                            {
                                companionTargetOverrideScore += bond.Obsession * 0.8f;
                                companionBreakScore += bond.Rivalry * math.max(0f, personality.Boldness) * 0.5f;
                                if (companionTargetOverrideScore > 0.5f)
                                {
                                    nemesisTarget = other;
                                }
                            }
                        }
                    }
                }

                // Add companion modifiers to base scores
                groupObeyScore += companionObeyScore;
                breakScore += companionBreakScore;
                fleeScore += companionFleeScore;
                mutinyScore += companionBerserkScore;

                // Normalize scores to probabilities
                float totalScore = groupObeyScore + breakScore + fleeScore + mutinyScore;
                if (totalScore < 0.001f)
                {
                    // Default to following orders if all scores are zero
                    intent.Intent = IndividualTacticalIntent.FollowGroupOrder;
                    intent.TargetOverride = Entity.Null;
                    return;
                }

                float obeyProb = groupObeyScore / totalScore;
                float breakProb = breakScore / totalScore;
                float fleeProb = fleeScore / totalScore;
                float mutinyProb = mutinyScore / totalScore;

                // Deterministic random selection based on tick and entity hash
                uint hash = (uint)(CurrentTick + entity.GetHashCode());
                float random = (hash % 10000) / 10000f;

                // Select intent based on weighted probabilities
                if (random < obeyProb)
                {
                    intent.Intent = IndividualTacticalIntent.FollowGroupOrder;
                    intent.TargetOverride = Entity.Null;
                }
                else if (random < obeyProb + breakProb)
                {
                    // Break formation and pursue own target
                    intent.Intent = IndividualTacticalIntent.AggressivePursuit;
                    // Set nemesis target if present
                    if (nemesisTarget != Entity.Null)
                    {
                        intent.TargetOverride = nemesisTarget;
                    }
                }
                else if (random < obeyProb + breakProb + fleeProb)
                {
                    intent.Intent = IndividualTacticalIntent.Flee;
                    intent.TargetOverride = Entity.Null;
                }
                else
                {
                    intent.Intent = IndividualTacticalIntent.Mutiny;
                    intent.TargetOverride = Entity.Null;
                }

                // Override target if nemesis present (high priority)
                if (companionTargetOverrideScore > 0.5f && nemesisTarget != Entity.Null)
                {
                    intent.TargetOverride = nemesisTarget;
                }

                // Cautious hold: if low focus and craven, may hold instead of following aggressive orders
                if (focus.Current < focus.SoftThreshold && personality.Boldness < -0.3f && groupStance.Stance == GroupStance.Attack)
                {
                    if (random < 0.3f) // 30% chance to hold instead
                    {
                        intent.Intent = IndividualTacticalIntent.CautiousHold;
                    }
                }
            }

            bool IsInSameGroup(Entity a, Entity b)
            {
                // Check if both entities are in the same group by scanning all groups
                // This is a simplified check - in practice, you'd want to cache group membership
                // For now, return false (companion nearby check can be enhanced later)
                return false;
            }

            bool IsThreatened(Entity entity)
            {
                // Check if entity is threatened (low health, in combat, etc.)
                // Simplified for now - return false
                return false;
            }
        }
    }
}

