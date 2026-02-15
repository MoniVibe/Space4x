using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Space4X.Headless;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.ModulesLearning
{
    public struct Space4XBlueprintLearningMicroState : IComponentData
    {
        public byte Initialized;
        public byte FlushRequested;
        public float OrgAMaturity;
        public float OrgBMaturity;
        public float OrgBMaturityStart;
        public float DeltaStart;
        public float DeltaCurrent;
        public uint CompletedBuilds;
        public uint LastBuildTick;
        public uint Digest;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XBlueprintLearningMicroSystem : ISystem
    {
        private static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("space4x_blueprint_learning_micro");
        private static readonly FixedString64Bytes MetricOrgAMaturity = new FixedString64Bytes("modules.learning.orgA.maturity");
        private static readonly FixedString64Bytes MetricOrgBMaturity = new FixedString64Bytes("modules.learning.orgB.maturity");
        private static readonly FixedString64Bytes MetricOrgBMaturityStart = new FixedString64Bytes("modules.learning.orgB.maturity.start");
        private static readonly FixedString64Bytes MetricIntegrationDelta = new FixedString64Bytes("modules.learning.integration_quality_delta");
        private static readonly FixedString64Bytes MetricIntegrationDeltaStart = new FixedString64Bytes("modules.learning.integration_quality_delta.start");
        private static readonly FixedString64Bytes MetricIntegrationDeltaEnd = new FixedString64Bytes("modules.learning.integration_quality_delta.end");
        private static readonly FixedString64Bytes MetricDigest = new FixedString64Bytes("modules.learning.digest");
        private static readonly FixedString64Bytes MetricBuilds = new FixedString64Bytes("modules.learning.completed_builds");

        private const uint BuildIntervalTicks = 45u;
        private const float OrgAInitialMaturity = 0.84f;
        private const float OrgBInitialMaturity = 0.34f;
        private const float OrgALearningK = 0.035f;
        private const float OrgBLearningK = 0.18f;
        private const float SharedCapability = 0.82f;
        private const float SharedMaterialQuality = 0.88f;
        private const float OrgAProvenanceBias = 0.06f;
        private const float OrgBProvenanceBias = -0.02f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            if (!scenarioInfo.ScenarioId.Equals(ScenarioId))
            {
                return;
            }

            var scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var tickState)
                ? tickState.Tick
                : time.Tick;
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();

            if (!SystemAPI.TryGetSingletonEntity<Space4XBlueprintLearningMicroState>(out var stateEntity))
            {
                stateEntity = state.EntityManager.CreateEntity(typeof(Space4XBlueprintLearningMicroState));
            }

            var learning = state.EntityManager.GetComponentData<Space4XBlueprintLearningMicroState>(stateEntity);
            InitializeIfNeeded(ref learning, scenarioTick);

            while (scenarioTick >= learning.LastBuildTick + BuildIntervalTicks)
            {
                learning.LastBuildTick += BuildIntervalTicks;
                learning.CompletedBuilds++;
                learning.OrgAMaturity = StepMaturity(learning.OrgAMaturity, OrgALearningK);
                learning.OrgBMaturity = StepMaturity(learning.OrgBMaturity, OrgBLearningK);
                learning.DeltaCurrent = ComputeIntegrationQualityDelta(learning.OrgAMaturity, learning.OrgBMaturity);
                learning.Digest = MixDigest(learning.Digest, learning.CompletedBuilds, learning.OrgBMaturity, learning.DeltaCurrent);
            }

            EmitLearningMetrics(ref state, in learning);

            if (runtime.EndTick > 0u && scenarioTick >= runtime.EndTick && learning.FlushRequested == 0)
            {
                Space4XOperatorReportUtility.RequestHeadlessAnswersFlush(ref state, time.Tick);
                learning.FlushRequested = 1;
            }

            state.EntityManager.SetComponentData(stateEntity, learning);
        }

        private static void InitializeIfNeeded(ref Space4XBlueprintLearningMicroState learning, uint scenarioTick)
        {
            if (learning.Initialized != 0)
            {
                return;
            }

            learning.Initialized = 1;
            learning.OrgAMaturity = OrgAInitialMaturity;
            learning.OrgBMaturity = OrgBInitialMaturity;
            learning.OrgBMaturityStart = OrgBInitialMaturity;
            learning.CompletedBuilds = 0u;
            learning.LastBuildTick = scenarioTick;
            learning.DeltaStart = ComputeIntegrationQualityDelta(learning.OrgAMaturity, learning.OrgBMaturity);
            learning.DeltaCurrent = learning.DeltaStart;
            learning.Digest = MixDigest(0xA341316Cu, 0u, learning.OrgBMaturity, learning.DeltaCurrent);
        }

        private static float StepMaturity(float maturity, float k)
        {
            var next = maturity + k * (1f - maturity);
            return math.clamp(next, 0f, 1f);
        }

        private static float ComputeIntegrationQualityDelta(float orgAMaturity, float orgBMaturity)
        {
            var orgAQuality = ComputeIntegrationQuality(orgAMaturity, OrgAProvenanceBias);
            var orgBQuality = ComputeIntegrationQuality(orgBMaturity, OrgBProvenanceBias);
            return orgAQuality - orgBQuality;
        }

        private static float ComputeIntegrationQuality(float maturity, float provenanceBias)
        {
            var quality =
                0.2f +
                0.55f * maturity +
                0.15f * SharedCapability +
                0.1f * SharedMaterialQuality +
                provenanceBias;
            return math.clamp(quality, 0f, 1f);
        }

        private static uint MixDigest(uint digest, uint buildCount, float maturity, float delta)
        {
            var quantMaturity = (uint)math.round(math.clamp(maturity, 0f, 1f) * 10000f);
            var quantDelta = (uint)math.round(math.clamp(delta + 1f, 0f, 2f) * 10000f);
            return math.hash(new uint4(digest ^ 0x9E3779B9u, buildCount + 0x85EBCA6Bu, quantMaturity + 0xC2B2AE35u, quantDelta + 0x27D4EB2Fu));
        }

        private static void EmitLearningMetrics(ref SystemState state, in Space4XBlueprintLearningMicroState learning)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var metrics))
            {
                return;
            }

            AddOrUpdateMetric(metrics, MetricOrgAMaturity, learning.OrgAMaturity);
            AddOrUpdateMetric(metrics, MetricOrgBMaturity, learning.OrgBMaturity);
            AddOrUpdateMetric(metrics, MetricOrgBMaturityStart, learning.OrgBMaturityStart);
            AddOrUpdateMetric(metrics, MetricIntegrationDelta, learning.DeltaCurrent);
            AddOrUpdateMetric(metrics, MetricIntegrationDeltaStart, learning.DeltaStart);
            AddOrUpdateMetric(metrics, MetricIntegrationDeltaEnd, learning.DeltaCurrent);
            AddOrUpdateMetric(metrics, MetricDigest, learning.Digest);
            AddOrUpdateMetric(metrics, MetricBuilds, learning.CompletedBuilds);
        }

        private static void AddOrUpdateMetric(DynamicBuffer<Space4XOperatorMetric> buffer, in FixedString64Bytes key, float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }
    }
}
