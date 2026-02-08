using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Executes business jobs on a fixed tick cadence (currently 10 ticks).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XBusinessJobSystem : ISystem
    {
        private const uint JobTickInterval = 10u;
        private ComponentLookup<Carrier> _carrierLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private ComponentLookup<EmpireMembership> _empireLookup;
        private BufferLookup<Space4XContactStanding> _contactLookup;
        private BufferLookup<FactionRelationEntry> _relationLookup;
        private BufferLookup<RacePresence> _raceLookup;
        private BufferLookup<CulturePresence> _cultureLookup;
        private ComponentLookup<TechLevel> _techLookup;
        private ComponentLookup<Space4XResearchUnlocks> _unlockLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XJobCatalogSingleton>();
            state.RequireForUpdate<Space4XBusinessCatalogSingleton>();

            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _empireLookup = state.GetComponentLookup<EmpireMembership>(true);
            _contactLookup = state.GetBufferLookup<Space4XContactStanding>(true);
            _relationLookup = state.GetBufferLookup<FactionRelationEntry>(true);
            _raceLookup = state.GetBufferLookup<RacePresence>(true);
            _cultureLookup = state.GetBufferLookup<CulturePresence>(true);
            _techLookup = state.GetComponentLookup<TechLevel>(true);
            _unlockLookup = state.GetComponentLookup<Space4XResearchUnlocks>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            var tickTime = SystemAPI.GetSingleton<TickTimeState>();
            if (tickTime.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = tickTime.Tick;
            if (JobTickInterval == 0u || (tick % JobTickInterval) != 0u)
            {
                return;
            }

            var jobCatalog = SystemAPI.GetSingleton<Space4XJobCatalogSingleton>().Catalog;
            var businessCatalog = SystemAPI.GetSingleton<Space4XBusinessCatalogSingleton>().Catalog;
            if (!jobCatalog.IsCreated || !businessCatalog.IsCreated)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _empireLookup.Update(ref state);
            _contactLookup.Update(ref state);
            _relationLookup.Update(ref state);
            _raceLookup.Update(ref state);
            _cultureLookup.Update(ref state);
            _techLookup.Update(ref state);
            _unlockLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var facilityMap = new NativeParallelMultiHashMap<Entity, FacilityBusinessClass>(64, Allocator.Temp);

            foreach (var link in SystemAPI.Query<RefRO<ColonyFacilityLink>>())
            {
                if (!IsValidEntity(link.ValueRO.Colony))
                {
                    continue;
                }

                facilityMap.Add(link.ValueRO.Colony, link.ValueRO.FacilityClass);
            }

            foreach (var (businessState, storage, businessEntity) in SystemAPI.Query<RefRW<Space4XBusinessState>, DynamicBuffer<ResourceStorage>>()
                         .WithEntityAccess())
            {
                if (!IsValidEntity(businessEntity))
                {
                    continue;
                }

                var storageBuffer = storage;
                if (businessState.ValueRO.NextJobTick > tick)
                {
                    continue;
                }

                if (!TryResolveBusinessDefinition(businessState.ValueRO.Kind, ref businessCatalog.Value, out var businessIndex))
                {
                    continue;
                }

                ref var businessDef = ref businessCatalog.Value.Businesses[businessIndex];
                var issuerFaction = ResolveBusinessIssuerFaction(businessState.ValueRO.Colony, out var issuerFactionId);
                var techTier = ResolveTechTier(businessState.ValueRO.Colony);

                var bestScore = float.MinValue;
                var bestJobIndex = -1;
                var found = false;

                for (int j = 0; j < businessDef.JobIds.Length; j++)
                {
                    var jobId = businessDef.JobIds[j];
                    if (!TryResolveJob(jobId, ref jobCatalog.Value, out var jobIndex))
                    {
                        continue;
                    }

                    ref var jobDef = ref jobCatalog.Value.Jobs[jobIndex];
                    if (jobDef.RequiredFacility != FacilityBusinessClass.None &&
                        jobDef.RequiredFacility != businessState.ValueRO.FacilityClass &&
                        !ColonyHasFacility(businessState.ValueRO.Colony, jobDef.RequiredFacility, facilityMap))
                    {
                        continue;
                    }

                    if (jobDef.MinTechTier > 0 && techTier < jobDef.MinTechTier)
                    {
                        continue;
                    }

                    if (jobDef.StandingGate > 0f && issuerFaction != Entity.Null)
                    {
                        if (!Space4XStandingUtility.PassesStandingGate(
                                businessState.ValueRO.Owner,
                                issuerFaction,
                                issuerFactionId,
                                jobDef.StandingGate,
                                in _factionLookup,
                                in _empireLookup,
                                in _carrierLookup,
                                in _affiliationLookup,
                                in _contactLookup,
                                in _relationLookup,
                                in _raceLookup,
                                in _cultureLookup))
                        {
                            continue;
                        }
                    }

                    if (!HasInputs(storageBuffer, ref jobDef))
                    {
                        continue;
                    }

                    var score = ComputeScore(ref jobDef);
                    if (!found || score > bestScore)
                    {
                        bestScore = score;
                        bestJobIndex = jobIndex;
                        found = true;
                    }
                }

                if (!found)
                {
                    businessState.ValueRW.NextJobTick = tick + JobTickInterval;
                    continue;
                }

                ref var bestJob = ref jobCatalog.Value.Jobs[bestJobIndex];
                ConsumeInputs(storageBuffer, ref bestJob);
                ProduceOutputs(storageBuffer, ref bestJob);

                var duration = bestJob.DurationTicks > 0 ? bestJob.DurationTicks : (byte)JobTickInterval;
                businessState.ValueRW.ActiveJobId = bestJob.Id;
                businessState.ValueRW.LastJobTick = tick;
                businessState.ValueRW.NextJobTick = tick + duration;

                if (businessState.ValueRO.Owner != Entity.Null &&
                    state.EntityManager.HasComponent<Space4XBusinessOwner>(businessState.ValueRO.Owner))
                {
                    var assignment = new Space4XJobRoleAssignment
                    {
                        JobId = bestJob.Id,
                        Business = businessEntity,
                        AssignedTick = tick,
                        NextEvaluateTick = tick + duration
                    };

                    if (state.EntityManager.HasComponent<Space4XJobRoleAssignment>(businessState.ValueRO.Owner))
                    {
                        state.EntityManager.SetComponentData(businessState.ValueRO.Owner, assignment);
                    }
                    else
                    {
                        ecb.AddComponent(businessState.ValueRO.Owner, assignment);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            facilityMap.Dispose();
            ecb.Dispose();
        }

        private Entity ResolveBusinessIssuerFaction(Entity colony, out ushort factionId)
        {
            factionId = 0;
            if (!IsValidEntity(colony) || !_affiliationLookup.HasBuffer(colony))
            {
                return Entity.Null;
            }

            var affiliations = _affiliationLookup[colony];
            for (int i = 0; i < affiliations.Length; i++)
            {
                var tag = affiliations[i];
                if (tag.Type != AffiliationType.Faction || !IsValidEntity(tag.Target))
                {
                    continue;
                }

                if (_factionLookup.HasComponent(tag.Target))
                {
                    factionId = _factionLookup[tag.Target].FactionId;
                    return tag.Target;
                }
            }

            return Entity.Null;
        }

        private byte ResolveTechTier(Entity colony)
        {
            if (IsValidEntity(colony))
            {
                if (_techLookup.HasComponent(colony))
                {
                    var tech = _techLookup[colony];
                    var tier = math.max((int)tech.MiningTech,
                        math.max((int)tech.CombatTech, math.max((int)tech.HaulingTech, (int)tech.ProcessingTech)));
                    return (byte)tier;
                }

                if (_unlockLookup.HasComponent(colony))
                {
                    var unlocks = _unlockLookup[colony];
                    var tier = math.max((int)unlocks.ProductionTier,
                        math.max((int)unlocks.ProcessingTier, (int)unlocks.ExtractionTier));
                    return (byte)tier;
                }
            }

            return 0;
        }

        private static bool ColonyHasFacility(Entity colony, FacilityBusinessClass facilityClass, NativeParallelMultiHashMap<Entity, FacilityBusinessClass> facilityMap)
        {
            if (colony == Entity.Null)
            {
                return false;
            }

            if (!facilityMap.TryGetFirstValue(colony, out var value, out var iterator))
            {
                return false;
            }

            do
            {
                if (value == facilityClass)
                {
                    return true;
                }
            }
            while (facilityMap.TryGetNextValue(out value, ref iterator));

            return false;
        }

        private static bool TryResolveBusinessDefinition(Space4XBusinessKind kind, ref Space4XBusinessCatalogBlob catalog, out int index)
        {
            for (int i = 0; i < catalog.Businesses.Length; i++)
            {
                ref var business = ref catalog.Businesses[i];
                if (business.Kind == kind)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static bool TryResolveJob(in FixedString64Bytes jobId, ref Space4XJobCatalogBlob catalog, out int index)
        {
            for (int i = 0; i < catalog.Jobs.Length; i++)
            {
                ref var job = ref catalog.Jobs[i];
                if (job.Id.Equals(jobId))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static float ComputeScore(ref Space4XJobDefinition job)
        {
            float inputs = 0f;
            float outputs = 0f;

            for (int i = 0; i < job.Inputs.Length; i++)
            {
                inputs += math.max(0f, job.Inputs[i].Units);
            }

            for (int i = 0; i < job.Outputs.Length; i++)
            {
                outputs += math.max(0f, job.Outputs[i].Units);
            }

            var score = outputs - inputs;
            if (outputs <= 0f)
            {
                score -= 0.1f;
            }

            return score;
        }

        private static bool HasInputs(DynamicBuffer<ResourceStorage> storage, ref Space4XJobDefinition job)
        {
            for (int i = 0; i < job.Inputs.Length; i++)
            {
                var input = job.Inputs[i];
                var index = FindStorageIndex(storage, input.Type);
                if (index < 0)
                {
                    return false;
                }

                if (storage[index].Amount + 0.0001f < input.Units)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ConsumeInputs(DynamicBuffer<ResourceStorage> storage, ref Space4XJobDefinition job)
        {
            for (int i = 0; i < job.Inputs.Length; i++)
            {
                var input = job.Inputs[i];
                var index = FindStorageIndex(storage, input.Type);
                if (index < 0)
                {
                    continue;
                }

                var entry = storage[index];
                entry.Amount = math.max(0f, entry.Amount - input.Units);
                storage[index] = entry;
            }
        }

        private static void ProduceOutputs(DynamicBuffer<ResourceStorage> storage, ref Space4XJobDefinition job)
        {
            for (int i = 0; i < job.Outputs.Length; i++)
            {
                var output = job.Outputs[i];
                var index = FindStorageIndex(storage, output.Type);
                if (index < 0)
                {
                    var slot = ResourceStorage.Create(output.Type, 5000f);
                    storage.Add(slot);
                    index = storage.Length - 1;
                }

                var entry = storage[index];
                entry.AddAmount(math.max(0f, output.Units));
                storage[index] = entry;
            }
        }

        private static int FindStorageIndex(DynamicBuffer<ResourceStorage> storage, ResourceType type)
        {
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }
    }
}
