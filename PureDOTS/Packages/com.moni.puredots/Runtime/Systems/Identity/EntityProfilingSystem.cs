using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Stats;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CoreIndividualStats = PureDOTS.Runtime.Individual.IndividualStats;
using Space4XIndividualStats = PureDOTS.Runtime.Stats.IndividualStats;
using IdentityPersonalityAxes = PureDOTS.Runtime.Identity.PersonalityAxes;

namespace PureDOTS.Systems.Identity
{
    /// <summary>
    /// Bootstrap: Auto-applies profiling to entities with VillagerId/SimIndividualTag missing EntityProfile.
    /// Implements hybrid integration - profiling applies automatically unless entities already have profiling components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct EntityProfilingBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Bootstrap Godgame villagers: entities with VillagerId but missing EntityProfile
            // Skip entities with SkipEntityProfiling opt-out component
            foreach (var (villagerId, entity) in SystemAPI.Query<RefRO<VillagerId>>()
                .WithNone<EntityProfile, SkipEntityProfiling>()
                .WithEntityAccess())
            {
                // Skip if already has all profiling components (manually created)
                bool hasAllComponents = state.EntityManager.HasComponent<CoreIndividualStats>(entity) &&
                                       state.EntityManager.HasComponent<EntityAlignment>(entity) &&
                                       state.EntityManager.HasComponent<EntityOutlook>(entity) &&
                                       state.EntityManager.HasComponent<IdentityPersonalityAxes>(entity) &&
                                       state.EntityManager.HasComponent<DerivedAttributes>(entity) &&
                                       state.EntityManager.HasComponent<SocialStats>(entity);

                if (hasAllComponents)
                    continue;

                // Create EntityProfile (archetype resolved later)
                ecb.AddComponent(entity, new EntityProfile
                {
                    ArchetypeName = default,
                    Source = EntityProfileSource.Generated,
                    CreatedTick = timeState.Tick,
                    IsResolved = 0
                });

                // Initialize ProfileApplicationState
                ecb.AddComponent(entity, new ProfileApplicationState
                {
                    Phase = ProfileApplicationPhase.None,
                    LastUpdatedTick = timeState.Tick,
                    NeedsRecalculation = 0
                });
            }

