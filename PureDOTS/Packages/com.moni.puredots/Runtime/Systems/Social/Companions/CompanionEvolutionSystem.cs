using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Systems.Social.Companions
{
    /// <summary>
    /// System that evolves companion bonds over time: updates Intensity, Trust, Rivalry, Obsession,
    /// and handles state transitions (Forming → Active, Active → Strained, etc.).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    public partial struct CompanionEvolutionSystem : ISystem
    {
        BufferLookup<EntityRelation> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<CompanionConfig>();

            _relationLookup = state.GetBufferLookup<EntityRelation>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            if (!SystemAPI.TryGetSingleton<CompanionConfig>(out var config))
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Check if it's time to run evolution update
            if (currentTick % config.EvolutionCheckInterval != 0)
                return;

            _relationLookup.Update(ref state);

            var job = new EvolutionUpdateJob
            {
                CurrentTick = currentTick,
                Config = config,
                RelationLookup = _relationLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct EvolutionUpdateJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public CompanionConfig Config;
            [ReadOnly] public BufferLookup<EntityRelation> RelationLookup;

            void Execute(ref CompanionBond bond)
            {
                // Skip if bond is ended or broken
                if (bond.State == CompanionState.Broken || bond.State == CompanionState.EndedByDeath)
                    return;

                uint ticksSinceUpdate = CurrentTick - bond.LastUpdateTick;
                if (ticksSinceUpdate == 0)
                    return;

                // Update bond fields based on interactions
                UpdateFromRelations(ref bond);

                // Apply decay if no recent interaction
                if (ticksSinceUpdate > 1000) // ~16 seconds at 60tps
                {
                    bond.Intensity = math.max(0f, bond.Intensity - Config.DecayRate * ticksSinceUpdate);
                }

                // State transitions
                HandleStateTransitions(ref bond);

                // Kind changes
                HandleKindChanges(ref bond);

                bond.LastUpdateTick = CurrentTick;
            }

            void UpdateFromRelations(ref CompanionBond bond)
            {
                // Check EntityRelation for recent interactions
                if (RelationLookup.HasBuffer(bond.A))
                {
                    var relations = RelationLookup[bond.A];
                    for (int i = 0; i < relations.Length; i++)
                    {
                        if (relations[i].OtherEntity == bond.B)
                        {
                            var relation = relations[i];
                            
                            // Update trust based on relation trust
                            float trustFromRelation = relation.Trust / 100f;
                            bond.TrustAB = math.lerp(bond.TrustAB, trustFromRelation, 0.1f);

                            // Update intensity based on relation intensity (positive)
                            if (relation.Intensity > 0)
                            {
                                float intensityFromRelation = relation.Intensity / 100f;
                                bond.Intensity = math.min(1f, bond.Intensity + Config.IntensityGrowthRate * intensityFromRelation);
                            }
                            // Negative intensity increases rivalry
                            else if (relation.Intensity < 0)
                            {
                                float rivalryFromRelation = math.abs(relation.Intensity) / 100f;
                                bond.Rivalry = math.min(1f, bond.Rivalry + Config.RivalryGrowthRate * rivalryFromRelation);
                            }

                            // Update obsession from interaction count
                            float obsessionFromInteractions = math.min(1f, relation.InteractionCount / 100f);
                            bond.Obsession = math.lerp(bond.Obsession, obsessionFromInteractions, 0.05f);

                            break;
                        }
                    }
                }

                // Same for B's relation to A
                if (RelationLookup.HasBuffer(bond.B))
                {
                    var relations = RelationLookup[bond.B];
                    for (int i = 0; i < relations.Length; i++)
                    {
                        if (relations[i].OtherEntity == bond.A)
                        {
                            var relation = relations[i];
                            float trustFromRelation = relation.Trust / 100f;
                            bond.TrustBA = math.lerp(bond.TrustBA, trustFromRelation, 0.1f);
                            break;
                        }
                    }
                }
            }

            void HandleStateTransitions(ref CompanionBond bond)
            {
                float avgTrust = (bond.TrustAB + bond.TrustBA) * 0.5f;

                switch (bond.State)
                {
                    case CompanionState.Forming:
                        // Forming → Active: Intensity > 0.5 AND average trust > 0.6
                        if (bond.Intensity > 0.5f && avgTrust > 0.6f)
                        {
                            bond.State = CompanionState.Active;
                        }
                        break;

                    case CompanionState.Active:
                        // Active → Strained: Major conflict (low trust, high rivalry)
                        if (avgTrust < 0.3f && bond.Rivalry > 0.5f)
                        {
                            bond.State = CompanionState.Strained;
                        }
                        break;

                    case CompanionState.Strained:
                        // Strained → Broken: Intensity decays too low OR deliberate break
                        if (bond.Intensity < 0.2f)
                        {
                            bond.State = CompanionState.Broken;
                        }
                        // Strained → Active: Recovery (trust restored, rivalry reduced)
                        else if (avgTrust > 0.6f && bond.Rivalry < 0.3f)
                        {
                            bond.State = CompanionState.Active;
                        }
                        break;
                }
            }

            void HandleKindChanges(ref CompanionBond bond)
            {
                // Friend → Rival: Rivalry > 0.7 AND Intensity > 0.5
                if (bond.Kind == CompanionKind.Friend && bond.Rivalry > 0.7f && bond.Intensity > 0.5f)
                {
                    bond.Kind = CompanionKind.Rival;
                }
                // Rival → Nemesis: Rivalry > 0.9 AND Obsession > 0.7
                else if (bond.Kind == CompanionKind.Rival && bond.Rivalry > 0.9f && bond.Obsession > 0.7f)
                {
                    bond.Kind = CompanionKind.Nemesis;
                }
            }
        }
    }
}

