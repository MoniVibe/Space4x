using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Space4x.Scenario;
using Space4X.Headless;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.ModulesQuality
{
    internal static class Space4XModulePipelineMicro
    {
        public static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("space4x_module_pipeline_micro");
        public static readonly FixedString64Bytes RefineryCarrierId = new FixedString64Bytes("modules-refinery");
        public static readonly FixedString64Bytes ForgeCarrierId = new FixedString64Bytes("modules-forge");
        public static readonly FixedString64Bytes AssemblerCarrierId = new FixedString64Bytes("modules-assembler");
        public static readonly FixedString64Bytes ShipyardCarrierId = new FixedString64Bytes("modules-shipyard");

        public static uint MixDigest(uint digest, uint a, uint b, uint c)
            => math.hash(new uint4(digest ^ 0x9E3779B9u, a + 0x85EBCA6Bu, b + 0xC2B2AE35u, c + 0x27D4EB2Fu));

        public static uint QuantizeQuality(float value)
            => (uint)math.clamp((int)math.round(math.saturate(value) * 10000f), 0, 10000);
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XModulePipelineBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info) ||
                !info.ScenarioId.Equals(Space4XModulePipelineMicro.ScenarioId))
            {
                return;
            }

            if (SystemAPI.TryGetSingleton(out Space4XModulePipelineRuntime existing) && existing.Initialized != 0)
            {
                return;
            }

            var em = state.EntityManager;
            var refinery = ResolveCarrierEntity(Space4XModulePipelineMicro.RefineryCarrierId);
            var forge = ResolveCarrierEntity(Space4XModulePipelineMicro.ForgeCarrierId);
            var assembler = ResolveCarrierEntity(Space4XModulePipelineMicro.AssemblerCarrierId);
            var shipyard = ResolveCarrierEntity(Space4XModulePipelineMicro.ShipyardCarrierId);

            EnsureFacility(ref em, ref refinery, Space4XFacilityType.MaterialRefinery, 0.68f, 0.58f, 2);
            EnsureFacility(ref em, ref forge, Space4XFacilityType.PartForge, 0.74f, 0.56f, 2);
            EnsureFacility(ref em, ref assembler, Space4XFacilityType.ModuleAssembler, 0.78f, 0.60f, 2);
            EnsureFacility(ref em, ref shipyard, Space4XFacilityType.Shipyard, 0.82f, 0.62f, 2);

            EnsureBuffer<Space4XMaterialBatch>(ref em, refinery);
            EnsureBuffer<Space4XMaterialBatch>(ref em, forge);
            EnsureBuffer<Space4XPartBatch>(ref em, forge);
            EnsureBuffer<Space4XPartBatch>(ref em, assembler);
            EnsureBuffer<Space4XModuleItem>(ref em, assembler);
            EnsureBuffer<Space4XModuleItem>(ref em, shipyard);
            EnsureBuffer<Space4XShipModuleIntegration>(ref em, shipyard);

            SpawnWorkersIfMissing(ref em, refinery, forge, assembler, shipyard);

            var researchEntity = EnsureSingletonEntity<Space4XModulePipelineResearchBias>(ref em);
            em.SetComponentData(researchEntity, new Space4XModulePipelineResearchBias
            {
                Direction = Space4XResearchDirection.Throughput
            });

            var runtimeEntity = EnsureSingletonEntity<Space4XModulePipelineRuntime>(ref em);
            var seed = info.Seed == 0u ? 4101u : info.Seed;
            em.SetComponentData(runtimeEntity, new Space4XModulePipelineRuntime
            {
                Refinery = refinery,
                Forge = forge,
                Assembler = assembler,
                Shipyard = shipyard,
                PartsProduced = 0u,
                ModulesAssembled = 0u,
                InstallsCompleted = 0u,
                PartQualitySum = 0f,
                ModuleQualitySum = 0f,
                InstallQualitySum = 0f,
                Digest = Space4XModulePipelineMicro.MixDigest(1u, seed, 4u, 0u),
                NextBatchId = 1u,
                NextPartId = 1u,
                NextModuleId = 1u,
                Initialized = 1,
                MetricsEmitted = 0
            });

            Debug.Log("[ModulesPipeline] bootstrap complete scenario=space4x_module_pipeline_micro");
        }

        private static Entity ResolveCarrierEntity(in FixedString64Bytes carrierId)
        {
            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (carrier.ValueRO.CarrierId.Equals(carrierId))
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private static void EnsureFacility(ref EntityManager em, ref Entity facilityEntity, Space4XFacilityType type, float processCapability, float qualityTarget, byte requiredWorkers)
        {
            if (facilityEntity == Entity.Null)
            {
                facilityEntity = em.CreateEntity();
            }

            if (!em.HasComponent<Space4XModulePipelineTag>(facilityEntity))
            {
                em.AddComponent<Space4XModulePipelineTag>(facilityEntity);
            }

            if (em.HasComponent<Space4XFacility>(facilityEntity))
            {
                em.SetComponentData(facilityEntity, new Space4XFacility { Type = type, QualityTarget = qualityTarget });
            }
            else
            {
                em.AddComponentData(facilityEntity, new Space4XFacility { Type = type, QualityTarget = qualityTarget });
            }

            if (em.HasComponent<Space4XFacilityCapability>(facilityEntity))
            {
                em.SetComponentData(facilityEntity, new Space4XFacilityCapability { ProcessCapability = processCapability });
            }
            else
            {
                em.AddComponentData(facilityEntity, new Space4XFacilityCapability { ProcessCapability = processCapability });
            }

            if (em.HasComponent<Space4XWorkforceSlot>(facilityEntity))
            {
                em.SetComponentData(facilityEntity, new Space4XWorkforceSlot { RequiredWorkers = requiredWorkers });
            }
            else
            {
                em.AddComponentData(facilityEntity, new Space4XWorkforceSlot { RequiredWorkers = requiredWorkers });
            }

            if (!em.HasComponent<Space4XAssignedWorkers>(facilityEntity))
            {
                em.AddComponentData(facilityEntity, default(Space4XAssignedWorkers));
            }

            if (!em.HasComponent<Space4XFacilityProcessState>(facilityEntity))
            {
                em.AddComponentData(facilityEntity, new Space4XFacilityProcessState { Progress = 0f });
            }
        }

        private static void EnsureBuffer<T>(ref EntityManager em, Entity entity) where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(entity))
            {
                em.AddBuffer<T>(entity);
            }
        }

        private static Entity EnsureSingletonEntity<T>(ref EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            return em.CreateEntity(typeof(T));
        }

        private static void SpawnWorkersIfMissing(ref EntityManager em, Entity refinery, Entity forge, Entity assembler, Entity shipyard)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XModulePipelineWorkerTag>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return;
            }

            SpawnWorker(ref em, refinery, 0.63f, 0);
            SpawnWorker(ref em, refinery, 0.57f, 0);
            SpawnWorker(ref em, forge, 0.68f, 1);
            SpawnWorker(ref em, forge, 0.64f, 1);
            SpawnWorker(ref em, assembler, 0.72f, 2);
            SpawnWorker(ref em, assembler, 0.70f, 2);
            SpawnWorker(ref em, shipyard, 0.76f, 2);
            SpawnWorker(ref em, shipyard, 0.74f, 2);
        }

        private static void SpawnWorker(ref EntityManager em, Entity facility, float skill, byte tier)
        {
            var worker = em.CreateEntity(
                typeof(Space4XModulePipelineTag),
                typeof(Space4XModulePipelineWorkerTag),
                typeof(Space4XWorkerSkill),
                typeof(Space4XWorkerAssignment));

            em.SetComponentData(worker, new Space4XWorkerSkill { Skill = math.saturate(skill), Tier = tier });
            em.SetComponentData(worker, new Space4XWorkerAssignment { Facility = facility });
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XModulePipelineBootstrapSystem))]
    public partial struct Space4XModulePipelineWorkforceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XModulePipelineRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info) ||
                !info.ScenarioId.Equals(Space4XModulePipelineMicro.ScenarioId) ||
                !SystemAPI.TryGetSingleton(out Space4XModulePipelineRuntime runtime) ||
                runtime.Initialized == 0)
            {
                return;
            }

            foreach (var (assigned, slot, entity) in SystemAPI.Query<RefRW<Space4XAssignedWorkers>, RefRO<Space4XWorkforceSlot>>()
                         .WithAll<Space4XModulePipelineTag>()
                         .WithEntityAccess())
            {
                var workerCount = 0;
                var tier0 = 0;
                var tier1 = 0;
                var tier2 = 0;
                var skillSum = 0f;

                foreach (var (assignment, skill) in SystemAPI.Query<RefRO<Space4XWorkerAssignment>, RefRO<Space4XWorkerSkill>>()
                             .WithAll<Space4XModulePipelineWorkerTag>())
                {
                    if (assignment.ValueRO.Facility != entity)
                    {
                        continue;
                    }

                    workerCount++;
                    skillSum += skill.ValueRO.Skill;
                    switch (skill.ValueRO.Tier)
                    {
                        case 0: tier0++; break;
                        case 1: tier1++; break;
                        default: tier2++; break;
                    }
                }

                assigned.ValueRW = new Space4XAssignedWorkers
                {
                    WorkerCount = (byte)math.clamp(workerCount, 0, 255),
                    Tier0Count = (byte)math.clamp(tier0, 0, 255),
                    Tier1Count = (byte)math.clamp(tier1, 0, 255),
                    Tier2Count = (byte)math.clamp(tier2, 0, 255),
                    AverageSkill = workerCount > 0 ? skillSum / workerCount : 0f
                };

                if (workerCount == 0 && slot.ValueRO.RequiredWorkers > 0)
                {
                    assigned.ValueRW.AverageSkill = 0.25f;
                }
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XModulePipelineWorkforceSystem))]
    public partial struct Space4XModulePipelinePolicySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XModulePipelineRuntime>();
            state.RequireForUpdate<Space4XModulePipelineResearchBias>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info) ||
                !info.ScenarioId.Equals(Space4XModulePipelineMicro.ScenarioId) ||
                !SystemAPI.TryGetSingleton(out Space4XModulePipelineRuntime runtime) ||
                runtime.Initialized == 0)
            {
                return;
            }

            var em = state.EntityManager;
            if (!em.Exists(runtime.Forge) || !em.Exists(runtime.Assembler) || !em.Exists(runtime.Shipyard))
            {
                return;
            }

            var forgeMaterialStock = SumMaterialUnits(em.GetBuffer<Space4XMaterialBatch>(runtime.Forge));
            var assemblerPartStock = SumPartUnits(em.GetBuffer<Space4XPartBatch>(runtime.Assembler));
            var shipyardModuleStock = math.max(0f, em.GetBuffer<Space4XModuleItem>(runtime.Shipyard).Length);
            var averageInstallQuality = runtime.InstallsCompleted > 0u ? runtime.InstallQualitySum / math.max(1f, runtime.InstallsCompleted) : 0f;

            var direction = Space4XResearchDirection.Balanced;
            if (runtime.InstallsCompleted == 0u || shipyardModuleStock < 1f)
            {
                direction = Space4XResearchDirection.Throughput;
            }
            else if (averageInstallQuality < 0.74f)
            {
                direction = Space4XResearchDirection.Quality;
            }

            var biasEntity = SystemAPI.GetSingletonEntity<Space4XModulePipelineResearchBias>();
            em.SetComponentData(biasEntity, new Space4XModulePipelineResearchBias { Direction = direction });

            var demandHigh = runtime.InstallsCompleted == 0u || shipyardModuleStock < 1f;
            foreach (var (facility, workers, slot) in SystemAPI.Query<RefRW<Space4XFacility>, RefRO<Space4XAssignedWorkers>, RefRO<Space4XWorkforceSlot>>()
                         .WithAll<Space4XModulePipelineTag>())
            {
                var directionBias = direction switch
                {
                    Space4XResearchDirection.Throughput => -0.11f,
                    Space4XResearchDirection.Quality => 0.12f,
                    _ => 0f
                };

                var scarcity = facility.ValueRO.Type switch
                {
                    Space4XFacilityType.MaterialRefinery => forgeMaterialStock < 2f ? 1f : 0f,
                    Space4XFacilityType.PartForge => forgeMaterialStock < 1f ? 1f : 0f,
                    Space4XFacilityType.ModuleAssembler => assemblerPartStock < 2f ? 1f : 0f,
                    Space4XFacilityType.Shipyard => shipyardModuleStock < 1f ? 1f : 0f,
                    _ => 0f
                };

                var staffingRatio = workers.ValueRO.WorkerCount / math.max(1f, slot.ValueRO.RequiredWorkers);
                var staffingPenalty = staffingRatio < 1f ? (1f - staffingRatio) * 0.08f : 0f;
                var demandPenalty = demandHigh ? 0.10f : -0.04f;

                var target = 0.62f + directionBias - demandPenalty - scarcity * 0.08f - staffingPenalty;
                facility.ValueRW.QualityTarget = math.clamp(target, 0.35f, 0.95f);
            }
        }

        private static float SumMaterialUnits(DynamicBuffer<Space4XMaterialBatch> buffer)
        {
            var total = 0f;
            for (var i = 0; i < buffer.Length; i++)
            {
                total += math.max(0f, buffer[i].Quantity);
            }

            return total;
        }

        private static float SumPartUnits(DynamicBuffer<Space4XPartBatch> buffer)
        {
            var total = 0f;
            for (var i = 0; i < buffer.Length; i++)
            {
                total += math.max(0f, buffer[i].Quantity);
            }

            return total;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XModulePipelinePolicySystem))]
    public partial struct Space4XModulePipelineRuntimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Space4XModulePipelineRuntime>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XModulePipelineResearchBias>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info) ||
                !info.ScenarioId.Equals(Space4XModulePipelineMicro.ScenarioId) ||
                !SystemAPI.TryGetSingletonEntity<Space4XModulePipelineRuntime>(out var runtimeEntity))
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var dt = ResolveFixedDelta(time);
            var runtime = state.EntityManager.GetComponentData<Space4XModulePipelineRuntime>(runtimeEntity);
            if (runtime.Initialized == 0 ||
                !TryGetFacilityState(ref state, runtime.Refinery, out var refinery, out var refineryCapability, out var refineryWorkers, out var refinerySlot, out var refineryProcess) ||
                !TryGetFacilityState(ref state, runtime.Forge, out var forge, out var forgeCapability, out var forgeWorkers, out var forgeSlot, out var forgeProcess) ||
                !TryGetFacilityState(ref state, runtime.Assembler, out var assembler, out var assemblerCapability, out var assemblerWorkers, out var assemblerSlot, out var assemblerProcess) ||
                !TryGetFacilityState(ref state, runtime.Shipyard, out var shipyard, out var shipyardCapability, out var shipyardWorkers, out var shipyardSlot, out var shipyardProcess))
            {
                return;
            }

            var em = state.EntityManager;
            var research = SystemAPI.GetSingleton<Space4XModulePipelineResearchBias>();

            var refineryMaterials = em.GetBuffer<Space4XMaterialBatch>(runtime.Refinery);
            var forgeMaterials = em.GetBuffer<Space4XMaterialBatch>(runtime.Forge);
            var forgeParts = em.GetBuffer<Space4XPartBatch>(runtime.Forge);
            var assemblerParts = em.GetBuffer<Space4XPartBatch>(runtime.Assembler);
            var assemblerModules = em.GetBuffer<Space4XModuleItem>(runtime.Assembler);
            var shipyardModules = em.GetBuffer<Space4XModuleItem>(runtime.Shipyard);
            var shipyardIntegrations = em.GetBuffer<Space4XShipModuleIntegration>(runtime.Shipyard);

            ProduceMaterials(ref runtime, ref refineryProcess, refinery, refineryCapability, refineryWorkers, refinerySlot, dt, refineryMaterials);
            TransferMaterials(refineryMaterials, forgeMaterials, 2);

            ProduceParts(ref runtime, ref forgeProcess, forge, forgeCapability, forgeWorkers, forgeSlot, dt, forgeMaterials, forgeParts);
            TransferParts(forgeParts, assemblerParts, 2);

            AssembleModules(ref runtime, ref assemblerProcess, assembler, assemblerCapability, assemblerWorkers, assemblerSlot, dt, research.Direction, assemblerParts, assemblerModules);
            TransferModules(assemblerModules, shipyardModules, 2);

            InstallModules(ref runtime, ref shipyardProcess, shipyard, shipyardCapability, shipyardWorkers, shipyardSlot, dt, shipyardModules, shipyardIntegrations);

            em.SetComponentData(runtime.Refinery, refineryProcess);
            em.SetComponentData(runtime.Forge, forgeProcess);
            em.SetComponentData(runtime.Assembler, assemblerProcess);
            em.SetComponentData(runtime.Shipyard, shipyardProcess);

            if (time.Tick % 60u == 0u && Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var snapshotBuffer))
            {
                EmitMetrics(ref snapshotBuffer, in runtime);
            }

            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var completionTick = scenario.EndTick;
            if (completionTick == 0u && scenario.DurationSeconds > 0f)
            {
                var fallbackTicks = (uint)math.max(1f, math.ceil(scenario.DurationSeconds / math.max(1e-4f, dt)));
                completionTick = scenario.StartTick + fallbackTicks;
            }

            if (runtime.MetricsEmitted == 0 && completionTick > 0u && time.Tick >= completionTick)
            {
                Space4XOperatorReportUtility.RequestHeadlessAnswersFlush(ref state, time.Tick);
                if (Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
                {
                    EmitMetrics(ref buffer, in runtime);
                }

                runtime.MetricsEmitted = 1;
                Debug.Log($"[ModulesPipeline] COMPLETE tick={time.Tick} digest={runtime.Digest} parts={runtime.PartsProduced} modules={runtime.ModulesAssembled} installs={runtime.InstallsCompleted}");
            }

            em.SetComponentData(runtimeEntity, runtime);
        }

        private static float ResolveFixedDelta(in TimeState time)
        {
            var dt = time.FixedDeltaTime;
            if (dt <= 0f)
            {
                dt = 1f / 60f;
            }

            return math.max(1e-4f, dt);
        }

        private static bool TryGetFacilityState(ref SystemState state, Entity entity, out Space4XFacility facility, out Space4XFacilityCapability capability, out Space4XAssignedWorkers workers, out Space4XWorkforceSlot slot, out Space4XFacilityProcessState process)
        {
            var em = state.EntityManager;
            facility = default;
            capability = default;
            workers = default;
            slot = default;
            process = default;

            if (entity == Entity.Null || !em.Exists(entity))
            {
                return false;
            }

            if (!em.HasComponent<Space4XFacility>(entity) ||
                !em.HasComponent<Space4XFacilityCapability>(entity) ||
                !em.HasComponent<Space4XAssignedWorkers>(entity) ||
                !em.HasComponent<Space4XWorkforceSlot>(entity) ||
                !em.HasComponent<Space4XFacilityProcessState>(entity))
            {
                return false;
            }

            facility = em.GetComponentData<Space4XFacility>(entity);
            capability = em.GetComponentData<Space4XFacilityCapability>(entity);
            workers = em.GetComponentData<Space4XAssignedWorkers>(entity);
            slot = em.GetComponentData<Space4XWorkforceSlot>(entity);
            process = em.GetComponentData<Space4XFacilityProcessState>(entity);
            return true;
        }

        private static void ProduceMaterials(ref Space4XModulePipelineRuntime runtime, ref Space4XFacilityProcessState process, in Space4XFacility facility, in Space4XFacilityCapability capability, in Space4XAssignedWorkers workers, in Space4XWorkforceSlot slot, float dt, DynamicBuffer<Space4XMaterialBatch> output)
        {
            var throughput = ResolveThroughput(in workers, in capability, in slot, facility.QualityTarget, 1.2f);
            process.Progress += throughput * dt;

            var cycles = 0;
            while (process.Progress >= 1f && cycles < 4)
            {
                process.Progress -= 1f;
                var workerSkill = ResolveWorkerSkill(in workers);
                var materialQuality = math.saturate(0.42f + workerSkill * 0.24f + capability.ProcessCapability * 0.22f + facility.QualityTarget * 0.12f);
                var batchId = runtime.NextBatchId++;
                output.Add(new Space4XMaterialBatch { BatchId = batchId, Quantity = 1f, MaterialQuality = materialQuality });
                runtime.Digest = Space4XModulePipelineMicro.MixDigest(runtime.Digest, batchId, Space4XModulePipelineMicro.QuantizeQuality(materialQuality), 11u);
                cycles++;
            }
        }

        private static void ProduceParts(ref Space4XModulePipelineRuntime runtime, ref Space4XFacilityProcessState process, in Space4XFacility facility, in Space4XFacilityCapability capability, in Space4XAssignedWorkers workers, in Space4XWorkforceSlot slot, float dt, DynamicBuffer<Space4XMaterialBatch> input, DynamicBuffer<Space4XPartBatch> output)
        {
            var throughput = ResolveThroughput(in workers, in capability, in slot, facility.QualityTarget, 1.1f);
            process.Progress += throughput * dt;

            var cycles = 0;
            while (process.Progress >= 1f && cycles < 4)
            {
                if (!TryConsumeMaterial(input, out var materialQuality, out var batchId))
                {
                    break;
                }

                process.Progress -= 1f;
                var workerSkill = ResolveWorkerSkill(in workers);
                var partQuality = math.saturate(materialQuality * 0.50f + workerSkill * 0.22f + capability.ProcessCapability * 0.18f + facility.QualityTarget * 0.10f);
                var partId = runtime.NextPartId++;

                output.Add(new Space4XPartBatch { PartId = partId, Quantity = 1f, PartQuality = partQuality });
                runtime.PartsProduced++;
                runtime.PartQualitySum += partQuality;
                runtime.Digest = Space4XModulePipelineMicro.MixDigest(runtime.Digest, batchId, partId, Space4XModulePipelineMicro.QuantizeQuality(partQuality));
                cycles++;
            }
        }

        private static void AssembleModules(ref Space4XModulePipelineRuntime runtime, ref Space4XFacilityProcessState process, in Space4XFacility facility, in Space4XFacilityCapability capability, in Space4XAssignedWorkers workers, in Space4XWorkforceSlot slot, float dt, Space4XResearchDirection direction, DynamicBuffer<Space4XPartBatch> input, DynamicBuffer<Space4XModuleItem> output)
        {
            var throughput = ResolveThroughput(in workers, in capability, in slot, facility.QualityTarget, 1.0f);
            process.Progress += throughput * dt;

            var cycles = 0;
            while (process.Progress >= 1f && cycles < 3)
            {
                if (!TryConsumeParts(input, 2, out var averagePartQuality, out var partDigest))
                {
                    break;
                }

                process.Progress -= 1f;
                var workerSkill = ResolveWorkerSkill(in workers);
                var moduleQuality = math.saturate(averagePartQuality * 0.58f + workerSkill * 0.18f + capability.ProcessCapability * 0.14f + facility.QualityTarget * 0.10f);
                var mark = direction switch
                {
                    Space4XResearchDirection.Throughput => 1u,
                    Space4XResearchDirection.Quality => 3u,
                    _ => 2u
                };
                var moduleId = (runtime.NextModuleId++ << 2) | mark;
                var provenanceDigest = Space4XModulePipelineMicro.MixDigest(partDigest, moduleId, mark, Space4XModulePipelineMicro.QuantizeQuality(moduleQuality));

                output.Add(new Space4XModuleItem { ModuleId = moduleId, ModuleQuality = moduleQuality, ProvenanceDigest = provenanceDigest });
                runtime.ModulesAssembled++;
                runtime.ModuleQualitySum += moduleQuality;
                runtime.Digest = Space4XModulePipelineMicro.MixDigest(runtime.Digest, moduleId, provenanceDigest, Space4XModulePipelineMicro.QuantizeQuality(moduleQuality));
                cycles++;
            }
        }

        private static void InstallModules(ref Space4XModulePipelineRuntime runtime, ref Space4XFacilityProcessState process, in Space4XFacility facility, in Space4XFacilityCapability capability, in Space4XAssignedWorkers workers, in Space4XWorkforceSlot slot, float dt, DynamicBuffer<Space4XModuleItem> input, DynamicBuffer<Space4XShipModuleIntegration> output)
        {
            var throughput = ResolveThroughput(in workers, in capability, in slot, facility.QualityTarget, 0.95f);
            process.Progress += throughput * dt;

            var cycles = 0;
            while (process.Progress >= 1f && cycles < 3)
            {
                if (!TryConsumeModule(input, out var module))
                {
                    break;
                }

                process.Progress -= 1f;
                var workerSkill = ResolveWorkerSkill(in workers);
                var installQuality = math.saturate(module.ModuleQuality * 0.62f + workerSkill * 0.18f + capability.ProcessCapability * 0.14f + facility.QualityTarget * 0.06f);
                output.Add(new Space4XShipModuleIntegration
                {
                    ShipId = 1001u,
                    Slot = (byte)(runtime.InstallsCompleted % 10u),
                    ModuleId = module.ModuleId,
                    InstallQuality = installQuality
                });

                runtime.InstallsCompleted++;
                runtime.InstallQualitySum += installQuality;
                runtime.Digest = Space4XModulePipelineMicro.MixDigest(runtime.Digest, module.ModuleId, module.ProvenanceDigest, Space4XModulePipelineMicro.QuantizeQuality(installQuality));
                cycles++;
            }
        }

        private static float ResolveThroughput(in Space4XAssignedWorkers workers, in Space4XFacilityCapability capability, in Space4XWorkforceSlot slot, float qualityTarget, float baseRate)
        {
            var staffingRatio = workers.WorkerCount / math.max(1f, slot.RequiredWorkers);
            var staffingFactor = math.clamp(0.4f + staffingRatio * 0.6f, 0.25f, 1.2f);
            var workerSkill = math.max(0.25f, workers.AverageSkill);
            var capabilityFactor = math.max(0.35f, capability.ProcessCapability);
            var throughputBias = math.clamp(1.2f - qualityTarget, 0.35f, 1.15f);
            return baseRate * staffingFactor * workerSkill * capabilityFactor * throughputBias;
        }

        private static float ResolveWorkerSkill(in Space4XAssignedWorkers workers)
        {
            return workers.WorkerCount > 0 ? math.saturate(workers.AverageSkill) : 0.35f;
        }

        private static void TransferMaterials(DynamicBuffer<Space4XMaterialBatch> source, DynamicBuffer<Space4XMaterialBatch> destination, int maxUnits)
        {
            for (var moved = 0; moved < maxUnits && source.Length > 0; moved++)
            {
                destination.Add(source[0]);
                source.RemoveAt(0);
            }
        }

        private static void TransferParts(DynamicBuffer<Space4XPartBatch> source, DynamicBuffer<Space4XPartBatch> destination, int maxUnits)
        {
            for (var moved = 0; moved < maxUnits && source.Length > 0; moved++)
            {
                destination.Add(source[0]);
                source.RemoveAt(0);
            }
        }

        private static void TransferModules(DynamicBuffer<Space4XModuleItem> source, DynamicBuffer<Space4XModuleItem> destination, int maxUnits)
        {
            for (var moved = 0; moved < maxUnits && source.Length > 0; moved++)
            {
                destination.Add(source[0]);
                source.RemoveAt(0);
            }
        }

        private static bool TryConsumeMaterial(DynamicBuffer<Space4XMaterialBatch> buffer, out float quality, out uint batchId)
        {
            quality = 0f;
            batchId = 0u;
            if (buffer.Length == 0)
            {
                return false;
            }

            var batch = buffer[0];
            quality = math.saturate(batch.MaterialQuality);
            batchId = batch.BatchId;
            buffer.RemoveAt(0);
            return true;
        }

        private static bool TryConsumeParts(DynamicBuffer<Space4XPartBatch> buffer, int requiredCount, out float averageQuality, out uint partDigest)
        {
            averageQuality = 0f;
            partDigest = 1u;
            if (buffer.Length < requiredCount)
            {
                return false;
            }

            var qualitySum = 0f;
            for (var i = 0; i < requiredCount; i++)
            {
                var part = buffer[0];
                qualitySum += math.saturate(part.PartQuality);
                partDigest = Space4XModulePipelineMicro.MixDigest(partDigest, part.PartId, Space4XModulePipelineMicro.QuantizeQuality(part.PartQuality), (uint)(i + 1));
                buffer.RemoveAt(0);
            }

            averageQuality = qualitySum / math.max(1, requiredCount);
            return true;
        }

        private static bool TryConsumeModule(DynamicBuffer<Space4XModuleItem> buffer, out Space4XModuleItem module)
        {
            module = default;
            if (buffer.Length == 0)
            {
                return false;
            }

            module = buffer[0];
            buffer.RemoveAt(0);
            return true;
        }

        private static void EmitMetrics(ref DynamicBuffer<Space4XOperatorMetric> buffer, in Space4XModulePipelineRuntime runtime)
        {
            var avgPart = runtime.PartsProduced > 0u ? runtime.PartQualitySum / math.max(1f, runtime.PartsProduced) : 0f;
            var avgModule = runtime.ModulesAssembled > 0u ? runtime.ModuleQualitySum / math.max(1f, runtime.ModulesAssembled) : 0f;
            var avgInstall = runtime.InstallsCompleted > 0u ? runtime.InstallQualitySum / math.max(1f, runtime.InstallsCompleted) : 0f;

            AddMetric(ref buffer, "modules.parts_produced", runtime.PartsProduced);
            AddMetric(ref buffer, "modules.modules_assembled", runtime.ModulesAssembled);
            AddMetric(ref buffer, "modules.installs_completed", runtime.InstallsCompleted);
            AddMetric(ref buffer, "modules.avg_part_quality", avgPart);
            AddMetric(ref buffer, "modules.avg_module_quality", avgModule);
            AddMetric(ref buffer, "modules.avg_install_quality", avgInstall);
            AddMetric(ref buffer, "modules.digest", runtime.Digest);
        }

        private static void AddMetric(ref DynamicBuffer<Space4XOperatorMetric> buffer, string key, float value)
        {
            var fixedKey = new FixedString64Bytes(key);
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(fixedKey))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric { Key = fixedKey, Value = value });
        }
    }
}
