using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    using PDCarrierModuleSlot = PureDOTS.Runtime.Ships.CarrierModuleSlot;
    using PDShipModule = PureDOTS.Runtime.Ships.ShipModule;
    using PDCrewMember = PureDOTS.Runtime.Platform.PlatformCrewMember;

    /// <summary>
    /// Resolves leisure/housing outputs from facility limbs into needs, morale modifiers, and incident risk.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4XMoraleSystem))]
    public partial struct Space4XLeisureNeedSystem : ISystem
    {
        private BufferLookup<PDCarrierModuleSlot> _moduleSlotsLookup;
        private ComponentLookup<PDShipModule> _shipModuleLookup;
        private ComponentLookup<LeisureFacilityLimb> _facilityLookup;
        private ComponentLookup<ModuleFunctionData> _moduleFunctionLookup;
        private BufferLookup<ModuleLimbState> _limbStateLookup;
        private ComponentLookup<CrewCapacity> _crewCapacityLookup;
        private BufferLookup<PDCrewMember> _crewLookup;
        private BufferLookup<EthicAxisValue> _axisLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<SupplyStatus> _supplyLookup;
        private ComponentLookup<SuspicionScore> _suspicionLookup;
        private ComponentLookup<LeisureEducationPolicy> _educationPolicyLookup;
        private ComponentLookup<LeisureEducationProgress> _educationProgressLookup;
        private ComponentLookup<SkillExperienceGain> _xpLookup;
        private ComponentLookup<CrewSkills> _skillsLookup;
        private ComponentLookup<PureDOTS.Runtime.Stats.WisdomStat> _wisdomLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MoraleState>();
            state.RequireForUpdate<LeisureNeedState>();

            _moduleSlotsLookup = state.GetBufferLookup<PDCarrierModuleSlot>(true);
            _shipModuleLookup = state.GetComponentLookup<PDShipModule>(true);
            _facilityLookup = state.GetComponentLookup<LeisureFacilityLimb>(true);
            _moduleFunctionLookup = state.GetComponentLookup<ModuleFunctionData>(true);
            _limbStateLookup = state.GetBufferLookup<ModuleLimbState>(true);
            _crewCapacityLookup = state.GetComponentLookup<CrewCapacity>(true);
            _crewLookup = state.GetBufferLookup<PDCrewMember>(true);
            _axisLookup = state.GetBufferLookup<EthicAxisValue>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(false);
            _supplyLookup = state.GetComponentLookup<SupplyStatus>(false);
            _suspicionLookup = state.GetComponentLookup<SuspicionScore>(false);
            _educationPolicyLookup = state.GetComponentLookup<LeisureEducationPolicy>(false);
            _educationProgressLookup = state.GetComponentLookup<LeisureEducationProgress>(false);
            _xpLookup = state.GetComponentLookup<SkillExperienceGain>(false);
            _skillsLookup = state.GetComponentLookup<CrewSkills>(false);
            _wisdomLookup = state.GetComponentLookup<PureDOTS.Runtime.Stats.WisdomStat>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _moduleSlotsLookup.Update(ref state);
            _shipModuleLookup.Update(ref state);
            _facilityLookup.Update(ref state);
            _moduleFunctionLookup.Update(ref state);
            _limbStateLookup.Update(ref state);
            _crewCapacityLookup.Update(ref state);
            _crewLookup.Update(ref state);
            _axisLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _supplyLookup.Update(ref state);
            _suspicionLookup.Update(ref state);
            _educationPolicyLookup.Update(ref state);
            _educationProgressLookup.Update(ref state);
            _xpLookup.Update(ref state);
            _skillsLookup.Update(ref state);
            _wisdomLookup.Update(ref state);

            var currentTick = time.Tick;
            var fixedDeltaTime = time.FixedDeltaTime;
            var hasSkillLog = SystemAPI.TryGetSingletonBuffer<SkillChangeLogEntry>(out var skillLog);

            foreach (var (needRef, preferenceRef, securityRef, aggregateRef, moraleModifiers, incidents, opportunities, entity) in
                SystemAPI.Query<
                    RefRW<LeisureNeedState>,
                    RefRW<LeisurePreferenceProfile>,
                    RefRO<LeisureSecurityPolicy>,
                    RefRW<LeisureFacilityAggregate>,
                    DynamicBuffer<MoraleModifier>,
                    DynamicBuffer<LeisureIncidentEvent>,
                    DynamicBuffer<LeisureOpportunityEvent>>()
                .WithEntityAccess())
            {
                var needs = needRef.ValueRO;
                var profile = preferenceRef.ValueRO;
                var security = securityRef.ValueRO;
                var educationPolicy = _educationPolicyLookup.HasComponent(entity)
                    ? _educationPolicyLookup[entity]
                    : LeisureEducationPolicy.Default;
                var educationProgress = _educationProgressLookup.HasComponent(entity)
                    ? _educationProgressLookup[entity]
                    : default;

                var chaos = 0.5f;
                var corruption = 0.5f;
                var warAxisConviction = ResolveAxisConviction(entity, EthicAxisId.War, ref _axisLookup);
                if (_alignmentLookup.HasComponent(entity))
                {
                    var alignment = _alignmentLookup[entity];
                    profile = LeisurePreferenceProfile.FromAlignment(alignment);
                    chaos = AlignmentMath.Chaos(alignment);
                    corruption = 1f - AlignmentMath.IntegrityNormalized(alignment);
                }
                profile = ApplyWarAxisPreferenceBias(profile, chaos, corruption, warAxisConviction);
                preferenceRef.ValueRW = profile;

                var aggregate = default(LeisureFacilityAggregate);
                var preferenceWeighted = 0f;
                var preferenceWeightTotal = 0f;
                var ambientLaw = 0f;
                var ambientGood = 0f;
                var ambientIntegrity = 0f;
                var ambientWeight = 0f;
                var schoolRate = 0f;
                var universityRate = 0f;
                var trainingRate = 0f;
                var wisdomRate = 0f;
                var researchRate = 0f;
                var ideologyRate = 0f;

                var arenaOpportunity = 0f;
                var orbitalOpportunity = 0f;
                var templeOpportunity = 0f;
                var purseRate = 0f;
                var lootRate = 0f;
                var salvageRate = 0f;
                var reputationRate = 0f;
                var topArenaOpportunity = 0f;
                var topOrbitalOpportunity = 0f;
                var topTempleOpportunity = 0f;
                var topArenaModule = Entity.Null;
                var topOrbitalModule = Entity.Null;
                var topTempleModule = Entity.Null;

                var topEspionageRisk = 0f;
                var topPoisonRisk = 0f;
                var topAssassinRisk = 0f;
                var topBriberyRisk = 0f;
                var topEspionageModule = Entity.Null;
                var topPoisonModule = Entity.Null;
                var topAssassinModule = Entity.Null;
                var topBriberyModule = Entity.Null;
                var topFacultySubversionRisk = 0f;
                var topFacultySubversionModule = Entity.Null;

                if (_moduleSlotsLookup.HasBuffer(entity))
                {
                    var slots = _moduleSlotsLookup[entity];
                    for (var i = 0; i < slots.Length; i++)
                    {
                        var module = slots[i].InstalledModule;
                        if (module == Entity.Null || !_shipModuleLookup.HasComponent(module))
                        {
                            continue;
                        }

                        var hasFacility = false;
                        LeisureFacilityLimb facility = default;

                        if (_facilityLookup.HasComponent(module))
                        {
                            facility = _facilityLookup[module];
                            hasFacility = true;
                        }
                        else if (_moduleFunctionLookup.HasComponent(module))
                        {
                            var function = _moduleFunctionLookup[module];
                            if (function.Function == ModuleFunction.Habitat)
                            {
                                facility = CreateHabitatFallback(function.Capacity);
                                hasFacility = true;
                            }
                        }

                        if (!hasFacility)
                        {
                            continue;
                        }

                        var integrity = ResolveFacilityIntegrity(module, ref _limbStateLookup);
                        if (integrity <= 1e-5f)
                        {
                            continue;
                        }

                        ApplyFacilityContribution(
                            ref aggregate,
                            in facility,
                            integrity,
                            in profile,
                            ref preferenceWeighted,
                            ref preferenceWeightTotal);

                        var educationContribution = math.max(0f, facility.EducationRate) * integrity;
                        var trainingContribution = math.max(0f, facility.TrainingRate) * integrity;
                        var wisdomContribution = math.max(0f, facility.WisdomRate) * integrity;
                        var researchContribution = math.max(0f, facility.ResearchRate) * integrity;
                        var ideologyContribution = math.max(0f, facility.IdeologyPressure) * integrity;

                        switch (facility.Type)
                        {
                            case LeisureFacilityType.School:
                                schoolRate += educationContribution;
                                break;
                            case LeisureFacilityType.University:
                                universityRate += educationContribution;
                                break;
                            case LeisureFacilityType.TrainingGround:
                                trainingRate += trainingContribution + educationContribution * 0.35f;
                                break;
                        }

                        wisdomRate += wisdomContribution;
                        researchRate += researchContribution;
                        ideologyRate += ideologyContribution;

                        var ambientIntensity = math.max(0f, facility.AmbientIntensity) * integrity;
                        if (ambientIntensity > 1e-5f)
                        {
                            ambientLaw += facility.AmbientLawBias * ambientIntensity;
                            ambientGood += facility.AmbientGoodBias * ambientIntensity;
                            ambientIntegrity += facility.AmbientIntegrityBias * ambientIntensity;
                            ambientWeight += ambientIntensity;
                        }

                        var preferenceAffinity = LeisureFacilityUtility.ResolvePreferenceWeight(profile, facility.Type);
                        var tierAffinity = facility.Type == LeisureFacilityType.Arena || facility.Type == LeisureFacilityType.OrbitalArena
                            ? LeisureFacilityUtility.ResolveArenaTierAffinity(profile, facility.ArenaTier)
                            : preferenceAffinity;
                        var opportunityAffinity = facility.Type == LeisureFacilityType.Temple
                            ? preferenceAffinity
                            : math.saturate((preferenceAffinity + tierAffinity) * 0.5f);

                        var baseOpportunity =
                            math.max(0f, facility.ParticipationPurseRate) +
                            math.max(0f, facility.LootYieldRate) +
                            math.max(0f, facility.SalvageRightsRate) +
                            math.max(0f, facility.ReputationYieldRate);
                        if (facility.Type == LeisureFacilityType.Temple && baseOpportunity <= 1e-5f)
                        {
                            baseOpportunity =
                                math.max(0f, facility.ReputationYieldRate) +
                                math.max(0f, facility.ComfortRate) * 0.25f +
                                math.max(0f, facility.SocialRate) * 0.2f;
                        }

                        var opportunityContribution = baseOpportunity * opportunityAffinity * integrity;
                        if (opportunityContribution > 1e-5f)
                        {
                            var weightedPurse = math.max(0f, facility.ParticipationPurseRate) * opportunityAffinity * integrity;
                            var weightedLoot = math.max(0f, facility.LootYieldRate) * opportunityAffinity * integrity;
                            var weightedSalvage = math.max(0f, facility.SalvageRightsRate) * opportunityAffinity * integrity;
                            var weightedReputation = math.max(0f, facility.ReputationYieldRate) * opportunityAffinity * integrity;

                            purseRate += weightedPurse;
                            lootRate += weightedLoot;
                            salvageRate += weightedSalvage;
                            reputationRate += weightedReputation;

                            switch (facility.Type)
                            {
                                case LeisureFacilityType.Arena:
                                    arenaOpportunity += opportunityContribution;
                                    if (opportunityContribution > topArenaOpportunity)
                                    {
                                        topArenaOpportunity = opportunityContribution;
                                        topArenaModule = module;
                                    }
                                    break;
                                case LeisureFacilityType.OrbitalArena:
                                    orbitalOpportunity += opportunityContribution;
                                    if (opportunityContribution > topOrbitalOpportunity)
                                    {
                                        topOrbitalOpportunity = opportunityContribution;
                                        topOrbitalModule = module;
                                    }
                                    break;
                                case LeisureFacilityType.Temple:
                                    templeOpportunity += opportunityContribution;
                                    if (opportunityContribution > topTempleOpportunity)
                                    {
                                        topTempleOpportunity = opportunityContribution;
                                        topTempleModule = module;
                                    }
                                    break;
                            }
                        }

                        var espionageContribution = math.max(0f, facility.EspionageRisk) * integrity;
                        if (espionageContribution > topEspionageRisk)
                        {
                            topEspionageRisk = espionageContribution;
                            topEspionageModule = module;
                        }

                        var poisonContribution = math.max(0f, facility.PoisonRisk) * integrity;
                        if (poisonContribution > topPoisonRisk)
                        {
                            topPoisonRisk = poisonContribution;
                            topPoisonModule = module;
                        }

                        var assassinContribution = math.max(0f, facility.AssassinationRisk) * integrity;
                        if (assassinContribution > topAssassinRisk)
                        {
                            topAssassinRisk = assassinContribution;
                            topAssassinModule = module;
                        }

                        var briberyContribution = math.max(0f, facility.BriberyPressure + facility.Illicitness * 0.35f) * integrity;
                        if (briberyContribution > topBriberyRisk)
                        {
                            topBriberyRisk = briberyContribution;
                            topBriberyModule = module;
                        }

                        var facultySubversionContribution = math.max(0f, facility.FacultyBriberyRisk) * integrity;
                        if (facultySubversionContribution > topFacultySubversionRisk)
                        {
                            topFacultySubversionRisk = facultySubversionContribution;
                            topFacultySubversionModule = module;
                        }
                    }
                }

                aggregate.PreferenceFit = preferenceWeightTotal > 1e-5f
                    ? math.saturate(preferenceWeighted / preferenceWeightTotal)
                    : 0.5f;

                var currentCrew = 0f;
                if (_crewCapacityLookup.HasComponent(entity))
                {
                    currentCrew = math.max(0f, _crewCapacityLookup[entity].CurrentCrew);
                }

                if (currentCrew > 0f)
                {
                    if (aggregate.HousingCapacity <= 1e-5f)
                    {
                        aggregate.Overcrowding = 1f;
                    }
                    else if (currentCrew > aggregate.HousingCapacity)
                    {
                        aggregate.Overcrowding = math.saturate((currentCrew - aggregate.HousingCapacity) / math.max(1f, currentCrew));
                    }
                    else
                    {
                        aggregate.Overcrowding = 0f;
                    }
                }

                var crewScale = math.max(1f, currentCrew / 8f);

                if (ambientWeight > 1e-5f)
                {
                    aggregate.AmbientLawBias = math.clamp(ambientLaw / ambientWeight, -1f, 1f);
                    aggregate.AmbientGoodBias = math.clamp(ambientGood / ambientWeight, -1f, 1f);
                    aggregate.AmbientIntegrityBias = math.clamp(ambientIntegrity / ambientWeight, -1f, 1f);
                    aggregate.AmbientIntensity = math.saturate(ambientWeight);
                }
                else
                {
                    aggregate.AmbientLawBias = 0f;
                    aggregate.AmbientGoodBias = 0f;
                    aggregate.AmbientIntegrityBias = 0f;
                    aggregate.AmbientIntensity = 0f;
                }

                aggregate.ArenaOpportunityRate = arenaOpportunity;
                aggregate.OrbitalOpportunityRate = orbitalOpportunity;
                aggregate.TempleOpportunityRate = templeOpportunity;
                aggregate.PrizePurseRate = purseRate;
                aggregate.LootYieldRate = lootRate;
                aggregate.SalvageRightsRate = salvageRate;
                aggregate.ReputationYieldRate = reputationRate;
                aggregate.EducationRate = schoolRate;
                aggregate.UniversityRate = universityRate;
                aggregate.TrainingRate = trainingRate;
                aggregate.WisdomRate = wisdomRate;
                aggregate.ResearchRate = researchRate;
                aggregate.IdeologyPressure = ideologyRate;

                if (aggregate.AmbientIntensity > 1e-5f)
                {
                    var ambientVector = new float3(
                        aggregate.AmbientLawBias,
                        aggregate.AmbientGoodBias,
                        aggregate.AmbientIntegrityBias);
                    var ambientMagnitude = math.length(ambientVector);
                    if (ambientMagnitude > 1e-5f)
                    {
                        var ambientDirection = ambientVector / ambientMagnitude;
                        var ambientStrength = math.saturate(aggregate.AmbientIntensity * (0.6f + 0.4f * (1f - aggregate.Overcrowding)));
                        var driftAppliedToCrew = false;

                        if (_crewLookup.HasBuffer(entity))
                        {
                            var crew = _crewLookup[entity];
                            for (var i = 0; i < crew.Length; i++)
                            {
                                var crewEntity = crew[i].CrewEntity;
                                if (crewEntity == Entity.Null || !_alignmentLookup.HasComponent(crewEntity))
                                {
                                    continue;
                                }

                                var crewAlignment = _alignmentLookup[crewEntity];
                                _alignmentLookup[crewEntity] = ApplyAmbientAlignmentDrift(
                                    crewAlignment,
                                    ambientDirection,
                                    ambientStrength,
                                    1f);
                                driftAppliedToCrew = true;
                            }
                        }

                        if (!driftAppliedToCrew && _alignmentLookup.HasComponent(entity))
                        {
                            var shipAlignment = _alignmentLookup[entity];
                            _alignmentLookup[entity] = ApplyAmbientAlignmentDrift(
                                shipAlignment,
                                ambientDirection,
                                ambientStrength,
                                math.max(1f, currentCrew * 0.35f));
                        }
                    }
                }

                var entertainment = math.saturate((float)needs.Entertainment - (float)needs.EntertainmentDecay);
                var comfort = math.saturate((float)needs.Comfort - (float)needs.ComfortDecay);
                var social = math.saturate((float)needs.Social - (float)needs.SocialDecay);
                var nourishment = math.saturate((float)needs.Nourishment - (float)needs.NourishmentDecay);

                entertainment = math.saturate(entertainment + aggregate.EntertainmentRate / crewScale);
                comfort = math.saturate(comfort + aggregate.ComfortRate / crewScale);
                social = math.saturate(social + aggregate.SocialRate / crewScale);
                nourishment = math.saturate(nourishment + aggregate.NourishmentRate / crewScale);

                if (_supplyLookup.HasComponent(entity) && aggregate.NourishmentRate > 1e-5f)
                {
                    var supply = _supplyLookup[entity];
                    if (supply.ProvisionsCapacity > 0f)
                    {
                        var provisionRecovery = aggregate.NourishmentRate * math.max(1f, crewScale) * 0.5f;
                        supply.Provisions = math.min(supply.ProvisionsCapacity, supply.Provisions + provisionRecovery);
                        _supplyLookup[entity] = supply;
                    }
                }

                needs.Entertainment = (half)entertainment;
                needs.Comfort = (half)comfort;
                needs.Social = (half)social;
                needs.Nourishment = (half)nourishment;
                needs.LastUpdateTick = currentTick;
                needRef.ValueRW = needs;

                var schoolWindow = 0f;
                var universityWindow = 0f;
                var trainingWindow = 0f;
                ResolveEducationScheduleWeights(
                    currentTick,
                    fixedDeltaTime,
                    in educationPolicy,
                    out schoolWindow,
                    out universityWindow,
                    out trainingWindow);

                var educationEnabled = educationPolicy.Enabled != 0;
                if (!educationEnabled)
                {
                    schoolWindow = 0f;
                    universityWindow = 0f;
                    trainingWindow = 0f;
                }

                var activeLearners = math.max(1f, currentCrew * math.max(0.05f, (float)educationPolicy.EnrollmentRatio));
                var facultyQuality = ResolveFacultyQuality(in educationPolicy);
                var standards = math.saturate((float)educationPolicy.Standards01);
                var learningOutput =
                    (aggregate.EducationRate * schoolWindow + aggregate.UniversityRate * universityWindow) *
                    facultyQuality * standards / activeLearners;
                var trainingOutput =
                    aggregate.TrainingRate * trainingWindow *
                    (0.45f + 0.55f * facultyQuality) / activeLearners;
                var wisdomOutput =
                    aggregate.WisdomRate * (schoolWindow * 0.55f + universityWindow * 0.45f) *
                    facultyQuality / activeLearners;
                var researchOutput =
                    aggregate.ResearchRate * (0.3f + 0.7f * universityWindow) *
                    facultyQuality * math.max(0f, educationPolicy.ResearchScale);
                var ideologyPressure =
                    aggregate.IdeologyPressure *
                    (1f - math.saturate((float)educationPolicy.IdeologyResistance01)) *
                    (0.4f + 0.6f * universityWindow);

                var educationMorale =
                    (learningOutput + trainingOutput) * 2.2f +
                    researchOutput * 0.75f -
                    ideologyPressure * 0.35f -
                    aggregate.Overcrowding * 0.12f;
                educationMorale = math.clamp(educationMorale, -0.35f, 0.45f);
                UpsertMoraleModifier(ref moraleModifiers, MoraleModifierSource.Education, educationMorale, currentTick, 0u);

                if (educationEnabled && (learningOutput > 1e-5f || trainingOutput > 1e-5f || researchOutput > 1e-5f))
                {
                    ApplyEducationSkillProgress(
                        entity,
                        currentTick,
                        learningOutput,
                        trainingOutput,
                        researchOutput,
                        in educationPolicy,
                        hasSkillLog,
                        skillLog);

                    ApplyCrewWisdomGain(
                        entity,
                        wisdomOutput,
                        in educationPolicy,
                        ref _crewLookup,
                        ref _wisdomLookup);
                }

                if (_educationProgressLookup.HasComponent(entity))
                {
                    educationProgress.LastLearningOutput = learningOutput;
                    educationProgress.LastTrainingOutput = trainingOutput;
                    educationProgress.LastResearchOutput = researchOutput;
                    educationProgress.LastIdeologyPressure = ideologyPressure;
                    educationProgress.LifetimeResearch += researchOutput;
                    educationProgress.LastUpdateTick = currentTick;
                    _educationProgressLookup[entity] = educationProgress;
                }

                aggregate.IdeologyPressure = ideologyPressure;

                var needScore = (entertainment + comfort + social + nourishment) * 0.25f;
                var leisureStrength = (needScore - 0.5f) * 0.55f +
                                      (aggregate.PreferenceFit - 0.5f) * 0.25f -
                                      aggregate.Overcrowding * 0.65f;
                leisureStrength = math.clamp(leisureStrength, -0.9f, 0.6f);

                UpsertMoraleModifier(ref moraleModifiers, MoraleModifierSource.Leisure, leisureStrength, currentTick, 0u);

                var espionagePressure = aggregate.EspionageRisk * (1f - (float)security.CounterIntelLevel);
                var poisonPressure = aggregate.PoisonRisk * (1f - (float)security.FoodSafetyLevel);
                var assassinPressure = aggregate.AssassinationRisk * (1f - (float)security.InternalSecurityLevel);
                var briberyPressure = aggregate.BriberyRisk * (1f - (float)security.BriberyBudget);
                var facultySubversionPressure =
                    topFacultySubversionRisk *
                    (1f - math.saturate((float)educationPolicy.BriberyOversight01));

                var incidentChance = math.saturate(
                    espionagePressure * (0.3f + chaos * 0.2f) +
                    poisonPressure * 0.3f +
                    assassinPressure * (0.2f + corruption * 0.2f) +
                    briberyPressure * (0.15f + chaos * 0.25f) +
                    facultySubversionPressure * (0.1f + corruption * 0.2f));

                var canTriggerIncident = incidents.Length == 0 || currentTick - incidents[incidents.Length - 1].Tick >= 30u;
                if (incidentChance > 1e-5f && canTriggerIncident)
                {
                    var seed = math.hash(new uint4(
                        (uint)(entity.Index + 1),
                        currentTick + 17u,
                        0xA341316Cu,
                        0xC8013EA4u));
                    if (seed == 0u)
                    {
                        seed = 1u;
                    }

                    var random = new Unity.Mathematics.Random(seed);
                    if (incidentChance >= 0.999f || random.NextFloat() < incidentChance)
                    {
                        var incidentType = ResolveIncidentType(
                            espionagePressure,
                            poisonPressure,
                            assassinPressure,
                            briberyPressure,
                            facultySubversionPressure,
                            random.NextFloat());
                        var incidentModule = ResolveIncidentSourceModule(
                            incidentType,
                            topEspionageModule,
                            topPoisonModule,
                            topAssassinModule,
                            topBriberyModule,
                            topFacultySubversionModule);

                        if (incidents.Length >= 32)
                        {
                            incidents.RemoveAt(0);
                        }

                        var severity = math.clamp(0.05f + incidentChance * 0.35f, 0.05f, 0.5f);
                        incidents.Add(new LeisureIncidentEvent
                        {
                            Type = incidentType,
                            Severity = severity,
                            SourceModule = incidentModule,
                            Tick = currentTick
                        });

                        UpsertMoraleModifier(ref moraleModifiers, MoraleModifierSource.Espionage, -severity, currentTick, 120u);

                        if (_suspicionLookup.HasComponent(entity))
                        {
                            var suspicion = _suspicionLookup[entity];
                            suspicion.Value = (half)math.saturate((float)suspicion.Value + severity * 0.25f);
                            _suspicionLookup[entity] = suspicion;
                        }
                    }
                }

                var totalOpportunity = arenaOpportunity + orbitalOpportunity + templeOpportunity;
                var opportunityChance = math.saturate(
                    totalOpportunity * (0.55f + chaos * 0.2f) +
                    aggregate.PreferenceFit * 0.2f -
                    aggregate.Overcrowding * 0.35f);

                var canTriggerOpportunity = opportunities.Length == 0 || currentTick - opportunities[opportunities.Length - 1].Tick >= 20u;
                if (opportunityChance > 1e-5f && canTriggerOpportunity)
                {
                    var seed = math.hash(new uint4(
                        (uint)(entity.Index + 11),
                        currentTick + 113u,
                        0x8DA6B343u,
                        0xD8163841u));
                    if (seed == 0u)
                    {
                        seed = 1u;
                    }

                    var random = new Unity.Mathematics.Random(seed);
                    if (opportunityChance >= 0.999f || random.NextFloat() < opportunityChance)
                    {
                        var opportunityType = ResolveOpportunityType(
                            arenaOpportunity,
                            orbitalOpportunity,
                            templeOpportunity,
                            random.NextFloat());
                        var sourceModule = ResolveOpportunitySourceModule(
                            opportunityType,
                            topArenaModule,
                            topOrbitalModule,
                            topTempleModule);

                        var payoutScale = math.clamp(0.4f + totalOpportunity * 0.75f, 0.1f, 1.8f);
                        var purse = math.max(0f, purseRate) * payoutScale;
                        var loot = math.max(0f, lootRate) * payoutScale;
                        var salvage = math.max(0f, salvageRate) * payoutScale;
                        var reputation = math.max(0f, reputationRate) * payoutScale;

                        if (opportunityType == LeisureOpportunityType.TempleRite)
                        {
                            purse *= 0.1f;
                            loot = 0f;
                            salvage = 0f;
                            reputation = math.max(reputation, 0.02f + templeOpportunity * 0.6f);
                        }

                        if (opportunities.Length >= 32)
                        {
                            opportunities.RemoveAt(0);
                        }

                        opportunities.Add(new LeisureOpportunityEvent
                        {
                            Type = opportunityType,
                            PrizePurse = purse,
                            LootYield = loot,
                            SalvageRights = salvage,
                            ReputationGain = reputation,
                            SourceModule = sourceModule,
                            Tick = currentTick
                        });
                    }
                }

                aggregateRef.ValueRW = aggregate;
            }
        }

        private static LeisureFacilityLimb CreateHabitatFallback(float functionCapacity)
        {
            var housing = math.max(2f, functionCapacity);
            return new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.CrewQuarters,
                HousingCapacity = housing,
                EntertainmentRate = 0.005f,
                ComfortRate = 0.012f,
                SocialRate = 0.008f,
                NourishmentRate = 0.004f,
                EspionageRisk = 0.01f,
                PoisonRisk = 0.005f,
                AssassinationRisk = 0.008f,
                Illicitness = 0f,
                BriberyPressure = 0f
            };
        }

        private static void ApplyFacilityContribution(
            ref LeisureFacilityAggregate aggregate,
            in LeisureFacilityLimb facility,
            float integrity,
            in LeisurePreferenceProfile profile,
            ref float preferenceWeighted,
            ref float preferenceWeightTotal)
        {
            var clampedIntegrity = math.saturate(integrity);
            aggregate.HousingCapacity += math.max(0f, facility.HousingCapacity) * clampedIntegrity;
            aggregate.EntertainmentRate += math.max(0f, facility.EntertainmentRate) * clampedIntegrity;
            aggregate.ComfortRate += math.max(0f, facility.ComfortRate) * clampedIntegrity;
            aggregate.SocialRate += math.max(0f, facility.SocialRate) * clampedIntegrity;
            aggregate.NourishmentRate += math.max(0f, facility.NourishmentRate) * clampedIntegrity;
            aggregate.EspionageRisk += math.max(0f, facility.EspionageRisk) * clampedIntegrity;
            aggregate.PoisonRisk += math.max(0f, facility.PoisonRisk) * clampedIntegrity;
            aggregate.AssassinationRisk += math.max(0f, facility.AssassinationRisk) * clampedIntegrity;
            aggregate.BriberyRisk += math.max(0f, facility.BriberyPressure + facility.Illicitness * 0.35f + facility.FacultyBriberyRisk) * clampedIntegrity;

            var preference = LeisureFacilityUtility.ResolvePreferenceWeight(profile, facility.Type);
            var facilityYield = facility.EntertainmentRate + facility.ComfortRate + facility.SocialRate + facility.NourishmentRate;
            var weight = math.max(0.001f, facilityYield) * clampedIntegrity;
            preferenceWeighted += preference * weight;
            preferenceWeightTotal += weight;
        }

        private static float ResolveFacilityIntegrity(Entity moduleEntity, ref BufferLookup<ModuleLimbState> limbStateLookup)
        {
            if (!limbStateLookup.HasBuffer(moduleEntity))
            {
                return 1f;
            }

            var limbs = limbStateLookup[moduleEntity];
            if (limbs.Length == 0)
            {
                return 1f;
            }

            var sum = 0f;
            for (var i = 0; i < limbs.Length; i++)
            {
                sum += math.saturate(limbs[i].Integrity);
            }

            return sum / limbs.Length;
        }

        private static void UpsertMoraleModifier(
            ref DynamicBuffer<MoraleModifier> buffer,
            MoraleModifierSource source,
            float strength,
            uint currentTick,
            uint durationTicks)
        {
            var clamped = math.clamp(strength, -1f, 1f);
            var found = -1;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Source == source)
                {
                    found = i;
                    break;
                }
            }

            if (math.abs(clamped) <= 0.005f)
            {
                if (found >= 0)
                {
                    buffer.RemoveAt(found);
                }
                return;
            }

            if (found >= 0)
            {
                var existing = buffer[found];
                existing.Strength = (half)clamped;
                existing.RemainingTicks = durationTicks;
                existing.AppliedTick = currentTick;
                buffer[found] = existing;
                return;
            }

            buffer.Add(new MoraleModifier
            {
                Source = source,
                Strength = (half)clamped,
                RemainingTicks = durationTicks,
                AppliedTick = currentTick
            });
        }

        private static float ResolveAxisConviction(
            Entity entity,
            EthicAxisId axisId,
            ref BufferLookup<EthicAxisValue> axisLookup)
        {
            if (!axisLookup.HasBuffer(entity))
            {
                return 0f;
            }

            var axes = axisLookup[entity];
            for (var i = 0; i < axes.Length; i++)
            {
                if (axes[i].Axis != axisId)
                {
                    continue;
                }

                return math.clamp((float)axes[i].Value * 0.5f, -1f, 1f);
            }

            return 0f;
        }

        private static LeisurePreferenceProfile ApplyWarAxisPreferenceBias(
            in LeisurePreferenceProfile profile,
            float chaos,
            float corruption,
            float warAxisConviction)
        {
            var warlike = math.saturate(0.5f * (warAxisConviction + 1f));
            var pacifism = 1f - warlike;

            var adjusted = profile;
            adjusted.Arena = (half)math.saturate((float)profile.Arena + warlike * 0.2f);
            adjusted.OrbitalArena = (half)math.saturate((float)profile.OrbitalArena + warlike * 0.25f);
            adjusted.ThirdBlood = (half)math.saturate((float)profile.ThirdBlood + warlike * (0.1f + 0.2f * chaos));
            adjusted.SanguinisExtremis = (half)math.saturate((float)profile.SanguinisExtremis + warlike * (0.08f + 0.22f * corruption));
            adjusted.Temple = (half)math.saturate((float)profile.Temple + pacifism * 0.14f - warlike * 0.05f);
            adjusted.Training = (half)math.saturate((float)profile.Training + warlike * 0.2f);
            adjusted.School = (half)math.saturate((float)profile.School + pacifism * 0.08f - warlike * 0.04f);
            adjusted.University = (half)math.saturate((float)profile.University + pacifism * 0.1f - warlike * 0.03f);
            return adjusted;
        }

        private static AlignmentTriplet ApplyAmbientAlignmentDrift(
            in AlignmentTriplet alignment,
            float3 ambientDirection,
            float ambientStrength,
            float populationScale)
        {
            var current = alignment.AsFloat3();
            var currentLength = math.length(current);
            var currentDirection = currentLength > 1e-5f ? current / currentLength : ambientDirection;
            var affinity = math.clamp(math.dot(currentDirection, ambientDirection), -1f, 1f);
            var polarity = affinity >= 0f ? 1f : -1f;
            var coupling = 0.35f + 0.65f * math.abs(affinity);
            var step = math.saturate(ambientStrength) * coupling * 0.02f / math.max(1f, populationScale);
            var next = math.clamp(current + ambientDirection * step * polarity, new float3(-1f), new float3(1f));
            return AlignmentTriplet.FromFloats(next.x, next.y, next.z);
        }

        private static void ResolveEducationScheduleWeights(
            uint currentTick,
            float fixedDeltaTime,
            in LeisureEducationPolicy policy,
            out float schoolWindow,
            out float universityWindow,
            out float trainingWindow)
        {
            schoolWindow = 0f;
            universityWindow = 0f;
            trainingWindow = 0f;

            var dayLengthHours = math.max(1f, policy.DayLengthHours);
            var hourOfDay = math.fmod(currentTick * math.max(0.001f, fixedDeltaTime), dayLengthHours);
            if (hourOfDay < 0f)
            {
                hourOfDay += dayLengthHours;
            }

            var offHours = math.saturate((float)policy.OffHoursEfficiency);

            var schoolActive = IsWindowActive(hourOfDay, dayLengthHours, policy.SchoolStartHour, policy.SchoolDurationHours);
            schoolWindow = schoolActive ? 1f : offHours;

            var universityShiftAActive = IsWindowActive(
                hourOfDay,
                dayLengthHours,
                policy.UniversityShiftAStartHour,
                policy.UniversityShiftDurationHours);
            var universityShiftBActive = IsWindowActive(
                hourOfDay,
                dayLengthHours,
                policy.UniversityShiftBStartHour,
                policy.UniversityShiftDurationHours);
            universityWindow = universityShiftAActive || universityShiftBActive ? 1f : offHours;

            var trainingActive = IsWindowActive(hourOfDay, dayLengthHours, policy.TrainingStartHour, policy.TrainingDurationHours);
            trainingWindow = trainingActive ? 1f : offHours;
        }

        private static bool IsWindowActive(
            float hourOfDay,
            float dayLengthHours,
            float startHour,
            float durationHours)
        {
            var duration = math.max(0f, durationHours);
            if (duration <= 1e-5f)
            {
                return false;
            }

            var start = math.fmod(startHour, dayLengthHours);
            if (start < 0f)
            {
                start += dayLengthHours;
            }

            var end = start + duration;
            if (end < dayLengthHours)
            {
                return hourOfDay >= start && hourOfDay < end;
            }

            var wrappedEnd = end - dayLengthHours;
            return hourOfDay >= start || hourOfDay < wrappedEnd;
        }

        private static float ResolveFacultyQuality(in LeisureEducationPolicy policy)
        {
            var tutor = math.saturate((float)policy.TutorQuality01);
            var professor = math.saturate((float)policy.ProfessorQuality01);
            var assistants = math.saturate((float)policy.AssistantCoverage01);
            return math.saturate(0.35f + tutor * 0.25f + professor * 0.3f + assistants * 0.1f);
        }

        private void ApplyEducationSkillProgress(
            Entity entity,
            uint currentTick,
            float learningOutput,
            float trainingOutput,
            float researchOutput,
            in LeisureEducationPolicy policy,
            bool hasSkillLog,
            DynamicBuffer<SkillChangeLogEntry> skillLog)
        {
            if (!_xpLookup.HasComponent(entity) || !_skillsLookup.HasComponent(entity))
            {
                return;
            }

            var xp = _xpLookup[entity];
            var skills = _skillsLookup[entity];
            var gainScale = math.max(0f, policy.SkillGainScale);

            var studyMagnitude = (learningOutput + researchOutput * 0.3f) * gainScale;
            var drillMagnitude = trainingOutput * gainScale;

            if (studyMagnitude > 1e-5f)
            {
                var explorationDelta = Space4XSkillUtility.ComputeDeltaXp(SkillDomain.Exploration, studyMagnitude);
                var haulingDelta = Space4XSkillUtility.ComputeDeltaXp(SkillDomain.Hauling, studyMagnitude * 0.45f);
                xp.ExplorationXp += explorationDelta;
                xp.HaulingXp += haulingDelta;
                if (hasSkillLog && skillLog.IsCreated)
                {
                    skillLog.Add(new SkillChangeLogEntry
                    {
                        Tick = currentTick,
                        TargetEntity = entity,
                        Domain = SkillDomain.Exploration,
                        DeltaXp = explorationDelta,
                        NewSkill = skills.ExplorationSkill
                    });
                    skillLog.Add(new SkillChangeLogEntry
                    {
                        Tick = currentTick,
                        TargetEntity = entity,
                        Domain = SkillDomain.Hauling,
                        DeltaXp = haulingDelta,
                        NewSkill = skills.HaulingSkill
                    });
                }
            }

            if (drillMagnitude > 1e-5f)
            {
                var combatDelta = Space4XSkillUtility.ComputeDeltaXp(SkillDomain.Combat, drillMagnitude);
                var repairDelta = Space4XSkillUtility.ComputeDeltaXp(SkillDomain.Repair, drillMagnitude * 0.5f);
                xp.CombatXp += combatDelta;
                xp.RepairXp += repairDelta;
                if (hasSkillLog && skillLog.IsCreated)
                {
                    skillLog.Add(new SkillChangeLogEntry
                    {
                        Tick = currentTick,
                        TargetEntity = entity,
                        Domain = SkillDomain.Combat,
                        DeltaXp = combatDelta,
                        NewSkill = skills.CombatSkill
                    });
                    skillLog.Add(new SkillChangeLogEntry
                    {
                        Tick = currentTick,
                        TargetEntity = entity,
                        Domain = SkillDomain.Repair,
                        DeltaXp = repairDelta,
                        NewSkill = skills.RepairSkill
                    });
                }
            }

            xp.LastProcessedTick = currentTick;
            _xpLookup[entity] = xp;

            skills.MiningSkill = Space4XSkillUtility.XpToSkill(xp.MiningXp);
            skills.HaulingSkill = Space4XSkillUtility.XpToSkill(xp.HaulingXp);
            skills.CombatSkill = Space4XSkillUtility.XpToSkill(xp.CombatXp);
            skills.RepairSkill = Space4XSkillUtility.XpToSkill(xp.RepairXp);
            skills.ExplorationSkill = Space4XSkillUtility.XpToSkill(xp.ExplorationXp);
            _skillsLookup[entity] = skills;
        }

        private static void ApplyCrewWisdomGain(
            Entity owner,
            float wisdomOutput,
            in LeisureEducationPolicy policy,
            ref BufferLookup<PDCrewMember> crewLookup,
            ref ComponentLookup<PureDOTS.Runtime.Stats.WisdomStat> wisdomLookup)
        {
            if (wisdomOutput <= 1e-5f || !crewLookup.HasBuffer(owner))
            {
                return;
            }

            var crew = crewLookup[owner];
            if (crew.Length == 0)
            {
                return;
            }

            var perCrewGain = wisdomOutput * math.max(0f, policy.WisdomGainScale) * 100f / crew.Length;
            if (perCrewGain <= 1e-5f)
            {
                return;
            }

            for (var i = 0; i < crew.Length; i++)
            {
                var crewEntity = crew[i].CrewEntity;
                if (crewEntity == Entity.Null || !wisdomLookup.HasComponent(crewEntity))
                {
                    continue;
                }

                var wisdom = wisdomLookup[crewEntity];
                wisdom.Wisdom = math.clamp(wisdom.Wisdom + perCrewGain, 0f, 100f);
                wisdomLookup[crewEntity] = wisdom;
            }
        }

        private static LeisureOpportunityType ResolveOpportunityType(
            float arena,
            float orbital,
            float temple,
            float roll)
        {
            var total = math.max(1e-5f, arena + orbital + temple);
            var cursor = arena / total;
            if (roll <= cursor)
            {
                return LeisureOpportunityType.ArenaBout;
            }

            cursor += orbital / total;
            if (roll <= cursor)
            {
                return LeisureOpportunityType.OrbitalWargame;
            }

            return LeisureOpportunityType.TempleRite;
        }

        private static Entity ResolveOpportunitySourceModule(
            LeisureOpportunityType type,
            Entity arenaModule,
            Entity orbitalModule,
            Entity templeModule)
        {
            return type switch
            {
                LeisureOpportunityType.ArenaBout => arenaModule,
                LeisureOpportunityType.OrbitalWargame => orbitalModule,
                LeisureOpportunityType.TempleRite => templeModule,
                _ => Entity.Null
            };
        }

        private static LeisureIncidentType ResolveIncidentType(
            float espionage,
            float poison,
            float assassin,
            float bribery,
            float facultySubversion,
            float roll)
        {
            var total = math.max(1e-5f, espionage + poison + assassin + bribery + facultySubversion);
            var cursor = espionage / total;
            if (roll <= cursor)
            {
                return LeisureIncidentType.SpyRecruitment;
            }

            cursor += poison / total;
            if (roll <= cursor)
            {
                return LeisureIncidentType.PoisonedSupply;
            }

            cursor += assassin / total;
            if (roll <= cursor)
            {
                return LeisureIncidentType.SleeperAssassin;
            }

            cursor += bribery / total;
            if (roll <= cursor)
            {
                return LeisureIncidentType.BriberyDemand;
            }

            return LeisureIncidentType.FacultySubversion;
        }

        private static Entity ResolveIncidentSourceModule(
            LeisureIncidentType type,
            Entity espionageModule,
            Entity poisonModule,
            Entity assassinModule,
            Entity briberyModule,
            Entity facultySubversionModule)
        {
            return type switch
            {
                LeisureIncidentType.SpyRecruitment => espionageModule,
                LeisureIncidentType.PoisonedSupply => poisonModule,
                LeisureIncidentType.SleeperAssassin => assassinModule,
                LeisureIncidentType.BriberyDemand => briberyModule,
                LeisureIncidentType.FacultySubversion => facultySubversionModule,
                _ => Entity.Null
            };
        }
    }
}
