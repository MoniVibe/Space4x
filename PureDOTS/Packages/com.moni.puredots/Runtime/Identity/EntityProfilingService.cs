using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Stats;
using PureDOTS.Runtime.Villagers;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CoreIndividualStats = PureDOTS.Runtime.Individual.IndividualStats;
using Space4XIndividualStats = PureDOTS.Runtime.Stats.IndividualStats;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Static service for entity profiling operations.
    /// Provides helper methods for applying profiles, archetypes, stats, alignments, and personalities to entities.
    /// </summary>
    [BurstCompile]
    public static class EntityProfilingService
    {
        // Construct "Default" archetype name from bytes to avoid BC1016/BC1091
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CreateDefaultArchetypeName(out FixedString64Bytes name)
        {
            name = default(FixedString64Bytes);
            // "Default" in UTF-8 bytes: 0x44, 0x65, 0x66, 0x61, 0x75, 0x6C, 0x74
            name.Append((byte)0x44); // D
            name.Append((byte)0x65); // e
            name.Append((byte)0x66); // f
            name.Append((byte)0x61); // a
            name.Append((byte)0x75); // u
            name.Append((byte)0x6C); // l
            name.Append((byte)0x74); // t
        }

        /// <summary>
        /// Apply archetype assignment to entity.
        /// </summary>
        [BurstCompile]
        public static void ApplyArchetype(ref EntityManager entityManager, in Entity entity, in FixedString64Bytes archetypeName)
        {
            if (!entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<VillagerArchetypeAssignment>(entity))
            {
                entityManager.AddComponentData(entity, new VillagerArchetypeAssignment
                {
                    ArchetypeName = archetypeName,
                    CachedIndex = -1
                });
            }
            else
            {
                var assignment = entityManager.GetComponentData<VillagerArchetypeAssignment>(entity);
                assignment.ArchetypeName = archetypeName;
                assignment.CachedIndex = -1; // Reset cache
                entityManager.SetComponentData(entity, assignment);
            }

            // Mark profile as having archetype assigned
            if (entityManager.HasComponent<EntityProfile>(entity))
            {
                var profile = entityManager.GetComponentData<EntityProfile>(entity);
                profile.IsResolved = 0; // Will be resolved by system
                entityManager.SetComponentData(entity, profile);
            }
        }

        /// <summary>
        /// Apply complete profile to entity.
        /// </summary>
        [BurstCompile]
        public static void ApplyProfile(ref EntityManager entityManager, in Entity entity, in EntityProfile profile)
        {
            if (!entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<EntityProfile>(entity))
            {
                entityManager.AddComponentData(entity, profile);
            }
            else
            {
                entityManager.SetComponentData(entity, profile);
            }

            if (profile.ArchetypeName.Length > 0)
            {
                ApplyArchetype(ref entityManager, entity, profile.ArchetypeName);
            }
        }

        /// <summary>
        /// Create Godgame villager from profile data.
        /// </summary>
        [BurstCompile]
        public static void CreateVillager(ref EntityManager entityManager, in Entity entity, in VillagerProfileData profile, in FixedString64Bytes archetypeName = default, uint createdTick = 0)
        {
            if (!entityManager.Exists(entity))
                return;

            // Use provided tick - bootstrap system will set it if needed
            uint tick = createdTick;

            var resolvedArchetypeName = archetypeName;
            // Use "Default" archetype if none provided but profile has stats
            if (resolvedArchetypeName.Length == 0 && (profile.BasePhysique > 0 || profile.BaseFinesse > 0 || profile.BaseWill > 0))
            {
                CreateDefaultArchetypeName(out resolvedArchetypeName);
            }

            // Add profile component
            if (!entityManager.HasComponent<EntityProfile>(entity))
            {
                entityManager.AddComponentData(entity, new EntityProfile
                {
                    ArchetypeName = resolvedArchetypeName,
                    Source = EntityProfileSource.Generated,
                    CreatedTick = tick,
                    IsResolved = 0
                });
            }
            else
            {
                var existingProfile = entityManager.GetComponentData<EntityProfile>(entity);
                existingProfile.ArchetypeName = resolvedArchetypeName;
                existingProfile.CreatedTick = tick;
                entityManager.SetComponentData(entity, existingProfile);
            }

            // Store profile data for systems to use
            if (!entityManager.HasComponent<VillagerProfileData>(entity))
            {
                entityManager.AddComponentData(entity, profile);
            }
            else
            {
                entityManager.SetComponentData(entity, profile);
            }

            // Apply archetype if name provided
            if (resolvedArchetypeName.Length > 0)
            {
                ApplyArchetype(ref entityManager, entity, resolvedArchetypeName);
            }

            // Create temporary IndividualStats from profile if stats provided
            if (profile.BasePhysique > 0 || profile.BaseFinesse > 0 || profile.BaseWill > 0)
            {
                var stats = new CoreIndividualStats
                {
                    Physique = profile.BasePhysique,
                    Finesse = profile.BaseFinesse,
                    Will = profile.BaseWill,
                    Agility = profile.BaseFinesse * 0.8f, // Default derivation
                    Intellect = profile.BaseWill * 0.8f,
                    Social = 50f, // Default
                    Faith = 50f // Default
                };
                
                if (!entityManager.HasComponent<CoreIndividualStats>(entity))
                {
                    entityManager.AddComponentData(entity, stats);
                }
                else
                {
                    entityManager.SetComponentData(entity, stats);
                }
            }
        }

        /// <summary>
        /// Create Space4X individual from profile data.
        /// </summary>
        [BurstCompile]
        public static void CreateIndividual(ref EntityManager entityManager, in Entity entity, in IndividualProfileData profile, in FixedString64Bytes archetypeName = default, uint createdTick = 0)
        {
            CreateIndividualWithOfficerStats(ref entityManager, entity, profile, archetypeName, createdTick, default, default, default);
        }

        /// <summary>
        /// Create Space4X individual from profile data with officer stats.
        /// </summary>
        [BurstCompile]
        public static void CreateIndividualWithOfficerStats(
            ref EntityManager entityManager, 
            in Entity entity, 
            in IndividualProfileData profile, 
            in FixedString64Bytes archetypeName = default, 
            uint createdTick = 0,
            in Space4XIndividualStats officerStats = default,
            in FixedList128Bytes<FixedString32Bytes> initialExpertiseTypes = default,
            in FixedList128Bytes<FixedString32Bytes> initialServiceTraits = default)
        {
            if (!entityManager.Exists(entity))
                return;

            // Use provided tick - bootstrap system will set it if needed
            uint tick = createdTick;

            // If officer stats provided (not all zeros), populate profile
            bool hasOfficerStats = (float)officerStats.Command > 0.01f || 
                                  (float)officerStats.Tactics > 0.01f || 
                                  (float)officerStats.Logistics > 0.01f ||
                                  (float)officerStats.Diplomacy > 0.01f ||
                                  (float)officerStats.Engineering > 0.01f ||
                                  (float)officerStats.Resolve > 0.01f;

            var profileValue = profile;

            if (hasOfficerStats)
            {
                profileValue.Command = officerStats.Command;
                profileValue.Tactics = officerStats.Tactics;
                profileValue.Logistics = officerStats.Logistics;
                profileValue.Diplomacy = officerStats.Diplomacy;
                profileValue.Engineering = officerStats.Engineering;
                profileValue.Resolve = officerStats.Resolve;
            }

            // Capture initial expertise/service traits data
            profileValue.InitialExpertiseTypes = initialExpertiseTypes;
            profileValue.InitialServiceTraits = initialServiceTraits;

            // Add profile component
            if (!entityManager.HasComponent<EntityProfile>(entity))
            {
                entityManager.AddComponentData(entity, new EntityProfile
                {
                    ArchetypeName = archetypeName,
                    Source = EntityProfileSource.Generated,
                    CreatedTick = tick,
                    IsResolved = 0
                });
            }
            else
            {
                var existingProfile = entityManager.GetComponentData<EntityProfile>(entity);
                existingProfile.ArchetypeName = archetypeName;
                existingProfile.CreatedTick = tick;
                entityManager.SetComponentData(entity, existingProfile);
            }

            // Store profile data for systems to use
            if (!entityManager.HasComponent<IndividualProfileData>(entity))
            {
                entityManager.AddComponentData(entity, profileValue);
            }
            else
            {
                entityManager.SetComponentData(entity, profileValue);
            }

            // Apply archetype if name provided
            if (archetypeName.Length > 0)
            {
                ApplyArchetype(ref entityManager, entity, archetypeName);
            }

            // Create IndividualStats from profile
            var stats = new CoreIndividualStats
            {
                Physique = profileValue.BasePhysique,
                Finesse = profileValue.BaseFinesse,
                Will = profileValue.BaseWill,
                Agility = profileValue.BaseFinesse * 0.8f, // Default derivation
                Intellect = profileValue.BaseWill * 0.8f,
                Social = 50f, // Default
                Faith = 50f // Default
            };
            
            if (!entityManager.HasComponent<CoreIndividualStats>(entity))
            {
                entityManager.AddComponentData(entity, stats);
            }
            else
            {
                entityManager.SetComponentData(entity, stats);
            }
        }

        /// <summary>
        /// Apply base IndividualStats to entity.
        /// </summary>
        [BurstCompile]
        public static void ApplyBaseStats(ref EntityManager entityManager, in Entity entity, in CoreIndividualStats stats)
        {
            if (!entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<CoreIndividualStats>(entity))
            {
                entityManager.AddComponentData(entity, stats);
            }
            else
            {
                entityManager.SetComponentData(entity, stats);
            }
        }

        /// <summary>
        /// Apply EntityAlignment to entity.
        /// </summary>
        [BurstCompile]
        public static void ApplyAlignment(ref EntityManager entityManager, in Entity entity, in EntityAlignment alignment)
        {
            if (!entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<EntityAlignment>(entity))
            {
                entityManager.AddComponentData(entity, alignment);
            }
            else
            {
                entityManager.SetComponentData(entity, alignment);
            }
        }

        /// <summary>
        /// Apply EntityOutlook to entity.
        /// </summary>
        [BurstCompile]
        public static void ApplyOutlook(ref EntityManager entityManager, in Entity entity, in EntityOutlook outlook)
        {
            if (!entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<EntityOutlook>(entity))
            {
                entityManager.AddComponentData(entity, outlook);
            }
            else
            {
                entityManager.SetComponentData(entity, outlook);
            }
        }

        /// <summary>
        /// Apply PersonalityAxes to entity.
        /// </summary>
        [BurstCompile]
        public static void ApplyPersonality(ref EntityManager entityManager, in Entity entity, in PersonalityAxes personality)
        {
            if (!entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<PersonalityAxes>(entity))
            {
                entityManager.AddComponentData(entity, personality);
            }
            else
            {
                entityManager.SetComponentData(entity, personality);
            }
        }

        /// <summary>
        /// Resolve archetype for entity by ensuring VillagerArchetypeAssignment exists.
        /// The VillagerArchetypeResolutionSystem will populate VillagerArchetypeResolved.
        /// </summary>
        [BurstCompile]
        public static void ResolveArchetype(ref EntityManager entityManager, in Entity entity)
        {
            if (!entityManager.Exists(entity))
                return;

            // Ensure EntityProfile exists
            if (!entityManager.HasComponent<EntityProfile>(entity))
            {
                entityManager.AddComponentData(entity, new EntityProfile
                {
                    ArchetypeName = default,
                    Source = EntityProfileSource.Generated,
                    CreatedTick = 0,
                    IsResolved = 0
                });
            }

            // Ensure VillagerArchetypeAssignment exists if EntityProfile has archetype name
            var profile = entityManager.GetComponentData<EntityProfile>(entity);
            if (profile.ArchetypeName.Length > 0 && !entityManager.HasComponent<VillagerArchetypeAssignment>(entity))
            {
                ApplyArchetype(ref entityManager, entity, profile.ArchetypeName);
            }
        }

        /// <summary>
        /// Trigger recalculation of derived attributes for entity.
        /// Sets NeedsRecalculation flag on DerivedAttributes component.
        /// </summary>
        [BurstCompile]
        public static void ApplyDerivedStats(ref EntityManager entityManager, in Entity entity)
        {
            if (!entityManager.Exists(entity))
                return;

            if (entityManager.HasComponent<DerivedAttributes>(entity))
            {
                var derived = entityManager.GetComponentData<DerivedAttributes>(entity);
                derived.NeedsRecalculation = 1;
                entityManager.SetComponentData(entity, derived);
            }
            else
            {
                // Create with NeedsRecalculation flag set - system will calculate
                entityManager.AddComponentData(entity, new DerivedAttributes
                {
                    Strength = 0f,
                    Agility = 0f,
                    Intelligence = 0f,
                    WisdomDerived = 0f,
                    LastRecalculatedTick = 0,
                    NeedsRecalculation = 1
                });
            }
        }

        /// <summary>
        /// Check if profile application is complete for entity.
        /// </summary>
        [BurstCompile]
        public static bool IsProfileComplete(ref EntityManager entityManager, in Entity entity)
        {
            if (!entityManager.Exists(entity))
                return false;

            if (!entityManager.HasComponent<ProfileApplicationState>(entity))
                return false;

            var state = entityManager.GetComponentData<ProfileApplicationState>(entity);
            return state.Phase == ProfileApplicationPhase.Complete;
        }
    }
}

