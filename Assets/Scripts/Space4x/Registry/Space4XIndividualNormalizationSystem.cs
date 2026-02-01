using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Profile;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures individuals (crew/pilots/officers) have baseline profile components.
    /// This keeps AI, morale, and loyalty systems from relying on partially-authored entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Profile.BehaviorDispositionSeedSystem))]
    public partial struct Space4XIndividualNormalizationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            var hasCatalog = SystemAPI.TryGetSingleton<IndividualProfileCatalogSingleton>(out var catalogSingleton) &&
                             catalogSingleton.Catalog.IsCreated &&
                             catalogSingleton.Catalog.Value.Profiles.Length > 0;

            var unique = new NativeParallelHashSet<Entity>(128, Allocator.Temp);
            var candidates = new NativeList<Entity>(128, Allocator.Temp);

            // Tagged individuals.
            foreach (var (_, entity) in SystemAPI.Query<RefRO<SimIndividualTag>>().WithEntityAccess())
            {
                TryAddCandidate(entity, em, ref unique, ref candidates);
            }

            // Individuals created with stats but missing the tag.
            foreach (var (_, entity) in SystemAPI.Query<RefRO<IndividualStats>>().WithNone<SimIndividualTag>().WithEntityAccess())
            {
                TryAddCandidate(entity, em, ref unique, ref candidates);
            }

            // Crew members referenced by platforms.
            foreach (var crewBuffer in SystemAPI.Query<DynamicBuffer<PlatformCrewMember>>())
            {
                for (int i = 0; i < crewBuffer.Length; i++)
                {
                    TryAddCandidate(crewBuffer[i].CrewEntity, em, ref unique, ref candidates);
                }
            }

            // Pilots referenced by vessels.
            foreach (var (pilotLink, _) in SystemAPI.Query<RefRO<VesselPilotLink>>().WithEntityAccess())
            {
                TryAddCandidate(pilotLink.ValueRO.Pilot, em, ref unique, ref candidates);
            }

            // Pilots referenced by strike craft.
            foreach (var (pilotLink, _) in SystemAPI.Query<RefRO<StrikeCraftPilotLink>>().WithEntityAccess())
            {
                TryAddCandidate(pilotLink.ValueRO.Pilot, em, ref unique, ref candidates);
            }

            // Authority seat occupants.
            foreach (var (occupant, _) in SystemAPI.Query<RefRO<AuthoritySeatOccupant>>().WithEntityAccess())
            {
                TryAddCandidate(occupant.ValueRO.OccupantEntity, em, ref unique, ref candidates);
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                var entity = candidates[i];
                IndividualProfileTemplate template = default;
                var hasTemplate = hasCatalog && TryResolveTemplate(entity, em, catalogSingleton, out template);
                NormalizeIndividual(entity, em, ref ecb, hasTemplate, template);
            }

            ecb.Playback(em);
            unique.Dispose();
            candidates.Dispose();
        }

        private static void TryAddCandidate(
            Entity entity,
            EntityManager em,
            ref NativeParallelHashSet<Entity> unique,
            ref NativeList<Entity> candidates)
        {
            if (entity == Entity.Null || !em.Exists(entity))
            {
                return;
            }

            if (unique.Add(entity))
            {
                candidates.Add(entity);
            }
        }

        private static void NormalizeIndividual(Entity entity, EntityManager em, ref EntityCommandBuffer ecb)
        {
            if (entity == Entity.Null || !em.Exists(entity))
            {
                return;
            }

            NormalizeIndividual(entity, em, ref ecb, false, default);
        }

        private static void NormalizeIndividual(
            Entity entity,
            EntityManager em,
            ref EntityCommandBuffer ecb,
            bool hasTemplate,
            IndividualProfileTemplate template)
        {
            if (entity == Entity.Null || !em.Exists(entity))
            {
                return;
            }

            if (!em.HasComponent<SimIndividualTag>(entity))
            {
                ecb.AddComponent<SimIndividualTag>(entity);
            }

            if (!em.HasComponent<IndividualId>(entity))
            {
                ecb.AddComponent(entity, new IndividualId { Value = entity.Index });
            }

            var hasAlignment = em.HasComponent<AlignmentTriplet>(entity);
            var alignmentValue = hasAlignment
                ? em.GetComponentData<AlignmentTriplet>(entity)
                : hasTemplate
                    ? template.Alignment
                    : AlignmentTriplet.FromFloats(0f, 0f, 0f);

            if (!hasAlignment)
            {
                ecb.AddComponent(entity, alignmentValue);
            }

            if (!em.HasBuffer<OutlookEntry>(entity))
            {
                var outlooks = ecb.AddBuffer<OutlookEntry>(entity);
                if (hasTemplate && template.Outlooks.Length > 0)
                {
                    for (int i = 0; i < template.Outlooks.Length; i++)
                    {
                        var entry = template.Outlooks[i];
                        outlooks.Add(new OutlookEntry
                        {
                            OutlookId = entry.OutlookId,
                            Weight = entry.Weight
                        });
                    }
                }
                else
                {
                    outlooks.Add(new OutlookEntry
                    {
                        OutlookId = OutlookId.Neutral,
                        Weight = (half)0.15f
                    });
                }
            }

            var hasBehavior = em.HasComponent<BehaviorDisposition>(entity);
            var wantsExplicitBehavior = hasTemplate ? template.BehaviorExplicit != 0 : true;
            var behaviorValue = hasTemplate ? template.Behavior : DefaultBehaviorDisposition();

            if (!hasBehavior)
            {
                if (wantsExplicitBehavior)
                {
                    ecb.AddComponent(entity, behaviorValue);
                    if (em.HasComponent<BehaviorDispositionSeedRequest>(entity))
                    {
                        ecb.RemoveComponent<BehaviorDispositionSeedRequest>(entity);
                    }
                }
                else if (!em.HasComponent<BehaviorDispositionSeedRequest>(entity))
                {
                    ecb.AddComponent(entity, new BehaviorDispositionSeedRequest
                    {
                        Seed = 0u,
                        SeedSalt = 0u
                    });
                }
            }

            if (!em.HasComponent<IndividualStats>(entity))
            {
                var stats = hasTemplate ? template.Stats : DefaultIndividualStats();
                ecb.AddComponent(entity, stats);
            }

            if (!em.HasComponent<DerivedCapacities>(entity))
            {
                var capacities = hasTemplate ? template.Capacities : DefaultCapacities();
                ecb.AddComponent(entity, capacities);
            }

            if (!em.HasComponent<PhysiqueFinesseWill>(entity))
            {
                var physique = hasTemplate ? template.Physique : DefaultPhysique();
                ecb.AddComponent(entity, physique);
            }

            if (!em.HasComponent<PersonalityAxes>(entity))
            {
                var personality = hasTemplate ? template.Personality : PersonalityAxes.FromValues(0f, 0f, 0f, 0f, 0f);
                ecb.AddComponent(entity, personality);
            }

            if (!em.HasComponent<MoraleState>(entity))
            {
                if (hasTemplate && template.MoraleExplicit != 0)
                {
                    ecb.AddComponent(entity, MoraleState.FromBaseline(template.MoraleBaseline, template.MoraleDriftRate));
                }
                else
                {
                    var baseline = MoraleUtility.ComputeBaseline(alignmentValue);
                    ecb.AddComponent(entity, MoraleState.FromBaseline(baseline));
                }
            }

            if (!em.HasBuffer<MoraleModifier>(entity))
            {
                ecb.AddBuffer<MoraleModifier>(entity);
            }

            if (!em.HasComponent<PatriotismProfile>(entity))
            {
                var patriotism = hasTemplate ? template.Patriotism : PatriotismProfile.Default();
                ecb.AddComponent(entity, patriotism);
            }

            if (!em.HasBuffer<BelongingEntry>(entity))
            {
                ecb.AddBuffer<BelongingEntry>(entity);
            }

            if (!em.HasComponent<PatriotismModifiers>(entity))
            {
                ecb.AddComponent(entity, PatriotismModifiers.Default());
            }
        }

        private static bool TryResolveTemplate(
            Entity entity,
            EntityManager em,
            in IndividualProfileCatalogSingleton catalog,
            out IndividualProfileTemplate template)
        {
            template = default;
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var profiles = ref catalog.Catalog.Value.Profiles;
            if (profiles.Length == 0)
            {
                return false;
            }

            var profileId = default(FixedString64Bytes);
            if (em.HasComponent<IndividualProfileId>(entity))
            {
                profileId = em.GetComponentData<IndividualProfileId>(entity).Id;
            }

            if (profileId.Length == 0)
            {
                profileId = catalog.DefaultProfileId;
            }

            if (profileId.Length > 0)
            {
                for (int i = 0; i < profiles.Length; i++)
                {
                    if (profiles[i].Id.Equals(profileId))
                    {
                        template = profiles[i];
                        return true;
                    }
                }
            }

            template = profiles[0];
            return true;
        }

        private static IndividualStats DefaultIndividualStats()
        {
            return new IndividualStats
            {
                Command = (half)65f,
                Tactics = (half)60f,
                Logistics = (half)60f,
                Diplomacy = (half)55f,
                Engineering = (half)50f,
                Resolve = (half)60f
            };
        }

        private static BehaviorDisposition DefaultBehaviorDisposition()
        {
            return BehaviorDisposition.FromValues(0.7f, 0.6f, 0.65f, 0.45f, 0.4f, 0.6f);
        }

        private static PhysiqueFinesseWill DefaultPhysique()
        {
            return new PhysiqueFinesseWill
            {
                Physique = (half)50f,
                Finesse = (half)50f,
                Will = (half)50f,
                PhysiqueInclination = 5,
                FinesseInclination = 5,
                WillInclination = 5,
                GeneralXP = 0f
            };
        }

        private static DerivedCapacities DefaultCapacities()
        {
            return new DerivedCapacities
            {
                Sight = 1f,
                Manipulation = 1f,
                Consciousness = 1f,
                ReactionTime = 1f,
                Boarding = 1f
            };
        }
    }
}