            // Bootstrap Space4X individuals: entities with SimIndividualTag but missing EntityProfile
            // Skip entities with SkipEntityProfiling opt-out component
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<SimIndividualTag>>()
                .WithNone<EntityProfile, SkipEntityProfiling>()
                .WithEntityAccess())
            {
                // Skip if already has all profiling components (manually created)
                bool hasAllComponents = state.EntityManager.HasComponent<CoreIndividualStats>(entity) &&
                                       state.EntityManager.HasComponent<EntityAlignment>(entity) &&
                                       state.EntityManager.HasComponent<EntityOutlook>(entity) &&
                                       state.EntityManager.HasComponent<IdentityPersonalityAxes>(entity) &&
                                       state.EntityManager.HasComponent<DerivedAttributes>(entity) &&
                                       state.EntityManager.HasComponent<Space4XIndividualStats>(entity);

                if (hasAllComponents)
                    continue;

                // Create EntityProfile for Space4X entities (no archetype needed)
                ecb.AddComponent(entity, new EntityProfile
                {
                    ArchetypeName = default, // Space4X entities don't use villager archetypes
                    Source = EntityProfileSource.Generated,
                    CreatedTick = timeState.Tick,
                    IsResolved = 0
                });

                // Initialize ProfileApplicationState
                ecb.AddComponent(entity, new ProfileApplicationState
                {
                    Phase = ProfileApplicationPhase.None,
                    LastUpdatedTick = timeState.Tick,
                    NeedsRecalculation = 0
                });
            }

            // Handle existing EntityProfile with CreatedTick == 0 (update tick to current)
            foreach (var (profile, entity) in SystemAPI.Query<RefRO<EntityProfile>>()
                .WithNone<SkipEntityProfiling>()
                .WithEntityAccess())
            {
                if (profile.ValueRO.CreatedTick == 0)
                {
                    var updatedProfile = profile.ValueRO;
                    updatedProfile.CreatedTick = timeState.Tick;
                    ecb.SetComponent(entity, updatedProfile);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Phase 1: Resolves archetype assignments to VillagerArchetypeResolved.
    /// Uses existing VillagerArchetypeResolutionSystem logic but triggers on EntityProfile.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EntityProfilingBootstrapSystem))]
    public partial struct ArchetypeResolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var hasCatalog = SystemAPI.TryGetSingleton<VillagerArchetypeCatalogComponent>(out var catalogComponent) &&
                             catalogComponent.Catalog.IsCreated;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            VillagerArchetypeDefaults.CreateFallback(out var fallback);

            foreach (var (profile, entity) in SystemAPI.Query<RefRO<EntityProfile>>()
                .WithNone<VillagerArchetypeResolved>()
                .WithEntityAccess())
            {
                FixedString64Bytes archetypeName = profile.ValueRO.ArchetypeName;
                
                // If no archetype name, try to use existing assignment data
                if (archetypeName.Length == 0 && state.EntityManager.HasComponent<VillagerArchetypeAssignment>(entity))
                {
                    var assignment = state.EntityManager.GetComponentData<VillagerArchetypeAssignment>(entity);
                    if (assignment.ArchetypeName.Length > 0)
                    {
                        archetypeName = assignment.ArchetypeName;
                        var updatedProfile = profile.ValueRO;
                        updatedProfile.ArchetypeName = archetypeName;
                        ecb.SetComponent(entity, updatedProfile);
                    }
                }

                // If no archetype name but entity has VillagerId, fall back to "Default"
                if (archetypeName.Length == 0 && state.EntityManager.HasComponent<VillagerId>(entity))
                {
                    // Burst-safe construction: build FixedString using Append
                    var defaultName = new FixedString64Bytes();
                    defaultName.Append('D');
                    defaultName.Append('e');
                    defaultName.Append('f');
                    defaultName.Append('a');
                    defaultName.Append('u');
                    defaultName.Append('l');
                    defaultName.Append('t');
                    archetypeName = defaultName;

                    var updatedProfile = profile.ValueRO;
                    updatedProfile.ArchetypeName = archetypeName;
                    ecb.SetComponent(entity, updatedProfile);
                }
                
                // Skip if still no archetype name (Space4X entities may not need archetypes)
                if (archetypeName.Length == 0)
                    continue;

                // Add archetype assignment if missing
                if (!state.EntityManager.HasComponent<VillagerArchetypeAssignment>(entity))
                {
                    ecb.AddComponent(entity, new VillagerArchetypeAssignment
                    {
                        ArchetypeName = archetypeName,
                        CachedIndex = -1
                    });
                }

                // Create VillagerArchetypeResolved with fallback data
                // The existing VillagerArchetypeResolutionSystem will update it with catalog data
                ecb.AddComponent(entity, new VillagerArchetypeResolved
                {
                    ArchetypeIndex = -1,
                    Data = fallback
                });

                // Update profile state
                if (state.EntityManager.HasComponent<ProfileApplicationState>(entity))
                {
                    var appState = state.EntityManager.GetComponentData<ProfileApplicationState>(entity);
                    appState.Phase = ProfileApplicationPhase.ArchetypeAssigned;
                    appState.LastUpdatedTick = timeState.Tick;
                    ecb.SetComponent(entity, appState);
                }
                else
                {
                    ecb.AddComponent(entity, new ProfileApplicationState
                    {
                        Phase = ProfileApplicationPhase.ArchetypeAssigned,
                        LastUpdatedTick = timeState.Tick,
                        NeedsRecalculation = 0
                    });
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Phase 2: Applies base stats from resolved archetype.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ArchetypeResolutionSystem))]
    public partial struct BaseStatsApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (resolved, entity) in SystemAPI.Query<RefRO<VillagerArchetypeResolved>>()
                .WithNone<CoreIndividualStats>()
                .WithEntityAccess())
            {
                var archetypeData = resolved.ValueRO.Data;

                // Check for profile data to override archetype values
                float physique = archetypeData.BasePhysique;
                float finesse = archetypeData.BaseFinesse;
                float willpower = archetypeData.BaseWillpower;
                float wisdom = 50f; // Default

                if (state.EntityManager.HasComponent<VillagerProfileData>(entity))
                {
                    var profileData = state.EntityManager.GetComponentData<VillagerProfileData>(entity);
                    if (profileData.BasePhysique > 0) physique = profileData.BasePhysique;
                    if (profileData.BaseFinesse > 0) finesse = profileData.BaseFinesse;
                    if (profileData.BaseWill > 0) willpower = profileData.BaseWill;
                    if (profileData.BaseWisdom > 0) wisdom = profileData.BaseWisdom;
                }

                // Apply IndividualStats from archetype or profile
                var stats = new CoreIndividualStats
                {
                    Physique = physique,
                    Finesse = finesse,
                    Will = willpower,
                    Agility = finesse * 0.8f, // Default derivation
                    Intellect = willpower * 0.8f,
                    Social = 50f, // Default
                    Faith = 50f // Default
                };
                ecb.AddComponent(entity, stats);

                // Apply WisdomStat from profile or default
                if (!state.EntityManager.HasComponent<WisdomStat>(entity))
                {
                    ecb.AddComponent(entity, WisdomStat.FromValue(wisdom));
                }

                // Calculate and apply ResourcePools
                var pools = new ResourcePools
                {
                    MaxHP = 50f + 0.6f * stats.Physique + 0.4f * stats.Will,
                    MaxStamina = stats.Physique / 10f,
                    MaxMana = 0.5f * stats.Will + 0.5f * stats.Intellect,
                    MaxFocus = 0.5f * stats.Intellect + 0.5f * stats.Will,
                    HP = 0f, // Will be initialized to MaxHP by other systems
                    Stamina = 0f,
                    Mana = 0f,
                    Focus = 0f
                };
                pools.HP = pools.MaxHP; // Initialize to max
                pools.Stamina = pools.MaxStamina;
                pools.Mana = pools.MaxMana;
                pools.Focus = pools.MaxFocus;

                if (!state.EntityManager.HasComponent<ResourcePools>(entity))
                {
                    ecb.AddComponent(entity, pools);
                }

                // Update profile state
                if (state.EntityManager.HasComponent<ProfileApplicationState>(entity))
                {
                    var appState = state.EntityManager.GetComponentData<ProfileApplicationState>(entity);
                    appState.Phase = ProfileApplicationPhase.StatsApplied;
                    appState.LastUpdatedTick = timeState.Tick;
                    ecb.SetComponent(entity, appState);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Phase 3: Calculates derived attributes from base stats + XP.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BaseStatsApplicationSystem))]
    public partial struct DerivedAttributesCalculationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Query entities that need derived attributes calculated or recalculated
            foreach (var (stats, entity) in SystemAPI.Query<RefRO<CoreIndividualStats>>()
                .WithEntityAccess())
            {
                bool needsCalculation = !state.EntityManager.HasComponent<DerivedAttributes>(entity);
                
                if (!needsCalculation)
                {
                    var existingDerived = state.EntityManager.GetComponentData<DerivedAttributes>(entity);
                    if (existingDerived.NeedsRecalculation == 0)
                        continue;
                    needsCalculation = true;
                }

                var baseStats = stats.ValueRO;

                // Get XP stats if available (default to 0)
                float physiqueXP = 0f;
                float finesseXP = 0f;
                float willXP = 0f;
                float wisdomXP = 0f;

                if (state.EntityManager.HasComponent<XPStats>(entity))
                {
                    var xpStats = state.EntityManager.GetComponentData<XPStats>(entity);
                    physiqueXP = xpStats.PhysiqueXP;
                    finesseXP = xpStats.FinesseXP;
                    willXP = xpStats.WillXP;
                    wisdomXP = xpStats.WisdomXP;
                }

                // Calculate derived attributes (using default weights)
                // Strength = 0.8 * Physique + 0.2 * WeaponMastery (using PhysiqueXP as proxy)
                var strength = 0.8f * baseStats.Physique + 0.2f * math.min(physiqueXP / 10f, 100f);

                // Agility = 0.8 * Finesse + 0.2 * Acrobatics (using FinesseXP as proxy)
                var agility = 0.8f * baseStats.Finesse + 0.2f * math.min(finesseXP / 10f, 100f);

                // Intelligence = 0.6 * Will + 0.4 * Education (using WillXP as proxy)
                var intelligence = 0.6f * baseStats.Will + 0.4f * math.min(willXP / 10f, 100f);

                // WisdomDerived = 0.6 * Will + 0.4 * Lore (using WisdomXP as proxy)
                var wisdomDerived = 0.6f * baseStats.Will + 0.4f * math.min(wisdomXP / 10f, 100f);

                var derived = new DerivedAttributes
                {
                    Strength = math.clamp(strength, 0f, 100f),
                    Agility = math.clamp(agility, 0f, 100f),
                    Intelligence = math.clamp(intelligence, 0f, 100f),
                    WisdomDerived = math.clamp(wisdomDerived, 0f, 100f),
                    LastRecalculatedTick = timeState.Tick,
                    NeedsRecalculation = 0
                };

                if (state.EntityManager.HasComponent<DerivedAttributes>(entity))
                {
                    ecb.SetComponent(entity, derived);
                }
                else
                {
                    ecb.AddComponent(entity, derived);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Phase 4: Applies alignment and outlook from archetype lean.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DerivedAttributesCalculationSystem))]
    public partial struct AlignmentOutlookApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (resolved, entity) in SystemAPI.Query<RefRO<VillagerArchetypeResolved>>()
                .WithNone<EntityAlignment>()
                .WithEntityAccess())
            {
                var archetypeData = resolved.ValueRO.Data;

                // Check for profile data alignment leans first, fallback to archetype
                float moralLean = (float)archetypeData.MoralAxisLean;
                float orderLean = (float)archetypeData.OrderAxisLean;
                float purityLean = (float)archetypeData.PurityAxisLean;

                if (state.EntityManager.HasComponent<VillagerProfileData>(entity))
                {
                    var profileData = state.EntityManager.GetComponentData<VillagerProfileData>(entity);
                    if (math.abs(profileData.MoralAxisLean) > 0.01f) moralLean = profileData.MoralAxisLean;
                    if (math.abs(profileData.OrderAxisLean) > 0.01f) orderLean = profileData.OrderAxisLean;
                    if (math.abs(profileData.PurityAxisLean) > 0.01f) purityLean = profileData.PurityAxisLean;
                }

                // Apply EntityAlignment from archetype lean or profile data
                var alignment = new EntityAlignment
                {
                    Moral = math.clamp(moralLean, -100f, 100f),
                    Order = math.clamp(orderLean, -100f, 100f),
                    Purity = math.clamp(purityLean, -100f, 100f),
                    Strength = 0.5f // Default conviction
                };
                ecb.AddComponent(entity, alignment);

                // Derive EntityOutlook from alignment
                DeriveOutlook(in alignment, out var outlook);
                ecb.AddComponent(entity, outlook);

                // Seed TraitAxisValue buffer with alignment axes
                DynamicBuffer<TraitAxisValue> traitBuffer;
                if (state.EntityManager.HasBuffer<TraitAxisValue>(entity))
                {
                    var existingBuffer = state.EntityManager.GetBuffer<TraitAxisValue>(entity);
                    traitBuffer = ecb.SetBuffer<TraitAxisValue>(entity);
                    // Copy existing entries
                    for (int i = 0; i < existingBuffer.Length; i++)
                    {
                        traitBuffer.Add(existingBuffer[i]);
                    }
                }
                else
                {
                    traitBuffer = ecb.AddBuffer<TraitAxisValue>(entity);
                }
                
                traitBuffer.Add(new TraitAxisValue { AxisId = BuildLawfulChaotic(), Value = alignment.Order });
                traitBuffer.Add(new TraitAxisValue { AxisId = BuildGoodEvil(), Value = alignment.Moral });
                traitBuffer.Add(new TraitAxisValue { AxisId = BuildCorruptPure(), Value = alignment.Purity });

                // Update profile state
                if (state.EntityManager.HasComponent<ProfileApplicationState>(entity))
                {
                    var appState = state.EntityManager.GetComponentData<ProfileApplicationState>(entity);
                    appState.Phase = ProfileApplicationPhase.AlignmentApplied;
                    appState.LastUpdatedTick = timeState.Tick;
                    ecb.SetComponent(entity, appState);
                }
            }

            ecb.Playback(state.EntityManager);
        }

        [BurstCompile]
        private static void DeriveOutlook(in EntityAlignment alignment, out EntityOutlook outlook)
        {
            outlook = new EntityOutlook
            {
                Primary = OutlookType.None,
                Secondary = OutlookType.None,
                Tertiary = OutlookType.None
            };

            // Derive primary outlook from alignment combinations
            if (alignment.Moral > 50f && alignment.Order > 30f)
            {
                outlook.Primary = OutlookType.Warlike; // Heroic
            }
            else if (alignment.Moral < -50f)
            {
                outlook.Primary = OutlookType.Authoritarian; // Ruthless
            }
            else if (alignment.Order > 50f)
            {
                outlook.Primary = OutlookType.Scholarly; // Methodical
            }
            else if (alignment.Order < -50f)
            {
                outlook.Primary = OutlookType.Pragmatic; // Rebellious
            }
            else
            {
                outlook.Primary = OutlookType.Pragmatic; // Default neutral
            }

            // Derive secondary outlook
            if (alignment.Purity > 50f)
            {
                outlook.Secondary = OutlookType.Spiritual; // Devout
            }
            else if (alignment.Purity < -50f)
            {
                outlook.Secondary = OutlookType.Materialistic; // Corrupt
            }

        }

        private static FixedString32Bytes BuildLawfulChaotic()
        {
            var fs = new FixedString32Bytes();
            fs.Append('L'); fs.Append('a'); fs.Append('w'); fs.Append('f'); fs.Append('u'); fs.Append('l');
            fs.Append('C'); fs.Append('h'); fs.Append('a'); fs.Append('o'); fs.Append('t'); fs.Append('i'); fs.Append('c');
            return fs;
        }

        private static FixedString32Bytes BuildGoodEvil()
        {
            var fs = new FixedString32Bytes();
            fs.Append('G'); fs.Append('o'); fs.Append('o'); fs.Append('d');
            fs.Append('E'); fs.Append('v'); fs.Append('i'); fs.Append('l');
            return fs;
        }

        private static FixedString32Bytes BuildCorruptPure()
        {
            var fs = new FixedString32Bytes();
            fs.Append('C'); fs.Append('o'); fs.Append('r'); fs.Append('r'); fs.Append('u'); fs.Append('p'); fs.Append('t');
            fs.Append('P'); fs.Append('u'); fs.Append('r'); fs.Append('e');
            return fs;
        }
    }

    /// <summary>
    /// Phase 5: Applies personality axes and behavior tuning.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AlignmentOutlookApplicationSystem))]
    public partial struct PersonalityApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<EntityAlignment>>()
                .WithNone<IdentityPersonalityAxes>()
                .WithEntityAccess())
            {
                // Get personality values from profile data if available, otherwise generate deterministically
                float vengefulForgiving = 0f;
                float cravenBold = 0f;
                float cooperativeCompetitive = 0f;
                float warlikePeaceful = 0f;

                bool hasProfileData = false;
                if (state.EntityManager.HasComponent<VillagerProfileData>(entity))
                {
                    var profileData = state.EntityManager.GetComponentData<VillagerProfileData>(entity);
                    vengefulForgiving = profileData.VengefulForgiving;
                    cravenBold = profileData.CravenBold;
                    cooperativeCompetitive = profileData.CooperativeCompetitive;
                    warlikePeaceful = profileData.WarlikePeaceful;
                    hasProfileData = true;
                }
                else if (state.EntityManager.HasComponent<IndividualProfileData>(entity))
                {
                    var profileData = state.EntityManager.GetComponentData<IndividualProfileData>(entity);
                    vengefulForgiving = profileData.VengefulForgiving;
                    cravenBold = profileData.CravenBold;
                    hasProfileData = true;
                }

                // Generate deterministic random values if not in profile (use entity index + tick as seed)
                if (!hasProfileData || (math.abs(vengefulForgiving) < 0.01f && math.abs(cravenBold) < 0.01f))
                {
                    var seed = (uint)(entity.Index + (int)timeState.Tick);
                    var random = new Unity.Mathematics.Random(seed);
                    if (!hasProfileData || math.abs(vengefulForgiving) < 0.01f)
                        vengefulForgiving = random.NextFloat(-100f, 100f);
                    if (!hasProfileData || math.abs(cravenBold) < 0.01f)
                        cravenBold = random.NextFloat(-100f, 100f);
                    if (!hasProfileData || math.abs(cooperativeCompetitive) < 0.01f)
                        cooperativeCompetitive = random.NextFloat(-100f, 100f);
                    if (!hasProfileData || math.abs(warlikePeaceful) < 0.01f)
                        warlikePeaceful = random.NextFloat(-100f, 100f);
                }

                var personality = new IdentityPersonalityAxes
                {
                    VengefulForgiving = math.clamp(vengefulForgiving, -100f, 100f),
                    CravenBold = math.clamp(cravenBold, -100f, 100f)
                };
                ecb.AddComponent(entity, personality);

                // Apply ExtendedPersonalityAxes
                var extendedPersonality = ExtendedPersonalityAxes.FromValues(
                    math.clamp(cooperativeCompetitive, -100f, 100f),
                    math.clamp(warlikePeaceful, -100f, 100f)
                );
                ecb.AddComponent(entity, extendedPersonality);

                // Calculate BehaviorTuning from personality + alignment + stats
                var stats = state.EntityManager.HasComponent<CoreIndividualStats>(entity)
                    ? state.EntityManager.GetComponentData<CoreIndividualStats>(entity)
                    : default;

                var alignmentValue = alignment.ValueRO;
                CalculateBehaviorTuning(in personality, in extendedPersonality, in alignmentValue, in stats, out var behaviorTuning);
                ecb.AddComponent(entity, behaviorTuning);

                // Add personality axes to TraitAxisValue buffer
                if (state.EntityManager.HasBuffer<TraitAxisValue>(entity))
                {
                    var existingBuffer = state.EntityManager.GetBuffer<TraitAxisValue>(entity);
                    var traitBuffer = ecb.SetBuffer<TraitAxisValue>(entity);
                    // Copy existing entries
                    for (int i = 0; i < existingBuffer.Length; i++)
                    {
                        traitBuffer.Add(existingBuffer[i]);
                    }
                    // Add personality axes
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildVengefulForgiving(), Value = personality.VengefulForgiving });
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildBoldCraven(), Value = personality.CravenBold });
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildCooperativeCompetitive(), Value = extendedPersonality.CooperativeCompetitive });
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildWarlikePeaceful(), Value = extendedPersonality.WarlikePeaceful });
                }
                else
                {
                    var traitBuffer = ecb.AddBuffer<TraitAxisValue>(entity);
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildVengefulForgiving(), Value = personality.VengefulForgiving });
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildBoldCraven(), Value = personality.CravenBold });
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildCooperativeCompetitive(), Value = extendedPersonality.CooperativeCompetitive });
                    traitBuffer.Add(new TraitAxisValue { AxisId = BuildWarlikePeaceful(), Value = extendedPersonality.WarlikePeaceful });
                }

                // Update profile state
                if (state.EntityManager.HasComponent<ProfileApplicationState>(entity))
                {
                    var appState = state.EntityManager.GetComponentData<ProfileApplicationState>(entity);
                    appState.Phase = ProfileApplicationPhase.PersonalityApplied;
                    appState.LastUpdatedTick = timeState.Tick;
                    ecb.SetComponent(entity, appState);
                }
            }

            ecb.Playback(state.EntityManager);
        }

        [BurstCompile]
        private static void CalculateBehaviorTuning(
            in IdentityPersonalityAxes personality,
            in ExtendedPersonalityAxes extendedPersonality,
            in EntityAlignment alignment,
            in CoreIndividualStats stats,
            out BehaviorTuning tuningOut)
        {
            // AggressionBias = (BoldScore / 100.0) * 0.5 + 1.0 (0.5 to 1.5)
            var aggressionBias = (personality.CravenBold / 100f) * 0.5f + 1f;

            // SocialBias = (CooperativeCompetitive / 100.0) * 0.3 + 1.0
            var socialBias = (extendedPersonality.CooperativeCompetitive / 100f) * 0.3f + 1f;

            // GreedBias = (Purity < 0 ? 1.2 : 0.9) (corrupt = more greedy)
            var greedBias = alignment.Purity < 0f ? 1.2f : 0.9f;

            // CuriosityBias = (Intellect / 100.0) * 0.4 + 0.8
            var curiosityBias = (stats.Intellect / 100f) * 0.4f + 0.8f;

            // ObedienceBias = (Order / 100.0) * 0.5 + 0.75 (lawful = more obedient)
            var obedienceBias = (alignment.Order / 100f) * 0.5f + 0.75f;

            tuningOut = new BehaviorTuning
            {
                AggressionBias = math.clamp(aggressionBias, 0f, 2f),
                SocialBias = math.clamp(socialBias, 0f, 2f),
                GreedBias = math.clamp(greedBias, 0f, 2f),
                CuriosityBias = math.clamp(curiosityBias, 0f, 2f),
                ObedienceBias = math.clamp(obedienceBias, 0f, 2f)
            };
        }

        private static FixedString32Bytes BuildVengefulForgiving()
        {
            var fs = new FixedString32Bytes();
            fs.Append('V'); fs.Append('e'); fs.Append('n'); fs.Append('g'); fs.Append('e'); fs.Append('f'); fs.Append('u'); fs.Append('l');
            fs.Append('F'); fs.Append('o'); fs.Append('r'); fs.Append('g'); fs.Append('i'); fs.Append('v'); fs.Append('i'); fs.Append('n'); fs.Append('g');
            return fs;
        }

        private static FixedString32Bytes BuildBoldCraven()
        {
            var fs = new FixedString32Bytes();
            fs.Append('B'); fs.Append('o'); fs.Append('l'); fs.Append('d');
            fs.Append('C'); fs.Append('r'); fs.Append('a'); fs.Append('v'); fs.Append('e'); fs.Append('n');
            return fs;
        }

        private static FixedString32Bytes BuildCooperativeCompetitive()
        {
            var fs = new FixedString32Bytes();
            fs.Append('C'); fs.Append('o'); fs.Append('o'); fs.Append('p'); fs.Append('e'); fs.Append('r'); fs.Append('a'); fs.Append('t'); fs.Append('i'); fs.Append('v'); fs.Append('e');
            fs.Append('C'); fs.Append('o'); fs.Append('m'); fs.Append('p'); fs.Append('e'); fs.Append('t'); fs.Append('i'); fs.Append('t'); fs.Append('i'); fs.Append('v'); fs.Append('e');
            return fs;
        }

        private static FixedString32Bytes BuildWarlikePeaceful()
        {
            var fs = new FixedString32Bytes();
            fs.Append('W'); fs.Append('a'); fs.Append('r'); fs.Append('l'); fs.Append('i'); fs.Append('k'); fs.Append('e');
            fs.Append('P'); fs.Append('e'); fs.Append('a'); fs.Append('c'); fs.Append('e'); fs.Append('f'); fs.Append('u'); fs.Append('l');
            return fs;
        }
    }

    /// <summary>
    /// Phase 6a: Applies social stats for Godgame villagers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PersonalityApplicationSystem))]
    public partial struct SocialStatsApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Only apply to Godgame entities (those with VillagerId but not SocialStats)
            foreach (var (villagerId, entity) in SystemAPI.Query<RefRO<VillagerId>>()
                .WithNone<SocialStats>()
                .WithEntityAccess())
            {
                // Use VillagerProfileData values if present, otherwise use defaults
                float fame = 0f;
                float wealth = 0f;
                float reputation = 0f;
                float glory = 0f;

                if (state.EntityManager.HasComponent<VillagerProfileData>(entity))
                {
                    var profile = state.EntityManager.GetComponentData<VillagerProfileData>(entity);
                    fame = profile.InitialFame;
                    wealth = profile.InitialWealth;
                    reputation = profile.InitialReputation;
                    glory = profile.InitialGlory;
                }
                
                // Apply SocialStats with values from profile or defaults
                var socialStats = new SocialStats
                {
                    Fame = fame,
                    Wealth = wealth,
                    Reputation = reputation,
                    Glory = glory,
                    Renown = SocialStats.CalculateRenown(fame, glory)
                };
                ecb.AddComponent(entity, socialStats);

                // Apply XPStats with default values
                if (!state.EntityManager.HasComponent<XPStats>(entity))
                {
                    ecb.AddComponent(entity, new XPStats
                    {
                        PhysiqueXP = 0f,
                        FinesseXP = 0f,
                        WillXP = 0f,
                        WisdomXP = 0f
                    });
                }

                // Update profile state
                if (state.EntityManager.HasComponent<ProfileApplicationState>(entity))
                {
                    var appState = state.EntityManager.GetComponentData<ProfileApplicationState>(entity);
                    appState.Phase = ProfileApplicationPhase.GameSpecificApplied;
                    appState.LastUpdatedTick = timeState.Tick;
                    ecb.SetComponent(entity, appState);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Phase 6b: Applies officer stats for Space4X individuals.
    /// Applies Space4X IndividualStats (Command, Tactics, etc.) and related components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PersonalityApplicationSystem))]
    public partial struct OfficerStatsApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Apply to Space4X entities (those with IndividualProfileData but not Space4X IndividualStats)
            foreach (var (profileData, entity) in SystemAPI.Query<RefRO<IndividualProfileData>>()
                .WithNone<Space4XIndividualStats>()
                .WithEntityAccess())
            {
                var profile = profileData.ValueRO;

                // Apply Space4X IndividualStats with values from profile (default to 50 if not set)
                var officerStats = new Space4XIndividualStats
                {
                    Command = profile.Command > 0 ? profile.Command : (half)50f,
                    Tactics = profile.Tactics > 0 ? profile.Tactics : (half)50f,
                    Logistics = profile.Logistics > 0 ? profile.Logistics : (half)50f,
                    Diplomacy = profile.Diplomacy > 0 ? profile.Diplomacy : (half)50f,
                    Engineering = profile.Engineering > 0 ? profile.Engineering : (half)50f,
                    Resolve = profile.Resolve > 0 ? profile.Resolve : (half)50f
                };
                ecb.AddComponent(entity, officerStats);

                // Apply PhysiqueFinesseWill component
                if (!state.EntityManager.HasComponent<PhysiqueFinesseWill>(entity))
                {
                    var pfw = new PhysiqueFinesseWill
                    {
                        Physique = (half)math.clamp(profile.BasePhysique / 10f, 1f, 10f), // Convert 0-100 to 1-10
                        Finesse = (half)math.clamp(profile.BaseFinesse / 10f, 1f, 10f),
                        Will = (half)math.clamp(profile.BaseWill / 10f, 1f, 10f),
                        PhysiqueInclination = (half)5f, // Default
                        FinesseInclination = (half)5f,
                        WillInclination = (half)5f,
                        GeneralXP = 0f
                    };
                    ecb.AddComponent(entity, pfw);
                }

                // Add ExpertiseEntry buffer and populate from profile if provided
                DynamicBuffer<ExpertiseEntry> expertiseBuffer;
                if (state.EntityManager.HasBuffer<ExpertiseEntry>(entity))
                {
                    var existingBuffer = state.EntityManager.GetBuffer<ExpertiseEntry>(entity);
                    expertiseBuffer = ecb.SetBuffer<ExpertiseEntry>(entity);
                    // Copy existing entries
                    for (int i = 0; i < existingBuffer.Length; i++)
                    {
                        expertiseBuffer.Add(existingBuffer[i]);
                    }
                }
                else
                {
                    expertiseBuffer = ecb.AddBuffer<ExpertiseEntry>(entity);
                }

                // Populate expertise buffer from profile data
                if (profile.InitialExpertiseTypes.Length > 0)
                {
                    for (int i = 0; i < profile.InitialExpertiseTypes.Length; i++)
                    {
                        var expertiseType = profile.InitialExpertiseTypes[i];
                        if (expertiseType.Length > 0)
                        {
                            expertiseBuffer.Add(new ExpertiseEntry
                            {
                                Type = expertiseType,
                                Tier = 1 // Default tier
                            });
                        }
                    }
                }

                // Add ServiceTrait buffer and populate from profile if provided
                DynamicBuffer<ServiceTrait> serviceTraitBuffer;
                if (state.EntityManager.HasBuffer<ServiceTrait>(entity))
                {
                    var existingBuffer = state.EntityManager.GetBuffer<ServiceTrait>(entity);
                    serviceTraitBuffer = ecb.SetBuffer<ServiceTrait>(entity);
                    // Copy existing entries
                    for (int i = 0; i < existingBuffer.Length; i++)
                    {
                        serviceTraitBuffer.Add(existingBuffer[i]);
                    }
                }
                else
                {
                    serviceTraitBuffer = ecb.AddBuffer<ServiceTrait>(entity);
                }

                // Populate service trait buffer from profile data
                if (profile.InitialServiceTraits.Length > 0)
                {
                    for (int i = 0; i < profile.InitialServiceTraits.Length; i++)
                    {
                        var traitId = profile.InitialServiceTraits[i];
                        if (traitId.Length > 0)
                        {
                            serviceTraitBuffer.Add(new ServiceTrait
                            {
                                Id = traitId
                            });
                        }
                    }
                }

                // Update profile state
                if (state.EntityManager.HasComponent<ProfileApplicationState>(entity))
                {
                    var appState = state.EntityManager.GetComponentData<ProfileApplicationState>(entity);
                    appState.Phase = ProfileApplicationPhase.GameSpecificApplied;
                    appState.LastUpdatedTick = timeState.Tick;
                    ecb.SetComponent(entity, appState);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Phase 7: Marks profile application as complete.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SocialStatsApplicationSystem))]
    public partial struct ProfileCompletionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            var entityManager = state.EntityManager;

            foreach (var (appState, entity) in SystemAPI.Query<RefRO<ProfileApplicationState>>()
                .WithEntityAccess())
            {
                var appStateData = appState.ValueRO;

                // Check if all required components are present
                bool hasStats = entityManager.HasComponent<CoreIndividualStats>(entity);
                bool hasAlignment = entityManager.HasComponent<EntityAlignment>(entity);
                bool hasOutlook = entityManager.HasComponent<EntityOutlook>(entity);
                bool hasPersonality = entityManager.HasComponent<IdentityPersonalityAxes>(entity);
                bool hasBehaviorTuning = entityManager.HasComponent<BehaviorTuning>(entity);
                bool hasDerivedAttributes = entityManager.HasComponent<DerivedAttributes>(entity);

                // Game-specific checks
                bool hasGameSpecific = false;
                if (entityManager.HasComponent<VillagerProfileData>(entity))
                {
                    // Godgame: check for SocialStats, XPStats, WisdomStat
                    hasGameSpecific = entityManager.HasComponent<SocialStats>(entity) &&
                                      entityManager.HasComponent<XPStats>(entity) &&
                                      entityManager.HasComponent<WisdomStat>(entity);
                }
                else if (entityManager.HasComponent<IndividualProfileData>(entity))
                {
                    // Space4X: check for Space4X IndividualStats, PhysiqueFinesseWill, ExpertiseEntry buffer
                    hasGameSpecific = entityManager.HasComponent<Space4XIndividualStats>(entity) &&
                                      entityManager.HasComponent<PhysiqueFinesseWill>(entity) &&
                                      entityManager.HasBuffer<ExpertiseEntry>(entity);
                }

                // Mark complete if all core components and game-specific components are present
                if (hasStats && hasAlignment && hasOutlook && hasPersonality && hasBehaviorTuning && 
                    hasDerivedAttributes && hasGameSpecific &&
                    appStateData.Phase != ProfileApplicationPhase.Complete)
                {
                    var newState = appStateData;
                    newState.Phase = ProfileApplicationPhase.Complete;
                    newState.LastUpdatedTick = timeState.Tick;
                    ecb.SetComponent(entity, newState);

                    // Mark EntityProfile as resolved
                    if (entityManager.HasComponent<EntityProfile>(entity))
                    {
                        var profile = entityManager.GetComponentData<EntityProfile>(entity);
                        profile.IsResolved = 1;
                        ecb.SetComponent(entity, profile);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

