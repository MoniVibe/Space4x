using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Individual;
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
    /// System that scans for candidate companion pairs and creates bonds.
    /// Runs periodically based on CompanionConfig.FormationCheckInterval.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    public partial struct CompanionFormationSystem : ISystem
    {
        BufferLookup<EntityRelation> _relationLookup;
        ComponentLookup<EntityAlignment> _alignmentLookup;
        ComponentLookup<EntityOutlook> _outlookLookup;
        ComponentLookup<PureDOTS.Runtime.Individual.PersonalityAxes> _personalityLookup;
        ComponentLookup<PureDOTS.Runtime.Individual.MightMagicAffinity> _affinityLookup;
        BufferLookup<CompanionLink> _companionLinkBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<CompanionConfig>();

            _relationLookup = state.GetBufferLookup<EntityRelation>(true);
            _companionLinkBufferLookup = state.GetBufferLookup<CompanionLink>(false);
            _alignmentLookup = state.GetComponentLookup<EntityAlignment>(true);
            _outlookLookup = state.GetComponentLookup<EntityOutlook>(true);
            _personalityLookup = state.GetComponentLookup<PureDOTS.Runtime.Individual.PersonalityAxes>(true);
            _affinityLookup = state.GetComponentLookup<PureDOTS.Runtime.Individual.MightMagicAffinity>(true);
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

            // Check if it's time to run formation scan
            if (currentTick % config.FormationCheckInterval != 0)
                return;

            _relationLookup.Update(ref state);
            _companionLinkBufferLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _personalityLookup.Update(ref state);
            _affinityLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Scan groups for candidate pairs
            var job = new FormationScanJob
            {
                CurrentTick = currentTick,
                Config = config,
                RelationLookup = _relationLookup,
                CompanionLinkBufferLookup = _companionLinkBufferLookup,
                AlignmentLookup = _alignmentLookup,
                OutlookLookup = _outlookLookup,
                PersonalityLookup = _personalityLookup,
                AffinityLookup = _affinityLookup,
                Ecb = ecb.AsParallelWriter()
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct FormationScanJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public CompanionConfig Config;
            [ReadOnly] public BufferLookup<EntityRelation> RelationLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<CompanionLink> CompanionLinkBufferLookup;
            [ReadOnly] public ComponentLookup<EntityAlignment> AlignmentLookup;
            [ReadOnly] public ComponentLookup<EntityOutlook> OutlookLookup;
            [ReadOnly] public ComponentLookup<PureDOTS.Runtime.Individual.PersonalityAxes> PersonalityLookup;
            [ReadOnly] public ComponentLookup<PureDOTS.Runtime.Individual.MightMagicAffinity> AffinityLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute([EntityIndexInQuery] int entityInQueryIndex, DynamicBuffer<GroupMember> members)
            {
                if (members.Length < 2)
                    return;

                // Check all pairs in the group
                for (int i = 0; i < members.Length - 1; i++)
                {
                    var memberA = members[i].MemberEntity;
                    if (memberA == Entity.Null)
                        continue;

                    for (int j = i + 1; j < members.Length; j++)
                    {
                        var memberB = members[j].MemberEntity;
                        if (memberB == Entity.Null)
                            continue;

                        // Check if bond already exists
                        if (HasExistingBond(memberA, memberB))
                            continue;

                        // Check bond count limits
                        if (GetBondCount(memberA) >= Config.MaxBondsPerEntity ||
                            GetBondCount(memberB) >= Config.MaxBondsPerEntity)
                            continue;

                        // Check relation intensity
                        if (!HasHighRelationIntensity(memberA, memberB))
                            continue;

                        // Compute compatibility
                        float compatibility = ComputeCompatibility(memberA, memberB);
                        if (compatibility < Config.MinCompatibilityForBond)
                            continue;

                        // Determine bond kind from relation type and compatibility
                        CompanionKind kind = DetermineBondKind(memberA, memberB, compatibility);

                        // Create bond entity
                        Entity bondEntity = Ecb.CreateEntity(entityInQueryIndex);
                        Ecb.AddComponent(entityInQueryIndex, bondEntity, new CompanionBondTag());
                        Ecb.AddComponent(entityInQueryIndex, bondEntity, new CompanionBond
                        {
                            A = memberA,
                            B = memberB,
                            Kind = kind,
                            State = CompanionState.Forming,
                            Intensity = 0.3f, // Start at forming level
                            TrustAB = 0.5f,
                            TrustBA = 0.5f,
                            Rivalry = kind == CompanionKind.Rival || kind == CompanionKind.Nemesis ? 0.4f : 0f,
                            Obsession = 0f,
                            FormedTick = CurrentTick,
                            LastUpdateTick = CurrentTick
                        });

                        // Add links to both entities (buffers will be added if needed in follow-up)
                        if (CompanionLinkBufferLookup.HasBuffer(memberA))
                        {
                            var linksA = CompanionLinkBufferLookup[memberA];
                            linksA.Add(new CompanionLink { Bond = bondEntity });
                        }
                        else
                        {
                            Ecb.AddBuffer<CompanionLink>(entityInQueryIndex, memberA);
                        }

                        if (CompanionLinkBufferLookup.HasBuffer(memberB))
                        {
                            var linksB = CompanionLinkBufferLookup[memberB];
                            linksB.Add(new CompanionLink { Bond = bondEntity });
                        }
                        else
                        {
                            Ecb.AddBuffer<CompanionLink>(entityInQueryIndex, memberB);
                        }
                    }
                }
            }

            bool HasExistingBond(Entity a, Entity b)
            {
                if (!CompanionLinkBufferLookup.HasBuffer(a))
                    return false;

                var linksA = CompanionLinkBufferLookup[a];
                for (int i = 0; i < linksA.Length; i++)
                {
                    var bondEntity = linksA[i].Bond;
                    if (CompanionLinkBufferLookup.HasBuffer(b))
                    {
                        var linksB = CompanionLinkBufferLookup[b];
                        for (int j = 0; j < linksB.Length; j++)
                        {
                            if (linksB[j].Bond == bondEntity)
                                return true;
                        }
                    }
                }
                return false;
            }

            int GetBondCount(Entity entity)
            {
                if (!CompanionLinkBufferLookup.HasBuffer(entity))
                    return 0;
                return CompanionLinkBufferLookup[entity].Length;
            }

            bool HasHighRelationIntensity(Entity a, Entity b)
            {
                if (!RelationLookup.HasBuffer(a))
                    return false;

                var relations = RelationLookup[a];
                for (int i = 0; i < relations.Length; i++)
                {
                    if (relations[i].OtherEntity == b)
                    {
                        return relations[i].Intensity >= Config.MinRelationIntensityForBond ||
                               relations[i].Intensity <= -Config.MinRelationIntensityForBond; // Negative intensity for rivals
                    }
                }
                return false;
            }

            float ComputeCompatibility(Entity a, Entity b)
            {
                // Get identity components
                EntityAlignment alignA = AlignmentLookup.HasComponent(a) ? AlignmentLookup[a] : default;
                EntityAlignment alignB = AlignmentLookup.HasComponent(b) ? AlignmentLookup[b] : default;

                EntityOutlook outlookA = OutlookLookup.HasComponent(a) ? OutlookLookup[a] : default;
                EntityOutlook outlookB = OutlookLookup.HasComponent(b) ? OutlookLookup[b] : default;

                PureDOTS.Runtime.Individual.PersonalityAxes persAInd = PersonalityLookup.HasComponent(a) ? PersonalityLookup[a] : default;
                PureDOTS.Runtime.Individual.PersonalityAxes persBInd = PersonalityLookup.HasComponent(b) ? PersonalityLookup[b] : default;

                PureDOTS.Runtime.Individual.MightMagicAffinity affAInd = AffinityLookup.HasComponent(a) ? AffinityLookup[a] : default;
                PureDOTS.Runtime.Individual.MightMagicAffinity affBInd = AffinityLookup.HasComponent(b) ? AffinityLookup[b] : default;

                // Convert Individual types to Identity types for compatibility calculation
                PureDOTS.Runtime.Identity.PersonalityAxes persA = new PureDOTS.Runtime.Identity.PersonalityAxes
                {
                    CravenBold = persAInd.Boldness * 100f, // Convert -1..1 to -100..100
                    VengefulForgiving = persAInd.Vengefulness * 100f
                };
                PureDOTS.Runtime.Identity.PersonalityAxes persB = new PureDOTS.Runtime.Identity.PersonalityAxes
                {
                    CravenBold = persBInd.Boldness * 100f,
                    VengefulForgiving = persBInd.Vengefulness * 100f
                };
                PureDOTS.Runtime.Identity.MightMagicAffinity affA = new PureDOTS.Runtime.Identity.MightMagicAffinity
                {
                    Axis = affAInd.Value * 100f, // Convert -1..1 to -100..100
                    Strength = 1f
                };
                PureDOTS.Runtime.Identity.MightMagicAffinity affB = new PureDOTS.Runtime.Identity.MightMagicAffinity
                {
                    Axis = affBInd.Value * 100f,
                    Strength = 1f
                };

                // Use IdentityCompatibility system
                return IdentityCompatibility.CalculateCompatibility(
                    alignA, outlookA, persA, affA,
                    alignB, outlookB, persB, affB);
            }

            CompanionKind DetermineBondKind(Entity a, Entity b, float compatibility)
            {
                // Check relation type for hints
                if (RelationLookup.HasBuffer(a))
                {
                    var relations = RelationLookup[a];
                    for (int i = 0; i < relations.Length; i++)
                    {
                        if (relations[i].OtherEntity == b)
                        {
                            var relationType = relations[i].Type;
                            var intensity = relations[i].Intensity;

                            // Negative intensity suggests rivalry
                            if (intensity < -50)
                            {
                                if (intensity < -80)
                                    return CompanionKind.Nemesis;
                                return CompanionKind.Rival;
                            }

                            // Check relation type
                            switch (relationType)
                            {
                                case RelationType.Lover:
                                case RelationType.Courting:
                                case RelationType.Betrothed:
                                    return CompanionKind.Lover;
                                case RelationType.BestFriend:
                                case RelationType.CloseFriend:
                                    return CompanionKind.Friend;
                                case RelationType.Sibling:
                                    return CompanionKind.Sibling;
                                case RelationType.Mentor:
                                    return CompanionKind.Mentor;
                                case RelationType.Student:
                                    return CompanionKind.Protégé;
                                case RelationType.Rival:
                                    return CompanionKind.Rival;
                                case RelationType.Nemesis:
                                    return CompanionKind.Nemesis;
                            }
                        }
                    }
                }

                // Default based on compatibility
                if (compatibility > 80)
                    return CompanionKind.Friend;
                if (compatibility < -50)
                    return CompanionKind.Rival;
                if (compatibility < -80)
                    return CompanionKind.Nemesis;

                return CompanionKind.Other;
            }
        }
    }
}

