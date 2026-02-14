using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Space4x.Scenario;
using Space4X.Headless;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.ModulesProvenance
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XModuleProvenanceProofSystem : ISystem
    {
        private static readonly FixedString64Bytes ScenarioAdvantage = new FixedString64Bytes("space4x_modules_provenance_advantage_micro");
        private static readonly FixedString64Bytes ScenarioSurpass = new FixedString64Bytes("space4x_modules_reverse_engineer_surpass_micro");
        private static readonly BlueprintId ProofBlueprint = ModuleBlueprintExamples.LaserSMk1RapidBlueprintId;

        private byte _flushRequested;
        private FixedString64Bytes _activeScenario;

        private struct OrgInputs
        {
            public float MaterialQuality;
            public float WorkforceSkill;
            public float FacilityCapability;
            public float FacilityMaturity;
            public float TargetQuality;
            public float AssemblerSkill;
            public float AssemblerCapability;
            public float AssemblerProcessMaturity;
            public BlueprintProvenance Provenance;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info))
            {
                return;
            }

            var scenarioId = info.ScenarioId;
            if (!scenarioId.Equals(ScenarioAdvantage) && !scenarioId.Equals(ScenarioSurpass))
            {
                _activeScenario = default;
                _flushRequested = 0;
                return;
            }

            if (!_activeScenario.Equals(scenarioId))
            {
                _activeScenario = scenarioId;
                _flushRequested = 0;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var time = SystemAPI.GetSingleton<TimeState>();
            var completionTick = ResolveCompletionTick(runtime, time.FixedDeltaTime);
            var shouldEmitNow = time.Tick % 30u == 0u || (completionTick > 0u && time.Tick >= completionTick);
            if (!shouldEmitNow)
            {
                return;
            }

            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var isSurpassScenario = scenarioId.Equals(ScenarioSurpass);
            BuildScenarioInputs(isSurpassScenario, out var orgA, out var orgB);
            var orgAAvg = EvaluateAverageIntegrationQuality(info.Seed, in orgA);
            var orgBAvg = EvaluateAverageIntegrationQuality(info.Seed, in orgB);
            var delta = orgAAvg - orgBAvg;

            AddMetric(buffer, "modules.provenance.orgA.avg_integration_quality", orgAAvg);
            AddMetric(buffer, "modules.provenance.orgB.avg_integration_quality", orgBAvg);
            AddMetric(buffer, "modules.provenance.integration_quality_delta", delta);

            if (_flushRequested == 0 && completionTick > 0u && time.Tick >= completionTick)
            {
                _flushRequested = 1;
                Space4XOperatorReportUtility.RequestHeadlessAnswersFlush(ref state, time.Tick);
                Debug.Log($"[ModulesProvenanceProof] COMPLETE tick={time.Tick} orgA={orgAAvg:0.###} orgB={orgBAvg:0.###} delta={delta:0.###}");
            }
        }

        private static uint ResolveCompletionTick(in Space4XScenarioRuntime runtime, float fixedDeltaTime)
        {
            if (runtime.EndTick > 0u)
            {
                return runtime.EndTick;
            }

            if (runtime.DurationSeconds <= 0f)
            {
                return 0u;
            }

            var dt = fixedDeltaTime > 0f ? fixedDeltaTime : (1f / 60f);
            var durationTicks = (uint)math.max(1f, math.ceil(runtime.DurationSeconds / math.max(1e-4f, dt)));
            return runtime.StartTick + durationTicks;
        }

        private static void BuildScenarioInputs(bool surpassScenario, out OrgInputs orgA, out OrgInputs orgB)
        {
            orgA = new OrgInputs
            {
                MaterialQuality = 0.74f,
                WorkforceSkill = surpassScenario ? 0.68f : 0.71f,
                FacilityCapability = surpassScenario ? 0.66f : 0.69f,
                FacilityMaturity = surpassScenario ? 0.58f : 0.82f,
                TargetQuality = 0.72f,
                AssemblerSkill = surpassScenario ? 0.67f : 0.71f,
                AssemblerCapability = surpassScenario ? 0.68f : 0.69f,
                AssemblerProcessMaturity = surpassScenario ? 0.57f : 0.80f,
                Provenance = new BlueprintProvenance
                {
                    OriginOrgId = new OrgId { Value = 101 },
                    ProvenanceKind = BlueprintProvenanceKind.Original,
                    KnowledgeLevel = 1f,
                    ProcessMaturity = surpassScenario ? 0.60f : 0.83f
                }.Clamp01()
            };

            orgB = new OrgInputs
            {
                MaterialQuality = 0.74f,
                WorkforceSkill = surpassScenario ? 0.84f : 0.71f,
                FacilityCapability = surpassScenario ? 0.86f : 0.69f,
                FacilityMaturity = surpassScenario ? 0.96f : 0.46f,
                TargetQuality = 0.72f,
                AssemblerSkill = surpassScenario ? 0.86f : 0.71f,
                AssemblerCapability = surpassScenario ? 0.88f : 0.69f,
                AssemblerProcessMaturity = surpassScenario ? 0.97f : 0.46f,
                Provenance = new BlueprintProvenance
                {
                    OriginOrgId = new OrgId { Value = 207 },
                    ProvenanceKind = BlueprintProvenanceKind.ReverseEngineered,
                    KnowledgeLevel = surpassScenario ? 0.98f : 0.64f,
                    ProcessMaturity = surpassScenario ? 0.99f : 0.45f
                }.Clamp01()
            };
        }

        private static float EvaluateAverageIntegrationQuality(uint seed, in OrgInputs inputs)
        {
            const int SampleCount = 8;
            var total = 0f;

            for (var i = 0; i < SampleCount; i++)
            {
                var materialQuality = ResolveMaterialQualitySample(inputs.MaterialQuality, seed, i);
                var cooling = ModuleProvenanceQualityMath.EvaluateLimbQuality(
                    materialQuality,
                    inputs.WorkforceSkill,
                    inputs.FacilityCapability,
                    inputs.FacilityMaturity,
                    inputs.TargetQuality);
                var power = ModuleProvenanceQualityMath.EvaluateLimbQuality(
                    materialQuality,
                    inputs.WorkforceSkill,
                    inputs.FacilityCapability,
                    inputs.FacilityMaturity,
                    inputs.TargetQuality);
                var optics = ModuleProvenanceQualityMath.EvaluateLimbQuality(
                    materialQuality,
                    inputs.WorkforceSkill,
                    inputs.FacilityCapability,
                    inputs.FacilityMaturity,
                    inputs.TargetQuality);
                var mount = ModuleProvenanceQualityMath.EvaluateLimbQuality(
                    materialQuality,
                    inputs.WorkforceSkill,
                    inputs.FacilityCapability,
                    inputs.FacilityMaturity,
                    inputs.TargetQuality);
                var firmware = ModuleProvenanceQualityMath.EvaluateLimbQuality(
                    materialQuality,
                    inputs.WorkforceSkill,
                    inputs.FacilityCapability,
                    inputs.FacilityMaturity,
                    inputs.TargetQuality);

                var qualityVector = new ModuleQualityVector
                {
                    Cooling = cooling,
                    Power = power,
                    Optics = optics,
                    Mount = mount,
                    Firmware = firmware
                };

                var spec = new ModuleCommissionSpec
                {
                    MinCoolingQuality = 0.40f,
                    MinPowerQuality = 0.40f,
                    MinOpticsQuality = 0.40f,
                    MinMountQuality = 0.40f,
                    MinFirmwareQuality = 0.40f,
                    MinIntegrationQuality = 0.45f,
                    RejectBelowSpec = 1
                };

                if (!ModuleProvenanceQualityMath.TryFinalizeModule(
                        in qualityVector,
                        inputs.AssemblerSkill,
                        inputs.AssemblerCapability,
                        inputs.AssemblerProcessMaturity,
                        in inputs.Provenance,
                        in spec,
                        out var integrationQuality,
                        out _))
                {
                    integrationQuality = 0f;
                }

                total += integrationQuality;
            }

            return total / SampleCount;
        }

        private static float ResolveMaterialQualitySample(float baseQuality, uint seed, int index)
        {
            var sampleHash = math.hash(new uint3(seed == 0u ? 1u : seed, (uint)(index + 1), ProofBlueprint.StableHash()));
            var offset = ((sampleHash & 0x3FFu) / 1023f - 0.5f) * 0.02f;
            return math.saturate(baseQuality + offset);
        }

        private static void AddMetric(DynamicBuffer<Space4XOperatorMetric> buffer, string key, float value)
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
