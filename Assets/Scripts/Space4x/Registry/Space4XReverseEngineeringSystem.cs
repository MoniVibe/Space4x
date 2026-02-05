using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XReverseEngineeringBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ReverseEngineeringConfig>(out _))
            {
                var configEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(configEntity, ReverseEngineeringConfig.Default);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<ReverseEngineeringState>(entity))
                {
                    ecb.AddComponent(entity, new ReverseEngineeringState
                    {
                        NextTaskId = 1,
                        NextVariantId = 1
                    });
                }

                if (!state.EntityManager.HasBuffer<ReverseEngineeringEvidence>(entity))
                {
                    ecb.AddBuffer<ReverseEngineeringEvidence>(entity);
                }

                if (!state.EntityManager.HasBuffer<ReverseEngineeringTask>(entity))
                {
                    ecb.AddBuffer<ReverseEngineeringTask>(entity);
                }

                if (!state.EntityManager.HasBuffer<ReverseEngineeringBlueprintVariant>(entity))
                {
                    ecb.AddBuffer<ReverseEngineeringBlueprintVariant>(entity);
                }

                if (!state.EntityManager.HasBuffer<ReverseEngineeringBlueprintProgress>(entity))
                {
                    ecb.AddBuffer<ReverseEngineeringBlueprintProgress>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSalvageOperationSystem))]
    [UpdateBefore(typeof(Space4XSalvageTransferSystem))]
    public partial struct Space4XReverseEngineeringEvidenceSystem : ISystem
    {
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<ReverseEngineeringEvidence> _evidenceLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SalvageOperation>();
            state.RequireForUpdate<ReverseEngineeringConfig>();
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _evidenceLookup = state.GetBufferLookup<ReverseEngineeringEvidence>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<ReverseEngineeringConfig>();
            var entityManager = state.EntityManager;
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _evidenceLookup.Update(ref state);

            foreach (var (operation, entity) in SystemAPI.Query<RefRW<SalvageOperation>>().WithEntityAccess())
            {
                if (operation.ValueRO.Phase != SalvagePhase.Complete)
                {
                    continue;
                }

                var target = operation.ValueRO.Target;
                if (target == Entity.Null || !entityManager.Exists(target))
                {
                    continue;
                }

                if (!entityManager.HasComponent<DerelictState>(target))
                {
                    continue;
                }

                var derelict = entityManager.GetComponentData<DerelictState>(target);
                if (derelict.EvidenceExtracted != 0)
                {
                    continue;
                }

                if (!entityManager.HasComponent<SalvageYield>(target))
                {
                    derelict.EvidenceExtracted = 1;
                    entityManager.SetComponentData(target, derelict);
                    continue;
                }

                var yield = entityManager.GetComponentData<SalvageYield>(target);
                var totalEvidence = yield.TechSamples + yield.Artifacts;
                if (totalEvidence <= 0)
                {
                    derelict.EvidenceExtracted = 1;
                    entityManager.SetComponentData(target, derelict);
                    continue;
                }

                if (!TryResolveFaction(operation.ValueRO.Salvager, out var factionEntity, out var factionId))
                {
                    continue;
                }

                if (!_evidenceLookup.HasBuffer(factionEntity))
                {
                    continue;
                }

                var evidenceBuffer = _evidenceLookup[factionEntity];
                var blueprintId = ResolveEvidenceBlueprintId(derelict, isArtifact, seed);
                var existingCount = CountEvidenceForBlueprint(evidenceBuffer, blueprintId);
                var maxToAdd = math.max(0, config.MaxEvidencePerBlueprint - existingCount);
                var toAdd = math.min(totalEvidence, maxToAdd);

                for (int i = 0; i < toAdd; i++)
                {
                    var isArtifact = i < yield.Artifacts;
                    var seed = BuildEvidenceSeed(derelict, factionId, currentTick, (uint)i);
                    var evidence = BuildEvidence(derelict, blueprintId, isArtifact, seed, currentTick);
                    evidenceBuffer.Add(evidence);
                }

                derelict.EvidenceExtracted = 1;
                entityManager.SetComponentData(target, derelict);
            }
        }

        private bool TryResolveFaction(Entity entity, out Entity factionEntity, out ushort factionId)
        {
            if (entity != Entity.Null && _factionLookup.HasComponent(entity))
            {
                factionEntity = entity;
                factionId = _factionLookup[entity].FactionId;
                return true;
            }

            if (entity != Entity.Null && _affiliationLookup.HasBuffer(entity))
            {
                var affiliations = _affiliationLookup[entity];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var target = affiliations[i].Target;
                    if (target != Entity.Null && _factionLookup.HasComponent(target))
                    {
                        factionEntity = target;
                        factionId = _factionLookup[target].FactionId;
                        return true;
                    }
                }
            }

            factionEntity = Entity.Null;
            factionId = 0;
            return false;
        }

        private static int CountEvidenceForBlueprint(DynamicBuffer<ReverseEngineeringEvidence> buffer, ushort blueprintId)
        {
            var count = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].BlueprintId == blueprintId)
                {
                    count++;
                }
            }
            return count;
        }

        private static uint BuildEvidenceSeed(in DerelictState derelict, ushort factionId, uint tick, uint index)
        {
            var hash = math.hash(new uint4(
                (uint)derelict.DerelictionTick + 1u,
                (uint)derelict.OriginalClass + 37u,
                (uint)factionId + 97u,
                tick + index * 13u));
            return hash == 0u ? 1u : hash;
        }

        private static ReverseEngineeringEvidence BuildEvidence(in DerelictState derelict, ushort blueprintId, bool isArtifact, uint seed, uint tick)
        {
            var random = new Unity.Mathematics.Random(seed);
            var fidelity = ResolveBaseFidelity(derelict.Condition, isArtifact);
            var integrity = ResolveBaseIntegrity(derelict.Condition, isArtifact);
            var coverageMin = isArtifact ? 25 : 8;
            var coverageMax = isArtifact ? 85 : 60;

            return new ReverseEngineeringEvidence
            {
                BlueprintId = blueprintId,
                Stage = 0,
                Fidelity = (byte)math.clamp(fidelity + random.NextInt(-6, 7), 5, 100),
                Integrity = (byte)math.clamp(integrity + random.NextInt(-5, 6), 5, 100),
                CoverageEfficiency = (byte)random.NextInt(coverageMin, coverageMax),
                CoverageReliability = (byte)random.NextInt(coverageMin, coverageMax),
                CoverageMass = (byte)random.NextInt(coverageMin, coverageMax),
                CoveragePower = (byte)random.NextInt(coverageMin, coverageMax),
                CoverageSignature = (byte)random.NextInt(coverageMin, coverageMax),
                CoverageDurability = (byte)random.NextInt(coverageMin, coverageMax),
                EvidenceSeed = seed,
                SourceTick = tick
            };
        }

        private static ushort ResolveEvidenceBlueprintId(in DerelictState derelict, bool isArtifact, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed ^ 0x9e3779b9u);
            var roll = random.NextFloat();

            if (isArtifact)
            {
                if (roll < 0.3f) return ReverseEngineeringBlueprintFamily.Weapon;
                if (roll < 0.5f) return ReverseEngineeringBlueprintFamily.Shield;
                if (roll < 0.65f) return ReverseEngineeringBlueprintFamily.Engine;
                if (roll < 0.8f) return ReverseEngineeringBlueprintFamily.Reactor;
                if (roll < 0.9f) return ReverseEngineeringBlueprintFamily.Command;
                return ReverseEngineeringBlueprintFamily.Hull;
            }

            if (derelict.Condition <= DerelictCondition.Damaged && roll < 0.2f)
            {
                return ReverseEngineeringBlueprintFamily.Reactor;
            }

            if (roll < 0.25f) return ReverseEngineeringBlueprintFamily.Hull;
            if (roll < 0.45f) return ReverseEngineeringBlueprintFamily.Engine;
            if (roll < 0.6f) return ReverseEngineeringBlueprintFamily.Shield;
            if (roll < 0.75f) return ReverseEngineeringBlueprintFamily.Weapon;
            if (roll < 0.85f) return ReverseEngineeringBlueprintFamily.Armor;
            if (roll < 0.92f) return ReverseEngineeringBlueprintFamily.Command;
            return ReverseEngineeringBlueprintFamily.Ammo;
        }

        private static int ResolveBaseFidelity(DerelictCondition condition, bool isArtifact)
        {
            var baseValue = condition switch
            {
                DerelictCondition.Pristine => 80,
                DerelictCondition.Damaged => 65,
                DerelictCondition.Ruined => 50,
                DerelictCondition.Stripped => 30,
                DerelictCondition.Decaying => 20,
                _ => 45
            };

            if (isArtifact)
            {
                baseValue += 15;
            }

            return math.clamp(baseValue, 5, 95);
        }

        private static int ResolveBaseIntegrity(DerelictCondition condition, bool isArtifact)
        {
            var baseValue = condition switch
            {
                DerelictCondition.Pristine => 85,
                DerelictCondition.Damaged => 70,
                DerelictCondition.Ruined => 55,
                DerelictCondition.Stripped => 35,
                DerelictCondition.Decaying => 25,
                _ => 50
            };

            if (isArtifact)
            {
                baseValue += 10;
            }

            return math.clamp(baseValue, 5, 95);
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XReverseEngineeringEvidenceSystem))]
    public partial struct Space4XReverseEngineeringProcessingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
            state.RequireForUpdate<ReverseEngineeringConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<ReverseEngineeringConfig>();
            var deltaSeconds = (float)SystemAPI.Time.DeltaTime;

            foreach (var (faction, reverseState, entity) in SystemAPI.Query<RefRO<Space4XFaction>, RefRW<ReverseEngineeringState>>().WithEntityAccess())
            {
                if (!SystemAPI.HasBuffer<ReverseEngineeringEvidence>(entity) ||
                    !SystemAPI.HasBuffer<ReverseEngineeringTask>(entity) ||
                    !SystemAPI.HasBuffer<ReverseEngineeringBlueprintVariant>(entity) ||
                    !SystemAPI.HasBuffer<ReverseEngineeringBlueprintProgress>(entity))
                {
                    continue;
                }

                var evidence = SystemAPI.GetBuffer<ReverseEngineeringEvidence>(entity);
                var tasks = SystemAPI.GetBuffer<ReverseEngineeringTask>(entity);
                var variants = SystemAPI.GetBuffer<ReverseEngineeringBlueprintVariant>(entity);
                var progress = SystemAPI.GetBuffer<ReverseEngineeringBlueprintProgress>(entity);

                if (tasks.Length == 0)
                {
                    TryEnqueueNextTask(faction.ValueRO, ref reverseState.ValueRW, evidence, progress, tasks, config);
                }

                if (tasks.Length == 0)
                {
                    continue;
                }

                var task = tasks[0];
                var throughput = ResolveThroughput(faction.ValueRO);
                var duration = math.max(0.5f, task.DurationSeconds);
                task.Progress = math.min(1f, task.Progress + (deltaSeconds * throughput) / duration);

                if (task.Progress < 1f)
                {
                    tasks[0] = task;
                    continue;
                }

                switch (task.Type)
                {
                    case ReverseEngineeringTaskType.ForensicScan:
                        CompleteForensicScan(evidence, task.BlueprintId);
                        break;
                    case ReverseEngineeringTaskType.DestructiveAnalysis:
                        CompleteDestructiveAnalysis(evidence, task.BlueprintId, config);
                        break;
                    case ReverseEngineeringTaskType.SynthesizePrototype:
                        CompleteSynthesis(evidence, progress, variants, ref reverseState.ValueRW, task, config);
                        break;
                }

                tasks.RemoveAt(0);
            }
        }

        private static void TryEnqueueNextTask(
            in Space4XFaction faction,
            ref ReverseEngineeringState state,
            DynamicBuffer<ReverseEngineeringEvidence> evidence,
            DynamicBuffer<ReverseEngineeringBlueprintProgress> progress,
            DynamicBuffer<ReverseEngineeringTask> tasks,
            in ReverseEngineeringConfig config)
        {
            if (TryFindBlueprint(evidence, 0, out var blueprintId))
            {
                tasks.Add(new ReverseEngineeringTask
                {
                    TaskId = state.NextTaskId++,
                    Type = ReverseEngineeringTaskType.ForensicScan,
                    BlueprintId = blueprintId,
                    EvidenceNeeded = 1,
                    DurationSeconds = config.ForensicScanDurationSeconds,
                    Progress = 0f,
                    AttemptIndex = 0,
                    TeamHash = faction.FactionId
                });
                return;
            }

            if (TryFindBlueprint(evidence, 1, out blueprintId))
            {
                tasks.Add(new ReverseEngineeringTask
                {
                    TaskId = state.NextTaskId++,
                    Type = ReverseEngineeringTaskType.DestructiveAnalysis,
                    BlueprintId = blueprintId,
                    EvidenceNeeded = 1,
                    DurationSeconds = config.DestructiveAnalysisDurationSeconds,
                    Progress = 0f,
                    AttemptIndex = 0,
                    TeamHash = faction.FactionId
                });
                return;
            }

            if (TryFindBlueprint(evidence, 2, out blueprintId))
            {
                var attemptIndex = ResolveAttemptIndex(progress, blueprintId);
                tasks.Add(new ReverseEngineeringTask
                {
                    TaskId = state.NextTaskId++,
                    Type = ReverseEngineeringTaskType.SynthesizePrototype,
                    BlueprintId = blueprintId,
                    EvidenceNeeded = config.EvidencePerSynthesis,
                    DurationSeconds = config.SynthesisDurationSeconds,
                    Progress = 0f,
                    AttemptIndex = attemptIndex,
                    TeamHash = faction.FactionId
                });
            }
        }

        private static bool TryFindBlueprint(DynamicBuffer<ReverseEngineeringEvidence> evidence, byte stage, out ushort blueprintId)
        {
            for (int i = 0; i < evidence.Length; i++)
            {
                if (evidence[i].Stage == stage && evidence[i].Integrity > 0)
                {
                    blueprintId = evidence[i].BlueprintId;
                    return true;
                }
            }

            blueprintId = 0;
            return false;
        }

        private static void CompleteForensicScan(DynamicBuffer<ReverseEngineeringEvidence> evidence, ushort blueprintId)
        {
            for (int i = 0; i < evidence.Length; i++)
            {
                if (evidence[i].BlueprintId != blueprintId || evidence[i].Stage != 0 || evidence[i].Integrity == 0)
                {
                    continue;
                }

                var entry = evidence[i];
                entry.Stage = 1;
                BoostCoverage(ref entry, 4);
                evidence[i] = entry;
                break;
            }
        }

        private static void CompleteDestructiveAnalysis(DynamicBuffer<ReverseEngineeringEvidence> evidence, ushort blueprintId, in ReverseEngineeringConfig config)
        {
            for (int i = 0; i < evidence.Length; i++)
            {
                if (evidence[i].BlueprintId != blueprintId || evidence[i].Stage != 1 || evidence[i].Integrity == 0)
                {
                    continue;
                }

                var entry = evidence[i];
                entry.Stage = 2;
                entry.Integrity = (byte)math.max(0, entry.Integrity - config.IntegrityCostAnalysis);
                if (entry.Integrity == 0)
                {
                    evidence.RemoveAt(i);
                }
                else
                {
                    BoostCoverage(ref entry, 9);
                    evidence[i] = entry;
                }
                break;
            }
        }

        private static void CompleteSynthesis(
            DynamicBuffer<ReverseEngineeringEvidence> evidence,
            DynamicBuffer<ReverseEngineeringBlueprintProgress> progress,
            DynamicBuffer<ReverseEngineeringBlueprintVariant> variants,
            ref ReverseEngineeringState state,
            ReverseEngineeringTask task,
            in ReverseEngineeringConfig config)
        {
            FixedList64Bytes<int> indices = default;
            for (int i = 0; i < evidence.Length && indices.Length < config.EvidencePerSynthesis; i++)
            {
                if (evidence[i].BlueprintId == task.BlueprintId && evidence[i].Stage >= 2 && evidence[i].Integrity > 0)
                {
                    indices.Add(i);
                }
            }

            if (indices.Length == 0)
            {
                return;
            }

            var average = ComputeEvidenceAverages(evidence, indices);
            var evidenceHash = ComputeEvidenceHash(evidence, indices, task.BlueprintId);
            var seed = math.hash(new uint4(evidenceHash, task.TeamHash, task.BlueprintId, task.AttemptIndex + 1u));
            if (seed == 0u)
            {
                seed = 1u;
            }

            var random = new Unity.Mathematics.Random(seed);
            var quality = math.clamp(average.quality, 0.1f, 1f);

            var variant = new ReverseEngineeringBlueprintVariant
            {
                VariantId = state.NextVariantId++,
                BlueprintId = task.BlueprintId,
                Quality = (byte)math.round(quality * 100f),
                RemainingRuns = (byte)math.clamp(math.round(quality * config.MaxVariantRuns), 1, config.MaxVariantRuns),
                EfficiencyScalar = ComputeScalar(average.coverageEfficiency, quality, 1f, ref random, 0.75f, 1.3f),
                ReliabilityScalar = ComputeScalar(average.coverageReliability, quality, 1f, ref random, 0.7f, 1.35f),
                MassScalar = ComputeScalar(average.coverageMass, quality, -1f, ref random, 0.7f, 1.35f),
                PowerScalar = ComputeScalar(average.coveragePower, quality, -1f, ref random, 0.7f, 1.35f),
                SignatureScalar = ComputeScalar(average.coverageSignature, quality, -1f, ref random, 0.7f, 1.35f),
                DurabilityScalar = ComputeScalar(average.coverageDurability, quality, 1f, ref random, 0.75f, 1.3f),
                EvidenceHash = evidenceHash,
                Seed = seed
            };

            variants.Add(variant);

            ApplyIntegrityCosts(evidence, indices, config.IntegrityCostSynthesis);
            IncrementAttempt(progress, task.BlueprintId);
        }

        private static float ResolveThroughput(in Space4XFaction faction)
        {
            return 0.5f + (float)faction.ResearchFocus * 1.5f;
        }

        private static void BoostCoverage(ref ReverseEngineeringEvidence entry, byte boost)
        {
            entry.CoverageEfficiency = (byte)math.min(100, entry.CoverageEfficiency + boost);
            entry.CoverageReliability = (byte)math.min(100, entry.CoverageReliability + boost);
            entry.CoverageMass = (byte)math.min(100, entry.CoverageMass + boost);
            entry.CoveragePower = (byte)math.min(100, entry.CoveragePower + boost);
            entry.CoverageSignature = (byte)math.min(100, entry.CoverageSignature + boost);
            entry.CoverageDurability = (byte)math.min(100, entry.CoverageDurability + boost);
        }

        private static (float quality, float coverageEfficiency, float coverageReliability, float coverageMass, float coveragePower, float coverageSignature, float coverageDurability) ComputeEvidenceAverages(
            DynamicBuffer<ReverseEngineeringEvidence> evidence,
            FixedList64Bytes<int> indices)
        {
            float fidelitySum = 0f;
            float integritySum = 0f;
            float eff = 0f;
            float rel = 0f;
            float mass = 0f;
            float power = 0f;
            float sig = 0f;
            float dura = 0f;

            for (int i = 0; i < indices.Length; i++)
            {
                var entry = evidence[indices[i]];
                fidelitySum += entry.Fidelity;
                integritySum += entry.Integrity;
                eff += entry.CoverageEfficiency;
                rel += entry.CoverageReliability;
                mass += entry.CoverageMass;
                power += entry.CoveragePower;
                sig += entry.CoverageSignature;
                dura += entry.CoverageDurability;
            }

            var count = math.max(1, indices.Length);
            var fidelityAvg = fidelitySum / count;
            var integrityAvg = integritySum / count;
            var quality = (fidelityAvg * 0.6f + integrityAvg * 0.4f) / 100f;

            return (
                quality,
                eff / (count * 100f),
                rel / (count * 100f),
                mass / (count * 100f),
                power / (count * 100f),
                sig / (count * 100f),
                dura / (count * 100f));
        }

        private static uint ComputeEvidenceHash(DynamicBuffer<ReverseEngineeringEvidence> evidence, FixedList64Bytes<int> indices, ushort blueprintId)
        {
            uint hash = (uint)(blueprintId * 2654435761u);
            for (int i = 0; i < indices.Length; i++)
            {
                var entry = evidence[indices[i]];
                hash = math.hash(new uint4(hash, entry.EvidenceSeed, entry.Fidelity, entry.Integrity));
            }
            return hash == 0u ? 1u : hash;
        }

        private static float ComputeScalar(float coverage, float quality, float direction, ref Unity.Mathematics.Random random, float min, float max)
        {
            var baseDelta = (quality - 0.5f) * 0.12f + (coverage - 0.5f) * 0.18f;
            var variance = (1f - coverage) * 0.12f;
            var scalar = 1f + direction * baseDelta + random.NextFloat(-variance, variance);
            return math.clamp(scalar, min, max);
        }

        private static void ApplyIntegrityCosts(DynamicBuffer<ReverseEngineeringEvidence> evidence, FixedList64Bytes<int> indices, byte cost)
        {
            for (int i = indices.Length - 1; i >= 0; i--)
            {
                var index = indices[i];
                if (index < 0 || index >= evidence.Length)
                {
                    continue;
                }

                var entry = evidence[index];
                entry.Integrity = (byte)math.max(0, entry.Integrity - cost);
                if (entry.Integrity == 0)
                {
                    evidence.RemoveAt(index);
                }
                else
                {
                    evidence[index] = entry;
                }
            }
        }

        private static uint ResolveAttemptIndex(DynamicBuffer<ReverseEngineeringBlueprintProgress> progress, ushort blueprintId)
        {
            for (int i = 0; i < progress.Length; i++)
            {
                if (progress[i].BlueprintId == blueprintId)
                {
                    return progress[i].AttemptCount;
                }
            }

            progress.Add(new ReverseEngineeringBlueprintProgress
            {
                BlueprintId = blueprintId,
                AttemptCount = 0
            });
            return 0;
        }

        private static void IncrementAttempt(DynamicBuffer<ReverseEngineeringBlueprintProgress> progress, ushort blueprintId)
        {
            for (int i = 0; i < progress.Length; i++)
            {
                if (progress[i].BlueprintId != blueprintId)
                {
                    continue;
                }

                var entry = progress[i];
                entry.AttemptCount += 1;
                progress[i] = entry;
                return;
            }

            progress.Add(new ReverseEngineeringBlueprintProgress
            {
                BlueprintId = blueprintId,
                AttemptCount = 1
            });
        }
    }
}
