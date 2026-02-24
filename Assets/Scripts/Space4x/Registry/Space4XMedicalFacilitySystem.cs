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
    /// Aggregates medical facilities and applies treatment/research output.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XLeisureNeedSystem))]
    public partial struct Space4XMedicalFacilitySystem : ISystem
    {
        private BufferLookup<PDCarrierModuleSlot> _moduleSlotsLookup;
        private ComponentLookup<PDShipModule> _shipModuleLookup;
        private ComponentLookup<MedicalFacilityLimb> _facilityLookup;
        private ComponentLookup<ModuleFunctionData> _moduleFunctionLookup;
        private BufferLookup<ModuleLimbState> _limbStateLookup;
        private BufferLookup<PDCrewMember> _crewLookup;
        private BufferLookup<Condition> _conditionLookup;
        private ComponentLookup<ModuleHealth> _moduleHealthLookup;
        private ComponentLookup<MedicalShiftPolicy> _shiftPolicyLookup;
        private ComponentLookup<MedicalStaffingPolicy> _staffingPolicyLookup;
        private ComponentLookup<MedicalStaffingAggregate> _staffingAggregateLookup;
        private ComponentLookup<MedicalCarePolicy> _carePolicyLookup;
        private ComponentLookup<MedicalResearchState> _researchLookup;
        private BufferLookup<MedicalResearchUnlock> _unlockLookup;
        private ComponentLookup<PureDOTS.Runtime.Technology.KnowledgePool> _knowledgeLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<MedicalFacilityAggregate>();
            _moduleSlotsLookup = state.GetBufferLookup<PDCarrierModuleSlot>(true);
            _shipModuleLookup = state.GetComponentLookup<PDShipModule>(true);
            _facilityLookup = state.GetComponentLookup<MedicalFacilityLimb>(true);
            _moduleFunctionLookup = state.GetComponentLookup<ModuleFunctionData>(true);
            _limbStateLookup = state.GetBufferLookup<ModuleLimbState>(true);
            _crewLookup = state.GetBufferLookup<PDCrewMember>(true);
            _conditionLookup = state.GetBufferLookup<Condition>(false);
            _moduleHealthLookup = state.GetComponentLookup<ModuleHealth>(false);
            _shiftPolicyLookup = state.GetComponentLookup<MedicalShiftPolicy>(true);
            _staffingPolicyLookup = state.GetComponentLookup<MedicalStaffingPolicy>(true);
            _staffingAggregateLookup = state.GetComponentLookup<MedicalStaffingAggregate>(false);
            _carePolicyLookup = state.GetComponentLookup<MedicalCarePolicy>(true);
            _researchLookup = state.GetComponentLookup<MedicalResearchState>(false);
            _unlockLookup = state.GetBufferLookup<MedicalResearchUnlock>(false);
            _knowledgeLookup = state.GetComponentLookup<PureDOTS.Runtime.Technology.KnowledgePool>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) &&
                rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _moduleSlotsLookup.Update(ref state);
            _shipModuleLookup.Update(ref state);
            _facilityLookup.Update(ref state);
            _moduleFunctionLookup.Update(ref state);
            _limbStateLookup.Update(ref state);
            _crewLookup.Update(ref state);
            _conditionLookup.Update(ref state);
            _moduleHealthLookup.Update(ref state);
            _shiftPolicyLookup.Update(ref state);
            _staffingPolicyLookup.Update(ref state);
            _staffingAggregateLookup.Update(ref state);
            _carePolicyLookup.Update(ref state);
            _researchLookup.Update(ref state);
            _unlockLookup.Update(ref state);
            _knowledgeLookup.Update(ref state);

            var staffMap = new NativeParallelHashMap<Entity, MedicalStaffingAccumulator>(32, state.WorldUpdateAllocator);
            foreach (var (role, assignment) in SystemAPI.Query<RefRO<MedicalStaffRole>, RefRO<MedicalStaffAssignment>>())
            {
                if (assignment.ValueRO.IsActive == 0 || assignment.ValueRO.FacilityEntity == Entity.Null)
                {
                    continue;
                }

                if (!staffMap.TryGetValue(assignment.ValueRO.FacilityEntity, out var acc))
                {
                    acc = default;
                }

                acc.Add(role.ValueRO, assignment.ValueRO.ShiftIndex);
                staffMap[assignment.ValueRO.FacilityEntity] = acc;
            }

            var catalog = Space4XMedicalResearchCatalog.LoadOrFallback();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (aggregateRef, entity) in SystemAPI.Query<RefRW<MedicalFacilityAggregate>>().WithEntityAccess())
            {
                var shiftPolicy = _shiftPolicyLookup.HasComponent(entity)
                    ? _shiftPolicyLookup[entity]
                    : MedicalShiftPolicy.Default;
                var staffingPolicy = _staffingPolicyLookup.HasComponent(entity)
                    ? _staffingPolicyLookup[entity]
                    : MedicalStaffingPolicy.Default;
                var carePolicy = _carePolicyLookup.HasComponent(entity)
                    ? _carePolicyLookup[entity]
                    : MedicalCarePolicy.Default;

                var aggregate = ResolveFacilityAggregate(entity);
                aggregateRef.ValueRW = aggregate;

                ResolveShiftWindows(
                    time.Tick,
                    time.FixedDeltaTime,
                    shiftPolicy,
                    out var clinicWindow,
                    out var surgeryWindow,
                    out var researchWindow);

                var staffing = ResolveStaffing(
                    entity,
                    staffingPolicy,
                    staffMap,
                    aggregate,
                    clinicWindow,
                    surgeryWindow,
                    researchWindow,
                    out var clinicCoverage,
                    out var surgeryCoverage,
                    out var recoveryCoverage,
                    out var researchCoverage);
                if (_staffingAggregateLookup.HasComponent(entity))
                {
                    _staffingAggregateLookup[entity] = staffing;
                }
                else
                {
                    ecb.AddComponent(entity, staffing);
                }

                var skillMean = math.saturate((float)staffing.SkillMean);
                var staffQuality = math.saturate(0.35f + 0.65f * skillMean);

                var treatmentOutput = aggregate.TreatmentRate * staffQuality * clinicCoverage;
                var surgeryOutput = aggregate.SurgeryRate * staffQuality * surgeryCoverage;
                var recoveryOutput = aggregate.RecoveryRate * staffQuality * recoveryCoverage;
                var researchOutput = aggregate.ResearchRate * staffQuality * researchCoverage;

                var deltaTime = time.FixedDeltaTime;
                if (treatmentOutput > 0f || surgeryOutput > 0f)
                {
                    ApplyConditionCare(entity, treatmentOutput, surgeryOutput, carePolicy, deltaTime);
                }

                if (recoveryOutput > 0f)
                {
                    ApplyModuleRecovery(entity, recoveryOutput, carePolicy, deltaTime);
                }

                var researchState = _researchLookup.HasComponent(entity)
                    ? _researchLookup[entity]
                    : default;

                var knowledgeGain = researchOutput * carePolicy.ResearchScalar * deltaTime;
                researchState.KnowledgeProgress += knowledgeGain;
                researchState.LastOutput = researchOutput;
                researchState.LifetimeKnowledge += knowledgeGain;
                researchState.LastUpdateTick = time.Tick;
                researchState.Tier = (byte)math.clamp((int)(researchState.KnowledgeProgress / 50f), 0, 255);

                if (_researchLookup.HasComponent(entity))
                {
                    _researchLookup[entity] = researchState;
                }
                else
                {
                    ecb.AddComponent(entity, researchState);
                }

                if (_knowledgeLookup.HasComponent(entity) && knowledgeGain > 0f)
                {
                    var knowledge = _knowledgeLookup[entity];
                    knowledge.AccumulatedKnowledge += knowledgeGain;
                    _knowledgeLookup[entity] = knowledge;
                }

                ApplyResearchUnlocks(entity, researchState, catalog, time.Tick, ref ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private MedicalFacilityAggregate ResolveFacilityAggregate(Entity entity)
        {
            var aggregate = default(MedicalFacilityAggregate);
            if (!_moduleSlotsLookup.HasBuffer(entity))
            {
                return aggregate;
            }

            var slots = _moduleSlotsLookup[entity];
            for (var i = 0; i < slots.Length; i++)
            {
                var module = slots[i].InstalledModule;
                if (module == Entity.Null || !_shipModuleLookup.HasComponent(module))
                {
                    continue;
                }

                var hasFacility = false;
                MedicalFacilityLimb facility = default;

                if (_facilityLookup.HasComponent(module))
                {
                    facility = _facilityLookup[module];
                    hasFacility = true;
                }
                else if (_moduleFunctionLookup.HasComponent(module))
                {
                    var function = _moduleFunctionLookup[module];
                    if (function.Function == ModuleFunction.RepairFacility)
                    {
                        facility = CreateMedicalFallback(function.Capacity);
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

                ApplyFacilityContribution(ref aggregate, in facility, integrity);
            }

            return aggregate;
        }

        private static void ApplyFacilityContribution(
            ref MedicalFacilityAggregate aggregate,
            in MedicalFacilityLimb facility,
            float integrity)
        {
            var clampedIntegrity = math.saturate(integrity);
            aggregate.TreatmentRate += math.max(0f, facility.TreatmentRate) * clampedIntegrity;
            aggregate.SurgeryRate += math.max(0f, facility.SurgeryRate) * clampedIntegrity;
            aggregate.RecoveryRate += math.max(0f, facility.RecoveryRate) * clampedIntegrity;
            aggregate.ResearchRate += math.max(0f, facility.ResearchRate) * clampedIntegrity;
            aggregate.AugmentRate += math.max(0f, facility.AugmentRate) * clampedIntegrity;
            aggregate.AugmentQualityBonus += math.max(0f, facility.AugmentQualityBonus) * clampedIntegrity;
            aggregate.InfectionRisk += math.max(0f, facility.InfectionRisk) * clampedIntegrity;
            aggregate.MalpracticeRisk += math.max(0f, facility.MalpracticeRisk) * clampedIntegrity;
            aggregate.Sterility += math.max(0f, facility.Sterility) * clampedIntegrity;
        }

        private MedicalStaffingAggregate ResolveStaffing(
            Entity entity,
            MedicalStaffingPolicy policy,
            NativeParallelHashMap<Entity, MedicalStaffingAccumulator> staffMap,
            in MedicalFacilityAggregate aggregate,
            float clinicWindow,
            float surgeryWindow,
            float researchWindow,
            out float clinicCoverage,
            out float surgeryCoverage,
            out float recoveryCoverage,
            out float researchCoverage)
        {
            var staffing = default(MedicalStaffingAggregate);
            clinicCoverage = 0f;
            surgeryCoverage = 0f;
            recoveryCoverage = 0f;
            researchCoverage = 0f;

            if (staffMap.TryGetValue(entity, out var acc))
            {
                staffing.Doctors = (byte)math.min(255, acc.Doctors);
                staffing.Surgeons = (byte)math.min(255, acc.Surgeons);
                staffing.Assistants = (byte)math.min(255, acc.Assistants);
                staffing.Researchers = (byte)math.min(255, acc.Researchers);

                var total = math.max(1, acc.TotalCount);
                staffing.SkillMean = (half)math.saturate(acc.SkillSum / total);

                var careRequired = math.max(1, policy.DoctorsRequired + policy.SurgeonsRequired + policy.AssistantsRequired);
                var researchRequired = math.max(1, policy.ResearchersRequired);

                var clinicCareCount = acc.ClinicDoctors + acc.ClinicSurgeons + acc.ClinicAssistants;
                var surgeryCareCount = acc.SurgeryDoctors + acc.SurgerySurgeons + acc.SurgeryAssistants;
                var flexCareCount = acc.FlexDoctors + acc.FlexSurgeons + acc.FlexAssistants;

                var clinicDemand = math.max(0f, aggregate.TreatmentRate) * clinicWindow;
                var surgeryDemand = math.max(0f, aggregate.SurgeryRate) * surgeryWindow;
                var preferSurgery = surgeryDemand > clinicDemand && surgeryWindow > 1e-4f;
                if (clinicWindow <= 1e-4f && surgeryWindow > 1e-4f)
                {
                    preferSurgery = true;
                }

                if (preferSurgery)
                {
                    surgeryCareCount += flexCareCount;
                }
                else
                {
                    clinicCareCount += flexCareCount;
                }

                var recoveryCareCount = clinicCareCount + surgeryCareCount;

                var researchCount = acc.ResearchResearchers + acc.FlexResearchers;

                clinicCoverage = math.saturate(clinicCareCount / (float)careRequired) * clinicWindow;
                surgeryCoverage = math.saturate(surgeryCareCount / (float)careRequired) * surgeryWindow;
                recoveryCoverage = math.saturate(recoveryCareCount / (float)careRequired) * math.max(clinicWindow, surgeryWindow);
                researchCoverage = math.saturate(researchCount / (float)researchRequired) * researchWindow;

                staffing.Coverage01 = (half)recoveryCoverage;
                staffing.ResearchCoverage01 = (half)researchCoverage;
                staffing.ClinicCoverage01 = (half)clinicCoverage;
                staffing.SurgeryCoverage01 = (half)surgeryCoverage;
                staffing.RecoveryCoverage01 = (half)recoveryCoverage;
            }
            else
            {
                staffing.Coverage01 = (half)0f;
                staffing.ResearchCoverage01 = (half)0f;
            }

            return staffing;
        }

        private void ApplyConditionCare(
            Entity facilityEntity,
            float treatmentOutput,
            float surgeryOutput,
            MedicalCarePolicy policy,
            float deltaTime)
        {
            if (!_crewLookup.HasBuffer(facilityEntity))
            {
                return;
            }

            var healRate = (treatmentOutput + surgeryOutput * policy.SurgeryBonusScalar) * policy.ConditionHealScalar;
            if (healRate <= 1e-6f)
            {
                return;
            }

            var crew = _crewLookup[facilityEntity];
            for (var i = 0; i < crew.Length; i++)
            {
                var crewEntity = crew[i].CrewEntity;
                if (crewEntity == Entity.Null || !_conditionLookup.HasBuffer(crewEntity))
                {
                    continue;
                }

                var conditions = _conditionLookup[crewEntity];
                for (var c = conditions.Length - 1; c >= 0; c--)
                {
                    var condition = conditions[c];
                    condition.Severity = math.max(0f, condition.Severity - healRate * deltaTime);
                    if (condition.Severity <= 0.01f)
                    {
                        conditions.RemoveAt(c);
                        continue;
                    }

                    conditions[c] = condition;
                }
            }
        }

        private void ApplyModuleRecovery(
            Entity facilityEntity,
            float recoveryOutput,
            MedicalCarePolicy policy,
            float deltaTime)
        {
            if (!_moduleSlotsLookup.HasBuffer(facilityEntity))
            {
                return;
            }

            var slots = _moduleSlotsLookup[facilityEntity];
            if (slots.Length == 0)
            {
                return;
            }

            var repairPerModule = recoveryOutput * policy.ModuleRepairScalar * deltaTime / math.max(1, slots.Length);
            if (repairPerModule <= 1e-6f)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var module = slots[i].InstalledModule;
                if (module == Entity.Null || !_moduleHealthLookup.HasComponent(module))
                {
                    continue;
                }

                var health = _moduleHealthLookup[module];
                var maxRepair = math.max(0.01f, health.MaxFieldRepairHealth);
                if (health.CurrentHealth >= maxRepair)
                {
                    continue;
                }

                health.CurrentHealth = math.min(maxRepair, health.CurrentHealth + repairPerModule);
                health.Failed = (byte)(health.CurrentHealth <= 0f ? 1 : 0);
                _moduleHealthLookup[module] = health;
            }
        }

        private void ApplyResearchUnlocks(
            Entity entity,
            in MedicalResearchState state,
            Space4XMedicalResearchCatalog catalog,
            uint tick,
            ref EntityCommandBuffer ecb)
        {
            if (catalog == null || catalog.Unlocks == null || catalog.Unlocks.Length == 0)
            {
                return;
            }

            var buffer = _unlockLookup.HasBuffer(entity)
                ? _unlockLookup[entity]
                : ecb.AddBuffer<MedicalResearchUnlock>(entity);

            for (var i = 0; i < catalog.Unlocks.Length; i++)
            {
                var unlock = catalog.Unlocks[i];
                if (state.KnowledgeProgress < unlock.RequiredKnowledge)
                {
                    continue;
                }

                var unlockId = new FixedString64Bytes(unlock.Id ?? string.Empty);
                if (unlockId.IsEmpty)
                {
                    continue;
                }

                var found = false;
                for (var u = 0; u < buffer.Length; u++)
                {
                    if (buffer[u].UnlockId.Equals(unlockId))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue;
                }

                buffer.Add(new MedicalResearchUnlock
                {
                    UnlockId = unlockId,
                    Kind = unlock.Kind,
                    TargetId = new FixedString64Bytes(unlock.TargetId ?? string.Empty),
                    RequiredKnowledge = unlock.RequiredKnowledge,
                    UnlockedTick = tick
                });
            }
        }

        private static void ResolveShiftWindows(
            uint currentTick,
            float fixedDeltaTime,
            in MedicalShiftPolicy policy,
            out float clinicWindow,
            out float surgeryWindow,
            out float researchWindow)
        {
            clinicWindow = 0f;
            surgeryWindow = 0f;
            researchWindow = 0f;

            var dayLengthHours = math.max(1f, policy.DayLengthHours);
            var hourOfDay = math.fmod(currentTick * math.max(0.001f, fixedDeltaTime), dayLengthHours);
            if (hourOfDay < 0f)
            {
                hourOfDay += dayLengthHours;
            }

            var offHours = math.saturate((float)policy.OffHoursEfficiency);
            clinicWindow = IsWindowActive(hourOfDay, dayLengthHours, policy.ClinicStartHour, policy.ClinicDurationHours)
                ? 1f
                : offHours;
            surgeryWindow = IsWindowActive(hourOfDay, dayLengthHours, policy.SurgeryStartHour, policy.SurgeryDurationHours)
                ? 1f
                : offHours;
            researchWindow = IsWindowActive(hourOfDay, dayLengthHours, policy.ResearchStartHour, policy.ResearchDurationHours)
                ? 1f
                : offHours;
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

        private static MedicalFacilityLimb CreateMedicalFallback(float functionCapacity)
        {
            var capacity = math.max(0f, functionCapacity);
            return new MedicalFacilityLimb
            {
                Type = MedicalFacilityType.Clinic,
                TreatmentRate = 0.02f + capacity * 0.004f,
                SurgeryRate = 0.01f + capacity * 0.002f,
                RecoveryRate = 0.03f + capacity * 0.004f,
                ResearchRate = 0.01f,
                AugmentRate = 0.005f,
                AugmentQualityBonus = 0.02f,
                InfectionRisk = 0.02f,
                MalpracticeRisk = 0.01f,
                Sterility = 0.7f
            };
        }

        private struct MedicalStaffingAccumulator
        {
            public int Doctors;
            public int Surgeons;
            public int Assistants;
            public int Researchers;
            public int ClinicDoctors;
            public int ClinicSurgeons;
            public int ClinicAssistants;
            public int ClinicResearchers;
            public int SurgeryDoctors;
            public int SurgerySurgeons;
            public int SurgeryAssistants;
            public int SurgeryResearchers;
            public int ResearchDoctors;
            public int ResearchSurgeons;
            public int ResearchAssistants;
            public int ResearchResearchers;
            public int FlexDoctors;
            public int FlexSurgeons;
            public int FlexAssistants;
            public int FlexResearchers;
            public float SkillSum;
            public int TotalCount;

            public void Add(in MedicalStaffRole role, byte shiftIndex)
            {
                var skill = math.saturate((float)role.Skill01);
                SkillSum += skill;
                TotalCount++;

                ref var target = ref ResolveShiftBucket(ref this, shiftIndex, role.Role);

                switch (role.Role)
                {
                    case MedicalStaffRoleType.Doctor:
                        Doctors++;
                        target++;
                        break;
                    case MedicalStaffRoleType.Surgeon:
                        Surgeons++;
                        target++;
                        break;
                    case MedicalStaffRoleType.Assistant:
                        Assistants++;
                        target++;
                        break;
                    case MedicalStaffRoleType.Researcher:
                        Researchers++;
                        target++;
                        break;
                }
            }

            private static ref int ResolveShiftBucket(
                ref MedicalStaffingAccumulator acc,
                byte shiftIndex,
                MedicalStaffRoleType roleType)
            {
                switch (shiftIndex)
                {
                    case 0:
                        return ref ResolveClinic(ref acc, roleType);
                    case 1:
                        return ref ResolveSurgery(ref acc, roleType);
                    case 2:
                        return ref ResolveResearch(ref acc, roleType);
                    default:
                        return ref ResolveFlex(ref acc, roleType);
                }
            }

            private static ref int ResolveClinic(ref MedicalStaffingAccumulator acc, MedicalStaffRoleType roleType)
            {
                switch (roleType)
                {
                    case MedicalStaffRoleType.Doctor:
                        return ref acc.ClinicDoctors;
                    case MedicalStaffRoleType.Surgeon:
                        return ref acc.ClinicSurgeons;
                    case MedicalStaffRoleType.Assistant:
                        return ref acc.ClinicAssistants;
                    case MedicalStaffRoleType.Researcher:
                        return ref acc.ClinicResearchers;
                    default:
                        return ref acc.ClinicAssistants;
                }
            }

            private static ref int ResolveSurgery(ref MedicalStaffingAccumulator acc, MedicalStaffRoleType roleType)
            {
                switch (roleType)
                {
                    case MedicalStaffRoleType.Doctor:
                        return ref acc.SurgeryDoctors;
                    case MedicalStaffRoleType.Surgeon:
                        return ref acc.SurgerySurgeons;
                    case MedicalStaffRoleType.Assistant:
                        return ref acc.SurgeryAssistants;
                    case MedicalStaffRoleType.Researcher:
                        return ref acc.SurgeryResearchers;
                    default:
                        return ref acc.SurgeryAssistants;
                }
            }

            private static ref int ResolveResearch(ref MedicalStaffingAccumulator acc, MedicalStaffRoleType roleType)
            {
                switch (roleType)
                {
                    case MedicalStaffRoleType.Doctor:
                        return ref acc.ResearchDoctors;
                    case MedicalStaffRoleType.Surgeon:
                        return ref acc.ResearchSurgeons;
                    case MedicalStaffRoleType.Assistant:
                        return ref acc.ResearchAssistants;
                    case MedicalStaffRoleType.Researcher:
                        return ref acc.ResearchResearchers;
                    default:
                        return ref acc.ResearchAssistants;
                }
            }

            private static ref int ResolveFlex(ref MedicalStaffingAccumulator acc, MedicalStaffRoleType roleType)
            {
                switch (roleType)
                {
                    case MedicalStaffRoleType.Doctor:
                        return ref acc.FlexDoctors;
                    case MedicalStaffRoleType.Surgeon:
                        return ref acc.FlexSurgeons;
                    case MedicalStaffRoleType.Assistant:
                        return ref acc.FlexAssistants;
                    case MedicalStaffRoleType.Researcher:
                        return ref acc.FlexResearchers;
                    default:
                        return ref acc.FlexAssistants;
                }
            }
        }
    }
}
