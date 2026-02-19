using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Production;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Modules;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Individual;
using PureDOTS.Systems;
using Space4X.Headless;
using Space4X.Presentation;
using Space4X.Registry;
using Space4X.Runtime;
using AlignmentTriplet = Space4X.Registry.AlignmentTriplet;
using IndividualStats = Space4X.Registry.IndividualStats;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using ResourceType = Space4X.Registry.ResourceType;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using SystemEnv = System.Environment;

namespace Space4x.Scenario
{
    /// <summary>
    /// Loads and executes the mining scenario from JSON, spawning carriers, mining vessels, and resource deposits.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class Space4XMiningScenarioSystem : SystemBase
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string PerfGateModeEnv = "PERF_GATE_MODE";
        private const string ExitPolicyEnv = "PUREDOTS_EXIT_POLICY";
        private const string HeadlessMiningForceEnv = "SPACE4X_HEADLESS_MINING_PROOF_UNDOCK";
        private const string JsonExtension = ".json";
        private const float DefaultSpawnVerticalRange = 60f;
        private const string RefitScenarioFile = "space4x_refit.json";
        private const string ResearchScenarioFile = "space4x_research_mvp.json";
        private bool _hasLoaded;
        private bool _loggedPerfGateMissingScenario;
        private MiningScenarioJson _scenarioData;
        private Dictionary<string, Entity> _spawnedEntities;
        private Dictionary<string, Entity> _profileEntities;
        private string _scenarioPath;
        private string _templateRoot;
        private bool _useSmokeMotionTuning;
        private bool _isCollisionScenario;
        private bool _applyDefaultModuleLoadouts;
        private Entity _friendlyAffiliationEntity;
        private Entity _hostileAffiliationEntity;
        private const ushort FriendlyFactionId = 1;
        private const ushort HostileFactionId = 2;

        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
        }

        protected override void OnUpdate()
        {
            if (_hasLoaded)
            {
                Enabled = false;
                return;
            }

            var scenarioPath = ResolveScenarioPath(out var scenarioInfo, out var hasScenarioInfo);
            if (!string.IsNullOrWhiteSpace(scenarioPath))
            {
                scenarioPath = Path.GetFullPath(scenarioPath);
                if (!File.Exists(scenarioPath))
                {
                    Debug.LogWarning($"[Space4XMiningScenario] Override missing, falling back to ScenarioInfo: {scenarioPath}");
                    scenarioPath = null;
                }
            }

            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                if (!hasScenarioInfo)
                {
                    // Wait for ScenarioInfo injection (smoke selector/bootstrap).
                    return;
                }

                var scenarioIdForWarning = scenarioInfo.ScenarioId.ToString();
                if (string.IsNullOrWhiteSpace(scenarioIdForWarning))
                {
                    scenarioIdForWarning = "unknown";
                }

                if (IsPerfGateModeActive(scenarioIdForWarning))
                {
                    if (!_loggedPerfGateMissingScenario)
                    {
                        Debug.LogWarning($"[Space4XMiningScenario] No mining scenario resolved for ScenarioId='{scenarioIdForWarning}' (perf gate mode active; mining scenario disabled).");
                        _loggedPerfGateMissingScenario = true;
                    }

                    Enabled = false;
                    return;
                }

                Debug.LogWarning($"[Space4XMiningScenario] No scenario path resolved for ScenarioId='{scenarioIdForWarning}'.");
                return;
            }

            if (!hasScenarioInfo)
            {
                scenarioInfo = EnsureScenarioInfoFromPath(scenarioPath);
                hasScenarioInfo = true;
            }

            _scenarioPath = scenarioPath;
            _templateRoot = ResolveTemplateRoot(scenarioPath);

            var isCentralSeedScenario = IsCentralSeedScenario(scenarioPath, scenarioInfo);
            var jsonText = File.ReadAllText(scenarioPath);
            _scenarioData = JsonUtility.FromJson<MiningScenarioJson>(jsonText);
            if (_scenarioData == null || (!isCentralSeedScenario && _scenarioData.spawn == null))
            {
                Debug.LogError("[Space4XMiningScenario] Failed to parse scenario JSON");
                Enabled = false;
                return;
            }

            ApplyScenarioConfig(_scenarioData.scenarioConfig);

            _useSmokeMotionTuning = IsSmokeScenario(scenarioPath, scenarioInfo);
            if (_useSmokeMotionTuning)
            {
                ApplySmokeMotionProfile();
                ApplySmokeLatchConfig();
                ApplyFloatingOriginConfig();
            }
            else if (IsHeadlessMiningProofEnabled())
            {
                // Headless proof runs can stall at the latch phase without relaxed thresholds.
                ApplyHeadlessMiningLatchConfig();
            }
            ApplyDogfightConfig(_scenarioData.dogfightConfig);
            ApplyStanceConfig(_scenarioData.stanceConfig);

            var timeState = SystemAPI.GetSingleton<TimeState>();

            var scenarioFileName = Path.GetFileName(scenarioPath);
            var isRefitScenario = scenarioFileName.Equals(RefitScenarioFile, StringComparison.OrdinalIgnoreCase);
            var isResearchScenario = scenarioFileName.Equals(ResearchScenarioFile, StringComparison.OrdinalIgnoreCase);
            var isMiningScenario = !(isRefitScenario || isResearchScenario || isCentralSeedScenario);
            _isCollisionScenario = scenarioFileName.Equals("space4x_collision_micro.json", StringComparison.OrdinalIgnoreCase);
            if (_isCollisionScenario)
            {
                EnsureCollisionScenarioTag();
            }

            _spawnedEntities = new Dictionary<string, Entity>();
            _profileEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            if (isMiningScenario)
            {
                SpawnEntities(timeState.Tick, timeState.FixedDeltaTime);
                ApplyPersonalRelations(timeState.Tick);
                EnsureScenarioFactionRelations(timeState.Tick);
                ForceHeadlessMiningTargets(timeState.Tick);
            }
            var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);
            var durationSeconds = math.max(0f, _scenarioData.duration_s);
            var durationTicks = (uint)math.ceil(durationSeconds / fixedDt);
            var startTick = timeState.Tick;
            var safeDurationTicks = durationTicks == 0 ? 1u : durationTicks;
            var endTick = startTick + safeDurationTicks;
            var runtimeEntity = EnsureScenarioRuntime(startTick, endTick, durationSeconds);
            if (isMiningScenario)
            {
                ScheduleScenarioActions(runtimeEntity, startTick, fixedDt);
            }
            UpdateScenarioInfoSingleton(scenarioInfo, safeDurationTicks);

            if (isMiningScenario)
            {
                Debug.Log($"[Space4XMiningScenario] Loaded '{scenarioPath}'. Spawned carriers/miners/asteroids. Duration={durationSeconds:F1}s ticks={safeDurationTicks} (startTick={startTick}, endTick={endTick}).");
            }
            else
            {
                Debug.Log($"[Space4XMiningScenario] Loaded '{scenarioPath}'. Deferring spawns to scenario-specific systems. Duration={durationSeconds:F1}s ticks={safeDurationTicks} (startTick={startTick}, endTick={endTick}).");
            }

            _hasLoaded = true;
            Enabled = false;
        }

        private void ApplyScenarioConfig(MiningScenarioConfigData scenarioConfig)
        {
            _applyDefaultModuleLoadouts = scenarioConfig != null && scenarioConfig.applyDefaultModuleLoadouts;
            ApplyReferenceFrameConfig(scenarioConfig != null && scenarioConfig.applyReferenceFrames);
            ApplyOrbitalBandConfig(scenarioConfig);
            ApplyRenderFrameConfig(scenarioConfig);
            ApplyFleetcrawlContractConfig(scenarioConfig != null ? scenarioConfig.fleetCrawl : null);
            if (scenarioConfig == null)
            {
                return;
            }

            ApplySensorsBeatConfig(scenarioConfig.sensorsBeat);
            ApplyCommsBeatConfig(scenarioConfig.commsBeat);
            ApplyHeadlessQuestionPackConfig(scenarioConfig.headlessQuestions);
            if (scenarioConfig.applyFloatingOrigin)
            {
                ApplyFloatingOriginConfig();
            }
        }

        private void ApplyFleetcrawlContractConfig(FleetcrawlScenarioConfigData config)
        {
            if (config == null)
            {
                ClearFleetcrawlContractConfig();
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XFleetcrawlScenarioContractConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XFleetcrawlScenarioContractConfig));
                EntityManager.AddBuffer<Space4XFleetcrawlRoomPlanOverride>(configEntity);
            }
            else if (!EntityManager.HasBuffer<Space4XFleetcrawlRoomPlanOverride>(configEntity))
            {
                EntityManager.AddBuffer<Space4XFleetcrawlRoomPlanOverride>(configEntity);
            }

            var contractId = string.IsNullOrWhiteSpace(config.contractId)
                ? string.Empty
                : config.contractId.Trim();
            var runDifficulty = string.IsNullOrWhiteSpace(config.runDifficulty)
                ? "normal"
                : config.runDifficulty.Trim().ToLowerInvariant();
            var depthStart = math.max(1, config.depthStart <= 0 ? 1 : config.depthStart);

            var roomPlan = EntityManager.GetBuffer<Space4XFleetcrawlRoomPlanOverride>(configEntity);
            roomPlan.Clear();
            if (config.roomPlan != null)
            {
                for (var i = 0; i < config.roomPlan.Count; i++)
                {
                    var entry = config.roomPlan[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    var archetype = string.IsNullOrWhiteSpace(entry.archetype) ? string.Empty : entry.archetype.Trim().ToLowerInvariant();
                    var roomClass = string.IsNullOrWhiteSpace(entry.roomClass) ? "normal" : entry.roomClass.Trim().ToLowerInvariant();
                    var systemSize = string.IsNullOrWhiteSpace(entry.systemSize) ? "medium" : entry.systemSize.Trim().ToLowerInvariant();
                    var threatLevel = math.max(1, entry.threatLevel);
                    if (string.IsNullOrWhiteSpace(archetype))
                    {
                        continue;
                    }

                    roomPlan.Add(new Space4XFleetcrawlRoomPlanOverride
                    {
                        Archetype = new FixedString64Bytes(TrimAscii(archetype, 63)),
                        RoomClass = new FixedString32Bytes(TrimAscii(roomClass, 31)),
                        SystemSize = new FixedString32Bytes(TrimAscii(systemSize, 31)),
                        ThreatLevel = threatLevel,
                        WildcardsCsv = BuildWildcardCsv(entry.wildcards)
                    });
                }
            }

            EntityManager.SetComponentData(configEntity, new Space4XFleetcrawlScenarioContractConfig
            {
                ContractId = new FixedString64Bytes(TrimAscii(contractId, 63)),
                RunDifficulty = new FixedString32Bytes(TrimAscii(runDifficulty, 31)),
                DepthStart = depthStart,
                HasRoomPlan = (byte)(roomPlan.Length > 0 ? 1 : 0)
            });

            Debug.Log($"[Space4XMiningScenario] Fleetcrawl contract config applied. contract={contractId} run_difficulty={runDifficulty} depth_start={depthStart} room_plan={roomPlan.Length}.");
        }

        private void ClearFleetcrawlContractConfig()
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XFleetcrawlScenarioContractConfig>(out var configEntity))
            {
                EntityManager.DestroyEntity(configEntity);
            }
        }

        private static FixedString128Bytes BuildWildcardCsv(List<string> wildcards)
        {
            var csv = new FixedString128Bytes();
            if (wildcards == null || wildcards.Count == 0)
            {
                return csv;
            }

            for (var i = 0; i < wildcards.Count; i++)
            {
                var token = wildcards[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var normalized = token.Trim().ToLowerInvariant();
                if (normalized.Length == 0)
                {
                    continue;
                }

                if (csv.Length > 0)
                {
                    csv.Append('|');
                }

                csv.Append(new FixedString32Bytes(TrimAscii(normalized, 31)));
            }

            return csv;
        }

        private static string TrimAscii(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value) || maxLen <= 0)
            {
                return string.Empty;
            }

            return value.Length > maxLen ? value.Substring(0, maxLen) : value;
        }

        private void ApplySensorsBeatConfig(SensorsBeatConfigData beat)
        {
            if (beat == null ||
                string.IsNullOrWhiteSpace(beat.observerCarrierId) ||
                string.IsNullOrWhiteSpace(beat.targetCarrierId))
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XSensorsBeatConfig>(out var entity))
            {
                entity = EntityManager.CreateEntity(typeof(Space4XSensorsBeatConfig));
            }

            EntityManager.SetComponentData(entity, new Space4XSensorsBeatConfig
            {
                ObserverCarrierId = new FixedString64Bytes(beat.observerCarrierId),
                TargetCarrierId = new FixedString64Bytes(beat.targetCarrierId),
                AcquireStartSeconds = math.max(0f, beat.acquireStart_s),
                AcquireDurationSeconds = math.max(0f, beat.acquireDuration_s),
                DropStartSeconds = math.max(0f, beat.dropStart_s),
                DropDurationSeconds = math.max(0f, beat.dropDuration_s),
                ObserverRange = math.max(0f, beat.observerRange),
                ObserverUpdateInterval = math.max(0f, beat.observerUpdateInterval),
                ObserverMaxTrackedTargets = (byte)math.clamp(beat.observerMaxTrackedTargets > 0 ? beat.observerMaxTrackedTargets : 12, 1, 255),
                SensorsEnsured = 0,
                Initialized = 0,
                Completed = 0,
                AcquireStartTick = 0u,
                AcquireEndTick = 0u,
                DropStartTick = 0u,
                DropEndTick = 0u
            });
        }

        private void ApplyCommsBeatConfig(CommsBeatConfigData beat)
        {
            if (beat == null ||
                string.IsNullOrWhiteSpace(beat.senderCarrierId) ||
                string.IsNullOrWhiteSpace(beat.receiverCarrierId))
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XCommsBeatConfig>(out var entity))
            {
                entity = EntityManager.CreateEntity(typeof(Space4XCommsBeatConfig));
            }

            var transport = beat.transportMask != 0
                ? (PerceptionChannel)beat.transportMask
                : PerceptionChannel.EM;

            EntityManager.SetComponentData(entity, new Space4XCommsBeatConfig
            {
                SenderCarrierId = new FixedString64Bytes(beat.senderCarrierId),
                ReceiverCarrierId = new FixedString64Bytes(beat.receiverCarrierId),
                PayloadId = string.IsNullOrWhiteSpace(beat.payloadId)
                    ? default
                    : new FixedString64Bytes(beat.payloadId),
                TransportMask = transport,
                StartSeconds = math.max(0f, beat.start_s),
                DurationSeconds = math.max(0.1f, beat.duration_s),
                SendIntervalSeconds = math.max(0.05f, beat.interval_s),
                RequireAck = (byte)(beat.requireAck ? 1 : 0),
                CommsEnsured = 0,
                Initialized = 0,
                Completed = 0,
                StartTick = 0u,
                EndTick = 0u,
                SendIntervalTicks = 0u
            });
        }

        private void ApplyHeadlessQuestionPackConfig(List<HeadlessQuestionConfigData> questions)
        {
            if (questions == null || questions.Count == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XHeadlessQuestionPackTag>(out var entity))
            {
                entity = EntityManager.CreateEntity(typeof(Space4XHeadlessQuestionPackTag));
            }

            var buffer = EntityManager.HasBuffer<Space4XHeadlessQuestionPackItem>(entity)
                ? EntityManager.GetBuffer<Space4XHeadlessQuestionPackItem>(entity)
                : EntityManager.AddBuffer<Space4XHeadlessQuestionPackItem>(entity);

            buffer.Clear();
            for (var i = 0; i < questions.Count; i++)
            {
                var question = questions[i];
                if (question == null || string.IsNullOrWhiteSpace(question.id))
                {
                    continue;
                }

                buffer.Add(new Space4XHeadlessQuestionPackItem
                {
                    Id = new FixedString64Bytes(question.id),
                    Required = (byte)(question.required ? 1 : 0)
                });
            }
        }

        private static bool IsSmokeScenario(string scenarioPath, ScenarioInfo scenarioInfo)
        {
            var scenarioName = Path.GetFileNameWithoutExtension(scenarioPath);
            if (!string.IsNullOrWhiteSpace(scenarioName) &&
                (scenarioName.StartsWith("space4x_smoke", StringComparison.OrdinalIgnoreCase)
                 || scenarioName.StartsWith("space4x_movement", StringComparison.OrdinalIgnoreCase)
                 || scenarioName.StartsWith("space4x_bug_hunt", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var scenarioId = scenarioInfo.ScenarioId.ToString();
            if (scenarioId.StartsWith("space4x_smoke", StringComparison.OrdinalIgnoreCase) ||
                scenarioId.StartsWith("space4x_bug_hunt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return scenarioId.StartsWith("space4x_movement", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCentralSeedScenario(string scenarioPath, ScenarioInfo scenarioInfo)
        {
            if (IsCentralSeedScenarioId(scenarioInfo.ScenarioId.ToString()))
            {
                return true;
            }

            var scenarioName = Path.GetFileNameWithoutExtension(scenarioPath);
            return IsCentralSeedScenarioId(scenarioName);
        }

        private static bool IsCentralSeedScenarioId(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return false;
            }

            var trimmed = scenarioId.Trim();
            return trimmed.Equals("space4x_hivemind_swarm_micro", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("space4x_infected_swarm_hivemind_micro", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySmokeMotionProfile()
        {
            var config = VesselMotionProfileConfig.Default;
            config.CapitalShipSpeedMultiplier = 0.65f;
            config.CapitalShipTurnMultiplier = 0.7f;
            config.CapitalShipAccelerationMultiplier = 0.6f;
            config.CapitalShipDecelerationMultiplier = 0.7f;
            config.MiningUndockSpeedMultiplier = 0.3f;
            config.MiningApproachSpeedMultiplier = 0.6f;
            config.MiningLatchSpeedMultiplier = 0.25f;
            config.MiningDetachSpeedMultiplier = 0.35f;
            config.MiningReturnSpeedMultiplier = 0.7f;
            config.MiningDockSpeedMultiplier = 0.25f;

            if (!SystemAPI.TryGetSingletonEntity<VesselMotionProfileConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(VesselMotionProfileConfig));
            }

            EntityManager.SetComponentData(configEntity, config);

            var allowMining = string.Equals(
                SystemEnv.GetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF"),
                "1",
                StringComparison.OrdinalIgnoreCase);
            if (!allowMining && !SystemAPI.TryGetSingletonEntity<Space4XLegacyMiningDisabledTag>(out _))
            {
                EntityManager.CreateEntity(typeof(Space4XLegacyMiningDisabledTag));
            }
        }

        private void ApplyFloatingOriginConfig()
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XFloatingOriginConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XFloatingOriginConfig));
            }

            EntityManager.SetComponentData(configEntity, new Space4XFloatingOriginConfig
            {
                Threshold = 500f,
                CooldownTicks = 300,
                Enabled = 1
            });
        }

        private void ApplyReferenceFrameConfig(bool enabled)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XReferenceFrameConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XReferenceFrameConfig));
            }

            EntityManager.SetComponentData(configEntity, new Space4XReferenceFrameConfig
            {
                Enabled = (byte)(enabled ? 1 : 0),
                LocalBubbleRadius = 500f,
                EnterSOIMultiplier = 0.9f,
                ExitSOIMultiplier = 1.1f
            });
        }

        private void ApplyOrbitalBandConfig(MiningScenarioConfigData scenarioConfig)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XOrbitalBandConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XOrbitalBandConfig));
            }

            var config = Space4XOrbitalBandConfig.Default;
            var referenceFramesEnabled = scenarioConfig != null && scenarioConfig.applyReferenceFrames;
            var enabled = referenceFramesEnabled;
            var bandOverride = scenarioConfig != null ? scenarioConfig.orbitalBand : null;
            if (bandOverride != null && bandOverride.enabled >= 0)
            {
                enabled = referenceFramesEnabled && bandOverride.enabled != 0;
            }
            config.Enabled = (byte)(enabled ? 1 : 0);

            if (bandOverride != null)
            {
                if (bandOverride.innerRadius >= 0f)
                {
                    config.InnerRadius = bandOverride.innerRadius;
                }
                if (bandOverride.outerRadius >= 0f)
                {
                    config.OuterRadius = bandOverride.outerRadius;
                }
                if (bandOverride.distanceScale > 0f)
                {
                    config.DistanceScale = bandOverride.distanceScale;
                }
                if (bandOverride.speedScale > 0f)
                {
                    config.SpeedScale = bandOverride.speedScale;
                }
                if (bandOverride.rangeScale > 0f)
                {
                    config.RangeScale = bandOverride.rangeScale;
                }
                if (bandOverride.presentationScale > 0f)
                {
                    config.PresentationScale = bandOverride.presentationScale;
                }
                if (bandOverride.enterMultiplier > 0f)
                {
                    config.EnterMultiplier = bandOverride.enterMultiplier;
                }
                if (bandOverride.exitMultiplier > 0f)
                {
                    config.ExitMultiplier = bandOverride.exitMultiplier;
                }
            }
            EntityManager.SetComponentData(configEntity, config);
        }

        private void ApplyRenderFrameConfig(MiningScenarioConfigData scenarioConfig)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XRenderFrameConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XRenderFrameConfig));
            }

            var config = Space4XRenderFrameConfig.Default;
            var frameOverride = scenarioConfig != null ? scenarioConfig.renderFrame : null;
            if (frameOverride != null)
            {
                if (frameOverride.enabled >= 0)
                {
                    config.Enabled = (byte)(frameOverride.enabled != 0 ? 1 : 0);
                }
                if (frameOverride.useBandScale >= 0)
                {
                    config.UseBandScale = (byte)(frameOverride.useBandScale != 0 ? 1 : 0);
                }
                if (frameOverride.surfaceScale > 0f)
                {
                    config.SurfaceScale = frameOverride.surfaceScale;
                }
                if (frameOverride.orbitalScale > 0f)
                {
                    config.OrbitalScale = frameOverride.orbitalScale;
                }
                if (frameOverride.deepScale > 0f)
                {
                    config.DeepScale = frameOverride.deepScale;
                }
                if (frameOverride.surfaceEnterMultiplier > 0f)
                {
                    config.SurfaceEnterMultiplier = frameOverride.surfaceEnterMultiplier;
                }
                if (frameOverride.surfaceExitMultiplier > 0f)
                {
                    config.SurfaceExitMultiplier = frameOverride.surfaceExitMultiplier;
                }
                if (frameOverride.orbitalEnterMultiplier > 0f)
                {
                    config.OrbitalEnterMultiplier = frameOverride.orbitalEnterMultiplier;
                }
                if (frameOverride.orbitalExitMultiplier > 0f)
                {
                    config.OrbitalExitMultiplier = frameOverride.orbitalExitMultiplier;
                }
                if (frameOverride.minHoldTicks >= 0)
                {
                    config.MinHoldTicks = (uint)frameOverride.minHoldTicks;
                }
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private void ApplySmokeLatchConfig()
        {
            var config = Space4XMiningLatchConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMiningLatchConfig>(out var existing))
            {
                config = existing;
            }

            // Smoke runs can stall if latch alignment never resolves; relax alignment for headless stability.
            config.SurfaceEpsilon = math.max(config.SurfaceEpsilon, 3.2f);
            config.AlignDotThreshold = -1f;

            if (!SystemAPI.TryGetSingletonEntity<Space4XMiningLatchConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XMiningLatchConfig));
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private void ApplyHeadlessMiningLatchConfig()
        {
            var config = Space4XMiningLatchConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMiningLatchConfig>(out var existing))
            {
                config = existing;
            }

            config.SurfaceEpsilon = math.max(config.SurfaceEpsilon, 3.2f);
            config.AlignDotThreshold = -1f;

            if (!SystemAPI.TryGetSingletonEntity<Space4XMiningLatchConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XMiningLatchConfig));
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private static bool IsHeadlessMiningProofEnabled()
        {
            return string.Equals(
                SystemEnv.GetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF"),
                "1",
                StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyDogfightConfig(StrikeCraftDogfightConfigData data)
        {
            if (data == null)
            {
                return;
            }

            var config = StrikeCraftDogfightConfig.Default;
            config.TargetAcquireRadius = math.max(0f, data.acquireRadius);
            config.FireConeDegrees = math.clamp(data.coneDegrees, 1f, 180f);
            config.NavConstantN = math.max(0.1f, data.navConstantN);
            config.BreakOffDistance = math.max(0f, data.breakOffDistance);
            config.BreakOffTicks = (uint)math.max(0, data.breakOffTicks);
            config.RejoinRadius = math.max(0f, data.rejoinRadius);
            if (data.rejoinOffset != null && data.rejoinOffset.Length >= 3)
            {
                config.RejoinOffset = new float3(data.rejoinOffset[0], data.rejoinOffset[1], data.rejoinOffset[2]);
            }
            config.JinkStrength = math.max(0f, data.jinkStrength);

            if (!SystemAPI.TryGetSingletonEntity<StrikeCraftDogfightConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(StrikeCraftDogfightConfig));
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private void ApplyStanceConfig(StanceTuningConfigData data)
        {
            if (data == null)
            {
                return;
            }

            var config = Space4XStanceTuningConfig.Default;
            if (data.aggressive != null)
            {
                config.Aggressive = ApplyStanceEntry(config.Aggressive, data.aggressive);
            }
            if (data.balanced != null)
            {
                config.Balanced = ApplyStanceEntry(config.Balanced, data.balanced);
            }
            if (data.defensive != null)
            {
                config.Defensive = ApplyStanceEntry(config.Defensive, data.defensive);
            }
            if (data.evasive != null)
            {
                config.Evasive = ApplyStanceEntry(config.Evasive, data.evasive);
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XStanceTuningConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XStanceTuningConfig));
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private static StanceTuningEntry ApplyStanceEntry(in StanceTuningEntry baseEntry, StanceTuningEntryData data)
        {
            var entry = baseEntry;
            if (data == null)
            {
                return entry;
            }

            entry.AvoidanceRadius = math.max(0f, data.avoidanceRadius);
            entry.AvoidanceStrength = math.max(0f, data.avoidanceStrength);
            entry.SpeedMultiplier = math.max(0f, data.speedMultiplier);
            entry.RotationMultiplier = math.max(0f, data.rotationMultiplier);
            entry.MaintainFormationWhenAttacking = math.clamp(data.maintainFormationWhenAttacking, 0f, 1f);
            entry.EvasionJinkStrength = math.max(0f, data.evasionJinkStrength);
            entry.AutoEngageRadius = math.max(0f, data.autoEngageRadius);
            entry.AbortAttackOnDamageThreshold = math.clamp(data.abortAttackOnDamageThreshold, 0f, 1f);
            entry.ReturnToPatrolAfterCombat = (byte)(data.returnToPatrolAfterCombat ? 1 : 0);
            entry.CommandOverrideDropsToNeutral = (byte)(data.commandOverrideDropsToNeutral ? 1 : 0);
            entry.AttackMoveBearingWeight = math.max(0f, data.attackMoveBearingWeight);
            entry.AttackMoveDestinationWeight = math.max(0f, data.attackMoveDestinationWeight);
            return entry;
        }

        private Entity EnsureScenarioAffiliation(byte scenarioSide)
        {
            if (scenarioSide == 1)
            {
                if (_hostileAffiliationEntity != Entity.Null && EntityManager.Exists(_hostileAffiliationEntity))
                {
                    return _hostileAffiliationEntity;
                }
            }
            else
            {
                if (_friendlyAffiliationEntity != Entity.Null && EntityManager.Exists(_friendlyAffiliationEntity))
                {
                    return _friendlyAffiliationEntity;
                }
            }

            var entity = EntityManager.CreateEntity(typeof(AffiliationRelation));
            var affiliationName = scenarioSide == 1 ? "Scenario-Hostile" : "Scenario-Friendly";
            EntityManager.SetComponentData(entity, new AffiliationRelation
            {
                AffiliationName = new FixedString64Bytes(affiliationName)
            });
            EnsureScenarioFactionProfile(entity, scenarioSide);

            if (scenarioSide == 1)
            {
                _hostileAffiliationEntity = entity;
            }
            else
            {
                _friendlyAffiliationEntity = entity;
            }

            return entity;
        }

        private void AddScenarioAffiliation(Entity entity, byte scenarioSide)
        {
            var affiliationEntity = EnsureScenarioAffiliation(scenarioSide);
            if (affiliationEntity == Entity.Null)
            {
                return;
            }

            if (!EntityManager.HasBuffer<AffiliationTag>(entity))
            {
                EntityManager.AddBuffer<AffiliationTag>(entity);
            }

            var buffer = EntityManager.GetBuffer<AffiliationTag>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Target == affiliationEntity)
                {
                    return;
                }
            }

            buffer.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = affiliationEntity,
                Loyalty = (half)1f
            });
        }

        private void EnsureDefaultSubsystems(Entity entity, float hullMax)
        {
            var subsystems = EntityManager.HasBuffer<SubsystemHealth>(entity)
                ? EntityManager.GetBuffer<SubsystemHealth>(entity)
                : EntityManager.AddBuffer<SubsystemHealth>(entity);

            if (subsystems.Length == 0)
            {
                var engineMax = math.max(5f, hullMax * 0.3f);
                var weaponMax = math.max(5f, hullMax * 0.2f);
                subsystems.Add(new SubsystemHealth
                {
                    Type = SubsystemType.Engines,
                    Current = engineMax,
                    Max = engineMax,
                    RegenPerTick = math.max(0.01f, engineMax * 0.005f),
                    Flags = SubsystemFlags.None
                });
                subsystems.Add(new SubsystemHealth
                {
                    Type = SubsystemType.Weapons,
                    Current = weaponMax,
                    Max = weaponMax,
                    RegenPerTick = math.max(0.01f, weaponMax * 0.005f),
                    Flags = SubsystemFlags.None
                });
            }

            if (!EntityManager.HasBuffer<SubsystemDisabled>(entity))
            {
                EntityManager.AddBuffer<SubsystemDisabled>(entity);
            }

            if (!EntityManager.HasBuffer<DamageScarEvent>(entity))
            {
                EntityManager.AddBuffer<DamageScarEvent>(entity);
            }
        }

        private Entity EnsureScenarioRuntime(uint startTick, uint endTick, float durationSeconds)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XScenarioRuntime>(out var runtimeEntity))
            {
                runtimeEntity = EntityManager.CreateEntity(typeof(Space4XScenarioRuntime));
            }

            EntityManager.SetComponentData(runtimeEntity, new Space4XScenarioRuntime
            {
                StartTick = startTick,
                EndTick = endTick,
                DurationSeconds = durationSeconds
            });

            return runtimeEntity;
        }

        private ScenarioInfo EnsureScenarioInfoFromPath(string scenarioPath)
        {
            var scenarioId = Path.GetFileNameWithoutExtension(scenarioPath);
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = "space4x_smoke";
            }

            if (!SystemAPI.TryGetSingletonEntity<ScenarioInfo>(out var scenarioEntity))
            {
                scenarioEntity = EntityManager.CreateEntity(typeof(ScenarioInfo));
            }

            var scenarioInfo = new ScenarioInfo
            {
                ScenarioId = new FixedString64Bytes(scenarioId),
                Seed = 0,
                RunTicks = 0
            };

            EntityManager.SetComponentData(scenarioEntity, scenarioInfo);
            return scenarioInfo;
        }

        private void UpdateScenarioInfoSingleton(ScenarioInfo scenarioInfo, uint runTicks)
        {
            if (!SystemAPI.TryGetSingletonEntity<ScenarioInfo>(out var scenarioEntity))
            {
                scenarioEntity = EntityManager.CreateEntity(typeof(ScenarioInfo));
            }

            var scenarioId = scenarioInfo.ScenarioId;
            if (scenarioId.Length == 0)
            {
                scenarioId = new FixedString64Bytes("space4x_smoke");
            }

            EntityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = scenarioId,
                Seed = (uint)math.max(0, _scenarioData.seed),
                RunTicks = (int)runTicks
            });
        }

        private string ResolveScenarioPath(out ScenarioInfo scenarioInfo, out bool hasScenarioInfo)
        {
            scenarioInfo = default;
            hasScenarioInfo = false;

            var envValue = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                var normalizedEnvPath = Path.GetFullPath(envValue);
                if (File.Exists(normalizedEnvPath))
                {
                    return normalizedEnvPath;
                }

                Debug.LogWarning($"[Space4XMiningScenario] {ScenarioPathEnv} was set to '{envValue}', but the file was not found. Falling back to ScenarioInfo.");
            }

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var info))
            {
                return null;
            }

            hasScenarioInfo = true;
            scenarioInfo = info;
            return FindScenarioPath(info.ScenarioId.ToString());
        }

        private string FindScenarioPath(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                Debug.LogWarning("[Space4XMiningScenario] ScenarioId was empty; cannot resolve scenario file.");
                return null;
            }

            var normalizedScenarioId = NormalizeScenarioId(scenarioId);
            if (string.IsNullOrEmpty(normalizedScenarioId))
            {
                Debug.LogWarning("[Space4XMiningScenario] ScenarioId became empty after normalization.");
                return null;
            }

            if (File.Exists(scenarioId))
            {
                return scenarioId;
            }

            var possiblePaths = new[]
            {
                Path.Combine(Application.dataPath, "Scenarios", $"{normalizedScenarioId}{JsonExtension}"),
                Path.Combine(Application.dataPath, "..", "Assets", "Scenarios", $"{normalizedScenarioId}{JsonExtension}"),
                Path.Combine(Application.streamingAssetsPath, "Scenarios", $"{normalizedScenarioId}{JsonExtension}")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            if (IsPerfGateModeActive(scenarioId))
            {
                return null;
            }

            Debug.LogWarning($"[Space4XMiningScenario] Unable to locate scenario '{scenarioId}' (normalized '{normalizedScenarioId}'). Checked: {string.Join(", ", possiblePaths)}");
            return null;
        }

        private static bool IsPerfGateModeActive(string scenarioId)
        {
            if (IsEnvTruthy(PerfGateModeEnv))
            {
                return true;
            }

            var exitPolicy = SystemEnv.GetEnvironmentVariable(ExitPolicyEnv);
            if (string.Equals(exitPolicy, "never", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsPerfGateScenarioId(scenarioId);
        }

        private static bool IsEnvTruthy(string key)
        {
            var value = SystemEnv.GetEnvironmentVariable(key);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPerfGateScenarioId(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return false;
            }

            return scenarioId.Contains("perf_gate", StringComparison.OrdinalIgnoreCase)
                || scenarioId.Contains("perfgate", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeScenarioId(string scenarioId)
        {
            var trimmedId = scenarioId?.Trim();
            if (string.IsNullOrEmpty(trimmedId))
            {
                return null;
            }

            if (trimmedId.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedId.Substring(0, trimmedId.Length - JsonExtension.Length);
            }

            return trimmedId;
        }

        private void SpawnEntities(uint currentTick, float fixedDt)
        {
            var spawnCount = _scenarioData?.spawn?.Count ?? 0;
            if (_scenarioData?.spawn == null || spawnCount == 0)
            {
                var emptyLane = _scenarioData?.scenarioConfig?.spawnLane;
                Debug.Log($"SCENARIO_SPAWN lane={emptyLane ?? "unknown"} spawns=0 carriers_spawned=0");
                return;
            }

            var carrierCount = 0;
            if (spawnCount > 0)
            {
                for (int i = 0; i < spawnCount; i++)
                {
                    var spawn = _scenarioData.spawn[i];
                    if (spawn != null && string.Equals(spawn.kind, "Carrier", StringComparison.OrdinalIgnoreCase))
                    {
                        carrierCount++;
                    }
                }
            }

            var spawnLane = _scenarioData?.scenarioConfig?.spawnLane;
            Debug.Log($"SCENARIO_SPAWN lane={spawnLane ?? "unknown"} spawns={spawnCount} carriers_spawned={carrierCount}");

            foreach (var spawn in _scenarioData.spawn)
            {
                switch (spawn.kind)
                {
                    case "Carrier":
                        SpawnCarrier(spawn, currentTick, fixedDt);
                        break;
                    case "MiningVessel":
                        SpawnMiningVessel(spawn, currentTick);
                        break;
                    case "ResourceDeposit":
                        SpawnResourceDeposit(spawn);
                        break;
                }
            }
        }

        private void ForceHeadlessMiningTargets(uint currentTick)
        {
            if (!IsHeadlessMiningOverrideEnabled())
            {
                return;
            }

            var sources = new List<(Entity entity, FixedString64Bytes resourceId, float3 position)>();
            foreach (var (resourceState, resourceId, transform, entity) in SystemAPI
                         .Query<RefRO<ResourceSourceState>, RefRO<ResourceTypeId>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (_spawnedEntities != null && _spawnedEntities.Count > 0 && !_spawnedEntities.ContainsValue(entity))
                {
                    continue;
                }

                if (resourceState.ValueRO.UnitsRemaining <= 0f)
                {
                    continue;
                }

                sources.Add((entity, resourceId.ValueRO.Value, transform.ValueRO.Position));
            }

            if (sources.Count == 0)
            {
                return;
            }

            foreach (var (order, miningState, transform, entity) in SystemAPI
                         .Query<RefRW<MiningOrder>, RefRW<MiningState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (order.ValueRO.ResourceId.IsEmpty)
                {
                    continue;
                }

                var target = ResolveNearestMiningSource(order.ValueRO.ResourceId, transform.ValueRO.Position, sources);
                if (target == Entity.Null)
                {
                    continue;
                }

                order.ValueRW.PreferredTarget = target;
                order.ValueRW.TargetEntity = target;
                order.ValueRW.Status = MiningOrderStatus.Active;
                order.ValueRW.IssuedTick = currentTick;

                miningState.ValueRW.ActiveTarget = target;
                if (miningState.ValueRW.Phase == MiningPhase.Idle || miningState.ValueRW.Phase == MiningPhase.Undocking)
                {
                    miningState.ValueRW.Phase = MiningPhase.ApproachTarget;
                    miningState.ValueRW.PhaseTimer = 0f;
                }
            }
        }

        private static Entity ResolveNearestMiningSource(
            FixedString64Bytes resourceId,
            float3 position,
            List<(Entity entity, FixedString64Bytes resourceId, float3 position)> sources)
        {
            var bestTarget = Entity.Null;
            var bestDistanceSq = float.MaxValue;

            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source.resourceId != resourceId)
                {
                    continue;
                }

                var distanceSq = math.distancesq(position, source.position);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestTarget = source.entity;
                }
            }

            return bestTarget;
        }

        private static bool IsHeadlessMiningOverrideEnabled()
        {
            var value = SystemEnv.GetEnvironmentVariable(HeadlessMiningForceEnv);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private void SpawnCarrier(MiningSpawnDefinition spawn, uint currentTick, float fixedDt)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponentData(entity, new PostTransformMatrix
            {
                Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f))
            });
            EntityManager.AddComponent<SpatialIndexedTag>(entity);
            EntityManager.AddComponent<CommunicationModuleTag>(entity);
            EntityManager.AddComponentData(entity, MediumContext.Vacuum);

            var carrierId = new FixedString64Bytes(spawn.entityId ?? $"carrier-{_spawnedEntities.Count}");
            var carrierSpeed = 3f;
            var carrierAcceleration = 0.4f;
            var carrierDeceleration = 0.6f;
            var carrierTurnSpeed = 0.25f;
            var carrierSlowdown = 20f;
            var carrierArrival = 3f;
            if (_useSmokeMotionTuning)
            {
                carrierSpeed = 7.2f;
                carrierAcceleration = 0.3f;
                carrierDeceleration = 0.45f;
                carrierTurnSpeed = 0.6f;
                carrierSlowdown = 30f;
                carrierArrival = 4f;
            }
            if (_isCollisionScenario)
            {
                carrierArrival = math.max(carrierArrival, 16f);
            }

            EntityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = carrierId,
                AffiliationEntity = Entity.Null,
                Speed = carrierSpeed,
                Acceleration = carrierAcceleration,
                Deceleration = carrierDeceleration,
                TurnSpeed = carrierTurnSpeed,
                SlowdownDistance = carrierSlowdown,
                ArrivalDistance = carrierArrival,
                PatrolCenter = position,
                PatrolRadius = 50f
            });

            var isHostile = spawn.components?.Combat?.isHostile ?? false;
            var scenarioSide = (byte)(isHostile ? 1 : 0);
            EntityManager.AddComponentData(entity, new ScenarioSide
            {
                Side = scenarioSide
            });
            var law = isHostile ? -0.7f : 0.7f;
            EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
            AddScenarioAffiliation(entity, scenarioSide);
            var carrierDisposition = ResolveDisposition(spawn.disposition, BuildCarrierDisposition(spawn.components?.Combat, isHostile));
            if (carrierDisposition != EntityDispositionFlags.None)
            {
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = carrierDisposition
                });
            }

            EnsureCarrierAuthorityAndCrew(entity, law, currentTick);
            ApplyCrewTemplate(entity, carrierId.ToString(), law, currentTick);

            EntityManager.AddComponentData(entity, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero,
                WaitTime = 3f,
                WaitTimer = 0f
            });

            EntityManager.AddComponentData(entity, new MovementCommand
            {
                TargetPosition = float3.zero,
                ArrivalThreshold = 2f
            });

            EntityManager.AddComponentData(entity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = carrierSpeed,
                CurrentSpeed = 0f,
                Acceleration = carrierAcceleration,
                Deceleration = carrierDeceleration,
                TurnSpeed = carrierTurnSpeed,
                SlowdownDistance = carrierSlowdown,
                ArrivalDistance = carrierArrival,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            EntityManager.AddComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            EntityManager.AddComponentData(entity, new EntityIntent
            {
                Mode = IntentMode.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                TriggeringInterrupt = InterruptType.None,
                IntentSetTick = 0,
                Priority = InterruptPriority.Low,
                IsValid = 0
            });

            EntityManager.AddBuffer<Interrupt>(entity);

            EntityManager.AddComponentData(entity, new VesselPhysicalProperties
            {
                Radius = 2.6f,
                BaseMass = 120f,
                HullDensity = 1.2f,
                CargoMassPerUnit = 0.02f,
                Restitution = 0.08f,
                TangentialDamping = 0.25f
            });

            EntityManager.AddComponentData(entity, DockingCapacity.MiningCarrier);
            EntityManager.AddBuffer<DockedEntity>(entity);

            // Add ResourceStorage buffer
            var storageBuffer = EntityManager.AddBuffer<ResourceStorage>(entity);
            if (spawn.components?.ResourceStorage != null)
            {
                foreach (var storage in spawn.components.ResourceStorage)
                {
                    var resourceType = ParseResourceType(storage.type);
                    storageBuffer.Add(ResourceStorage.Create(resourceType, storage.capacity));
                }
            }
            else
            {
                // Default storage
                storageBuffer.Add(ResourceStorage.Create(ResourceType.Food, 10000f));
                storageBuffer.Add(ResourceStorage.Create(ResourceType.Water, 10000f));
                storageBuffer.Add(ResourceStorage.Create(ResourceType.Supplies, 10000f));
                storageBuffer.Add(ResourceStorage.Create(ResourceType.Fuel, 10000f));
                storageBuffer.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));
                storageBuffer.Add(ResourceStorage.Create(ResourceType.RareMetals, 10000f));
            }

            var hasShipLoadout = TryApplySpawnShipDefinition(entity, spawn, DefaultModuleLoadoutKind.Carrier);
            if (!hasShipLoadout)
            {
                EnsureDefaultModuleLoadout(entity, DefaultModuleLoadoutKind.Carrier);
            }

            var fleetData = spawn.components?.Fleet;
            var combatData = spawn.components?.Combat;
            if (fleetData != null || combatData != null)
            {
                var fleetId = string.IsNullOrWhiteSpace(fleetData?.fleetId)
                    ? carrierId
                    : new FixedString64Bytes(fleetData.fleetId);

                var shipCount = fleetData?.shipCount > 0 ? fleetData.shipCount : 1;
                var posture = ParseFleetPosture(fleetData?.posture);

                EntityManager.AddComponentData(entity, new Space4XFleet
                {
                    FleetId = fleetId,
                    ShipCount = shipCount,
                    Posture = posture,
                    TaskForce = 0
                });

                EntityManager.AddComponentData(entity, new FleetMovementBroadcast
                {
                    Position = position,
                    Velocity = float3.zero,
                    LastUpdateTick = currentTick,
                    AllowsInterception = 1,
                    TechTier = 1
                });

                if (combatData != null && combatData.canIntercept)
                {
                    var interceptSpeed = combatData.interceptSpeed > 0f ? combatData.interceptSpeed : 10f;
                    EntityManager.AddComponentData(entity, new InterceptCapability
                    {
                        MaxSpeed = interceptSpeed,
                        TechTier = 1,
                        AllowIntercept = 1
                    });
                }
            }

            SpawnStrikeCraft(entity, position, combatData, scenarioSide);
            SpawnEscorts(entity, position, combatData, scenarioSide, currentTick, fixedDt);

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void SpawnMiningVessel(MiningSpawnDefinition spawn, uint currentTick)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);
            EntityManager.AddComponent<CommunicationModuleTag>(entity);
            EntityManager.AddComponentData(entity, MediumContext.Vacuum);

            // Find carrier entity
            Entity carrierEntity = Entity.Null;
            if (!string.IsNullOrEmpty(spawn.carrierId) && _spawnedEntities.TryGetValue(spawn.carrierId, out var carrier))
            {
                carrierEntity = carrier;
            }

            byte scenarioSide = 0;
            if (carrierEntity != Entity.Null && EntityManager.HasComponent<ScenarioSide>(carrierEntity))
            {
                scenarioSide = EntityManager.GetComponentData<ScenarioSide>(carrierEntity).Side;
            }

            EntityManager.AddComponentData(entity, new ScenarioSide
            {
                Side = scenarioSide
            });

            var law = scenarioSide == 1 ? -0.7f : 0.7f;
            EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
            AddScenarioAffiliation(entity, scenarioSide);

            var resourceId = new FixedString64Bytes(spawn.resourceId ?? "Minerals");
            var miningEfficiency = spawn.miningEfficiency > 0f ? spawn.miningEfficiency : 0.8f;
            var speed = spawn.speed > 0f ? spawn.speed : 5f;
            if (_useSmokeMotionTuning)
            {
                speed = math.max(1.8f, speed * 0.45f);
            }
            var cargoCapacity = spawn.cargoCapacity > 0f ? spawn.cargoCapacity : 100f;

            EntityManager.AddComponentData(entity, new MiningVessel
            {
                VesselId = new FixedString64Bytes(spawn.entityId ?? $"miner-{_spawnedEntities.Count}"),
                CarrierEntity = carrierEntity,
                MiningEfficiency = math.clamp(miningEfficiency, 0f, 1f),
                Speed = speed,
                CargoCapacity = cargoCapacity,
                CurrentCargo = 0f,
                CargoResourceType = ParseResourceType(spawn.resourceId ?? "Minerals")
            });

            var hasShipLoadout = TryApplySpawnShipDefinition(entity, spawn, DefaultModuleLoadoutKind.MiningVessel);
            if (!hasShipLoadout)
            {
                EnsureDefaultModuleLoadout(entity, DefaultModuleLoadoutKind.MiningVessel);
            }

            var toolKind = ParseMiningToolKind(spawn.toolKind);
            EntityManager.AddComponentData(entity, new Space4XMiningToolProfile
            {
                ToolKind = toolKind,
                HasShapeOverride = spawn.toolShapeOverride || !string.IsNullOrWhiteSpace(spawn.toolShape) ? (byte)1 : (byte)0,
                Shape = ParseMiningToolShape(spawn.toolShape),
                RadiusOverride = spawn.toolRadiusOverride,
                RadiusMultiplier = spawn.toolRadiusMultiplier,
                StepLengthOverride = spawn.toolStepLengthOverride,
                StepLengthMultiplier = spawn.toolStepLengthMultiplier,
                DigUnitsPerMeterOverride = spawn.toolDigUnitsPerMeterOverride,
                MinStepLengthOverride = spawn.toolMinStepLengthOverride,
                MaxStepLengthOverride = spawn.toolMaxStepLengthOverride,
                YieldMultiplier = spawn.toolYieldMultiplier,
                HeatDeltaMultiplier = spawn.toolHeatDeltaMultiplier,
                InstabilityDeltaMultiplier = spawn.toolInstabilityDeltaMultiplier,
                DamageDeltaOverride = (byte)math.clamp(spawn.toolDamageDeltaOverride, 0, 255),
                DamageThresholdOverride = (byte)math.clamp(spawn.toolDamageThresholdOverride, 0, 255)
            });

            var vesselDisposition = ResolveDisposition(spawn.disposition, BuildMiningDisposition(scenarioSide));
            if (vesselDisposition != EntityDispositionFlags.None)
            {
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = vesselDisposition
                });
            }

            var pilotProfile = FindIndividualProfile(spawn.pilotProfileId);
            var pilot = CreatePilotEntity(law, pilotProfile);
            EntityManager.AddComponentData(entity, new VesselPilotLink
            {
                Pilot = pilot
            });

            EntityManager.AddComponentData(entity, new MiningOrder
            {
                ResourceId = resourceId,
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = 0
            });

            EntityManager.AddComponentData(entity, new MiningState
            {
                Phase = MiningPhase.Idle,
                ActiveTarget = Entity.Null,
                MiningTimer = 0f,
                TickInterval = 0.5f,
                PhaseTimer = 0f
            });

            EntityManager.AddComponentData(entity, new MiningYield
            {
                ResourceId = resourceId,
                PendingAmount = 0f,
                SpawnThreshold = math.max(1f, cargoCapacity * 0.25f),
                SpawnReady = 0
            });

            EntityManager.AddComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Mining,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            var minerAcceleration = math.max(1f, speed * 0.8f);
            var minerDeceleration = math.max(1f, speed * 1.1f);
            var minerTurnSpeed = 2.4f;
            var minerSlowdown = 6f;
            var minerArrival = 1.5f;
            if (_useSmokeMotionTuning)
            {
                minerAcceleration = math.max(0.6f, speed * 0.6f);
                minerDeceleration = math.max(0.7f, speed * 0.8f);
                minerTurnSpeed = 1.2f;
                minerSlowdown = 10f;
                minerArrival = 1.8f;
            }

            EntityManager.AddComponentData(entity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                Acceleration = minerAcceleration,
                Deceleration = minerDeceleration,
                TurnSpeed = minerTurnSpeed,
                SlowdownDistance = minerSlowdown,
                ArrivalDistance = minerArrival,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            EntityManager.AddComponentData(entity, new VesselPhysicalProperties
            {
                Radius = 0.6f,
                BaseMass = 6f,
                HullDensity = 1.05f,
                CargoMassPerUnit = 0.04f,
                Restitution = 0.15f,
                TangentialDamping = 0.3f
            });

            EntityManager.AddBuffer<SpawnResourceRequest>(entity);

            if (spawn.startDocked && carrierEntity != Entity.Null)
            {
                EntityManager.AddComponentData(entity, new DockingRequest
                {
                    TargetCarrier = carrierEntity,
                    RequiredSlot = DockingSlotType.Utility,
                    RequestTick = currentTick,
                    Priority = 0
                });
            }

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void SpawnResourceDeposit(MiningSpawnDefinition spawn)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);

            var resourceId = new FixedString64Bytes(spawn.resourceId ?? "Minerals");
            var resourceType = ParseResourceType(spawn.resourceId ?? "Minerals");
            var unitsRemaining = spawn.unitsRemaining > 0f ? spawn.unitsRemaining : 1000f;
            var gatherRate = spawn.gatherRatePerWorker > 0f ? spawn.gatherRatePerWorker : 10f;
            var maxWorkers = spawn.maxSimultaneousWorkers > 0 ? spawn.maxSimultaneousWorkers : 3;

            // Add Asteroid component for registry registration
            var asteroidId = spawn.entityId ?? $"asteroid-{_spawnedEntities.Count}";
            EntityManager.AddComponentData(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes(asteroidId),
                ResourceAmount = unitsRemaining,
                MaxResourceAmount = unitsRemaining,
                ResourceType = resourceType,
                MiningRate = gatherRate
            });

            EntityManager.AddComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = unitsRemaining,
                LastHarvestTick = 0
            });

            EntityManager.AddComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = gatherRate,
                MaxSimultaneousWorkers = (ushort)maxWorkers,
                RespawnSeconds = 0f,
                Flags = 0
            });

            EntityManager.AddComponentData(entity, new ResourceTypeId
            {
                Value = resourceId
            });

            // Add rewind support
            EntityManager.AddComponent<RewindableTag>(entity);
            EntityManager.AddComponentData(entity, new LastRecordedTick { Tick = 0 });
            EntityManager.AddComponentData(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.LowVisibility,
                OverrideStrideSeconds = 0f
            });
            EntityManager.AddBuffer<ResourceHistorySample>(entity);
            EntityManager.AddBuffer<Space4XMiningLatchReservation>(entity);

            var volumeConfig = Space4XAsteroidVolumeConfig.Default;
            volumeConfig.Radius = math.max(0.1f, volumeConfig.Radius);
            EntityManager.AddComponentData(entity, volumeConfig);

            EntityManager.AddComponentData(entity, new Space4XAsteroidCenter
            {
                Position = position
            });

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void ScheduleScenarioActions(Entity runtimeEntity, uint startTick, float fixedDt)
        {
            if (_scenarioData.actions == null || _scenarioData.actions.Count == 0)
            {
                return;
            }

            if (EntityManager.HasBuffer<Space4XScenarioAction>(runtimeEntity))
            {
                return;
            }

            var actionsBuffer = EntityManager.AddBuffer<Space4XScenarioAction>(runtimeEntity);
            var safeDt = math.max(1e-6f, fixedDt);

            foreach (var action in _scenarioData.actions)
            {
                if (action == null || string.IsNullOrWhiteSpace(action.kind))
                {
                    continue;
                }

                var kind = ParseScenarioActionKind(action.kind);
                var executeTick = startTick + (uint)math.ceil(math.max(0f, action.time_s) / safeDt);
                var targetPosition = GetPosition(action.targetPosition);

                actionsBuffer.Add(new Space4XScenarioAction
                {
                    ExecuteTick = executeTick,
                    Kind = kind,
                    FleetId = new FixedString64Bytes(action.fleetId ?? action.requesterFleetId ?? string.Empty),
                    TargetFleetId = new FixedString64Bytes(action.targetFleetId ?? string.Empty),
                    TargetPosition = targetPosition,
                    BusinessId = new FixedString64Bytes(action.businessId ?? string.Empty),
                    ItemId = new FixedString64Bytes(action.itemId ?? string.Empty),
                    RecipeId = new FixedString64Bytes(action.recipeId ?? string.Empty),
                    Quantity = action.quantity,
                    Capacity = action.capacity,
                    BusinessType = (byte)ParseBusinessType(action.businessType),
                    Executed = 0
                });
            }
        }

        private void EnsureCollisionScenarioTag()
        {
            if (SystemAPI.HasSingleton<Space4XCollisionScenarioTag>())
            {
                return;
            }

            var entity = EntityManager.CreateEntity(typeof(Space4XCollisionScenarioTag));
            EntityManager.SetName(entity, "Space4XCollisionScenarioTag");
        }

        private void SpawnStrikeCraft(Entity carrierEntity, float3 carrierPosition, CombatComponentData combatData, byte scenarioSide)
        {
            if (combatData == null || combatData.strikeCraftCount <= 0)
            {
                return;
            }

            var role = ParseStrikeCraftRole(combatData.strikeCraftRole);
            var craftSpeed = 12f;
            var weaponDamage = 10f;
            var weaponRange = 20f;
            var hull = HullIntegrity.LightCraft;

            switch (role)
            {
                case StrikeCraftRole.Bomber:
                    craftSpeed = 9f;
                    weaponDamage = 50f;
                    weaponRange = 18f;
                    hull = HullIntegrity.Create(80f, 0.15f);
                    break;
                case StrikeCraftRole.Interceptor:
                    craftSpeed = 14f;
                    weaponDamage = 12f;
                    weaponRange = 22f;
                    hull = HullIntegrity.Create(40f, 0.1f);
                    break;
                case StrikeCraftRole.Suppression:
                    craftSpeed = 10f;
                    weaponDamage = 20f;
                    weaponRange = 25f;
                    hull = HullIntegrity.Create(120f, 0.2f);
                    break;
                case StrikeCraftRole.Recon:
                    craftSpeed = 15f;
                    weaponDamage = 6f;
                    weaponRange = 24f;
                    hull = HullIntegrity.Create(35f, 0.05f);
                    break;
            }

            for (int i = 0; i < combatData.strikeCraftCount; i++)
            {
                var entity = EntityManager.CreateEntity();
                var offset = new float3(2f * (i + 1), 0f, 2f * (i + 1));
                EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(carrierPosition + offset, quaternion.identity, 1f));
                EntityManager.AddComponent<CommunicationModuleTag>(entity);
                EntityManager.AddComponentData(entity, MediumContext.Vacuum);
                EntityManager.AddComponentData(entity, hull);
                EnsureDefaultSubsystems(entity, hull.Max);
                var weaponBuffer = EntityManager.AddBuffer<WeaponMount>(entity);
                weaponBuffer.Add(new WeaponMount
                {
                    Weapon = new Space4XWeapon
                    {
                        Type = WeaponType.Laser,
                        Size = WeaponSize.Small,
                        BaseDamage = weaponDamage,
                        OptimalRange = weaponRange * 0.8f,
                        MaxRange = weaponRange,
                        BaseAccuracy = (half)0.85f,
                        CooldownTicks = 1,
                        CurrentCooldown = 0,
                        AmmoPerShot = 0,
                        ShieldModifier = (half)1f,
                        ArmorPenetration = (half)0.3f
                    },
                    CurrentTarget = Entity.Null,
                    IsEnabled = 1
                });
                EntityManager.AddComponentData(entity, StrikeCraftProfile.Create(role, carrierEntity));
                EntityManager.AddComponentData(entity, new StrikeCraftState
                {
                    CurrentState = StrikeCraftState.State.Approaching,
                    TargetEntity = Entity.Null,
                    TargetPosition = carrierPosition + offset,
                    Experience = 0f,
                    StateStartTick = 0,
                    KamikazeActive = 0,
                    KamikazeStartTick = 0,
                    DogfightPhase = StrikeCraftDogfightPhase.Approach,
                    DogfightPhaseStartTick = 0,
                    DogfightLastFireTick = 0,
                    DogfightWingLeader = Entity.Null
                });
                EntityManager.AddComponent<StrikeCraftDogfightTag>(entity);
                EntityManager.AddComponentData(entity, AttackRunConfig.ForRole(role));
                EntityManager.AddComponentData(entity, StrikeCraftExperience.Rookie);
                EntityManager.AddComponentData(entity, new ScenarioSide
                {
                    Side = scenarioSide
                });
                EntityManager.AddComponentData(entity, new VesselMovement
                {
                    BaseSpeed = craftSpeed,
                    CurrentSpeed = 0f,
                    Velocity = float3.zero,
                    TurnSpeed = 4.5f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = 0
                });
                EntityManager.AddComponentData(entity, SupplyStatus.DefaultStrikeCraft);
                var law = scenarioSide == 1 ? -0.7f : 0.7f;
                EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
                AddScenarioAffiliation(entity, scenarioSide);
                var craftDisposition = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
                if (scenarioSide == 1)
                {
                    craftDisposition |= EntityDispositionFlags.Hostile;
                }
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = craftDisposition
                });
                var pilotProfile = ResolvePilotProfile(combatData, i);
                var pilot = CreatePilotEntity(law, pilotProfile);
                EntityManager.AddComponentData(entity, new StrikeCraftPilotLink
                {
                    Pilot = pilot
                });
            }
        }

        private IndividualProfileData ResolvePilotProfile(CombatComponentData combatData, int index)
        {
            if (combatData == null || _scenarioData == null || _scenarioData.individuals == null)
            {
                return null;
            }

            string pilotId = null;
            if (combatData.pilotProfileIds != null && combatData.pilotProfileIds.Count > 0)
            {
                pilotId = combatData.pilotProfileIds[index % combatData.pilotProfileIds.Count];
            }
            else if (!string.IsNullOrWhiteSpace(combatData.pilotProfileId))
            {
                pilotId = combatData.pilotProfileId;
            }

            if (string.IsNullOrWhiteSpace(pilotId))
            {
                return null;
            }
            return FindIndividualProfile(pilotId);
        }

        private Entity CreatePilotEntity(float lawfulness, IndividualProfileData profileData)
        {
            var config = StrikeCraftPilotProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftPilotProfileConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var pilot = EntityManager.CreateEntity();
            if (profileData != null)
            {
                EntityManager.AddComponentData(pilot, AlignmentTriplet.FromFloats(
                    math.clamp(profileData.law, -1f, 1f),
                    math.clamp(profileData.good, -1f, 1f),
                    math.clamp(profileData.integrity, -1f, 1f)));
                if (profileData.raceId > 0)
                {
                    EntityManager.AddComponentData(pilot, new RaceId { Value = profileData.raceId });
                }
                if (profileData.cultureId > 0)
                {
                    EntityManager.AddComponentData(pilot, new CultureId { Value = profileData.cultureId });
                }
            }
            else
            {
                EntityManager.AddComponentData(pilot, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));
            }

            if (!string.IsNullOrWhiteSpace(profileData?.id))
            {
                var profileId = new FixedString64Bytes(profileData.id);
                EntityManager.AddComponentData(pilot, new IndividualProfileId { Id = profileId });
                if (_profileEntities != null && !_profileEntities.ContainsKey(profileData.id))
                {
                    _profileEntities[profileData.id] = pilot;
                }
            }

            EntityManager.AddBuffer<StanceEntry>(pilot);
            EntityManager.AddBuffer<TopStance>(pilot);
            var stanceEntries = EntityManager.GetBuffer<StanceEntry>(pilot);
            var topStances = EntityManager.GetBuffer<TopStance>(pilot);
            var stanceWeights = profileData?.ResolveStances();
            if (stanceWeights != null && stanceWeights.Count > 0)
            {
                for (int i = 0; i < stanceWeights.Count; i++)
                {
                    var entry = stanceWeights[i];
                    stanceEntries.Add(new StanceEntry
                    {
                        StanceId = ParseStanceId(entry.ResolveStanceId()),
                        Weight = (half)math.clamp(entry.weight, -1f, 1f)
                    });
                }

                var ordered = stanceWeights
                    .OrderByDescending(o => o.weight)
                    .Take(3);
                foreach (var entry in ordered)
                {
                    topStances.Add(new TopStance
                    {
                        StanceId = ParseStanceId(entry.ResolveStanceId()),
                        Weight = (half)math.clamp(entry.weight, 0f, 1f)
                    });
                }
            }
            else
            {
                var StanceId = config.NeutralStance;
                if (lawfulness >= config.LoyalistLawThreshold)
                {
                    StanceId = config.FriendlyStance;
                }
                else if (lawfulness <= config.MutinousLawThreshold)
                {
                    StanceId = config.HostileStance;
                }

                stanceEntries.Add(new StanceEntry
                {
                    StanceId = StanceId,
                    Weight = (half)1f
                });
                topStances.Add(new TopStance
                {
                    StanceId = StanceId,
                    Weight = (half)1f
                });
            }

            var dispositionData = profileData?.behaviorDisposition;
            if (dispositionData != null && dispositionData.enabled)
            {
                EntityManager.AddComponentData(pilot, BehaviorDisposition.FromValues(
                    dispositionData.compliance,
                    dispositionData.caution,
                    dispositionData.formationAdherence,
                    dispositionData.riskTolerance,
                    dispositionData.aggression,
                    dispositionData.patience));
            }
            else
            {
                EntityManager.AddComponentData(pilot, new BehaviorDispositionSeedRequest
                {
                    Seed = 0u,
                    SeedSalt = 0u
                });
            }

            return pilot;
        }

        private void ApplyPersonalRelations(uint currentTick)
        {
            if (_scenarioData == null || _scenarioData.personalRelations == null || _scenarioData.personalRelations.Count == 0)
            {
                return;
            }

            foreach (var relation in _scenarioData.personalRelations)
            {
                if (relation == null ||
                    string.IsNullOrWhiteSpace(relation.idA) ||
                    string.IsNullOrWhiteSpace(relation.idB))
                {
                    continue;
                }

                if (_profileEntities == null ||
                    !_profileEntities.TryGetValue(relation.idA, out var entityA) ||
                    !_profileEntities.TryGetValue(relation.idB, out var entityB))
                {
                    Debug.LogWarning($"[Space4XMiningScenario] PersonalRelation missing entities idA='{relation.idA}' idB='{relation.idB}'.");
                    continue;
                }

                var kind = ParsePersonalRelationKind(relation.kind);
                var score = (sbyte)math.clamp((int)math.round(relation.score), -100, 100);
                var trust = (half)math.saturate(relation.trust);
                var fear = (half)math.saturate(relation.fear);

                UpsertPersonalRelation(entityA, entityB, score, kind, trust, fear, currentTick);
                UpsertPersonalRelation(entityB, entityA, score, kind, trust, fear, currentTick);
            }
        }

        private void UpsertPersonalRelation(Entity source, Entity other, sbyte score, PersonalRelationKind kind, half trust, half fear, uint tick)
        {
            if (!EntityManager.HasBuffer<PersonalRelationEntry>(source))
            {
                EntityManager.AddBuffer<PersonalRelationEntry>(source);
            }

            var buffer = EntityManager.GetBuffer<PersonalRelationEntry>(source);
            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                if (entry.Other == other)
                {
                    entry.Score = score;
                    entry.Kind = kind;
                    entry.Trust = trust;
                    entry.Fear = fear;
                    entry.LastInteractionTick = tick;
                    buffer[i] = entry;
                    return;
                }
            }

            buffer.Add(new PersonalRelationEntry
            {
                Other = other,
                Score = score,
                Kind = kind,
                Trust = trust,
                Fear = fear,
                LastInteractionTick = tick
            });
        }

        private void EnsureScenarioFactionProfile(Entity affiliationEntity, byte scenarioSide)
        {
            if (affiliationEntity == Entity.Null)
            {
                return;
            }

            var outlook = ResolveScenarioOutlook(scenarioSide);
            var factionId = scenarioSide == 1 ? HostileFactionId : FriendlyFactionId;

            if (EntityManager.HasComponent<Space4XFaction>(affiliationEntity))
            {
                var faction = EntityManager.GetComponentData<Space4XFaction>(affiliationEntity);
                if (faction.FactionId == 0)
                {
                    faction.FactionId = factionId;
                }
                if (outlook != FactionOutlook.None)
                {
                    faction.Outlook = outlook;
                }
                EntityManager.SetComponentData(affiliationEntity, faction);
                return;
            }

            var profile = Space4XFaction.Empire(factionId, outlook);
            EntityManager.AddComponentData(affiliationEntity, profile);
        }

        private FactionOutlook ResolveScenarioOutlook(byte scenarioSide)
        {
            var config = _scenarioData?.scenarioConfig;
            if (config == null)
            {
                return FactionOutlook.None;
            }

            var tokens = scenarioSide == 1 ? config.hostileFactionOutlook : config.friendlyFactionOutlook;
            return ParseFactionOutlook(tokens);
        }

        private static FactionOutlook ParseFactionOutlook(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return FactionOutlook.None;
            }

            var outlook = FactionOutlook.None;
            for (int i = 0; i < values.Count; i++)
            {
                var token = values[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                switch (token.Trim())
                {
                    case "Expansionist":
                        outlook |= FactionOutlook.Expansionist;
                        break;
                    case "Isolationist":
                        outlook |= FactionOutlook.Isolationist;
                        break;
                    case "Militarist":
                        outlook |= FactionOutlook.Militarist;
                        break;
                    case "Pacifist":
                        outlook |= FactionOutlook.Pacifist;
                        break;
                    case "Materialist":
                        outlook |= FactionOutlook.Materialist;
                        break;
                    case "Spiritualist":
                        outlook |= FactionOutlook.Spiritualist;
                        break;
                    case "Xenophile":
                        outlook |= FactionOutlook.Xenophile;
                        break;
                    case "Xenophobe":
                        outlook |= FactionOutlook.Xenophobe;
                        break;
                    case "Egalitarian":
                        outlook |= FactionOutlook.Egalitarian;
                        break;
                    case "Authoritarian":
                        outlook |= FactionOutlook.Authoritarian;
                        break;
                    case "Corrupt":
                        outlook |= FactionOutlook.Corrupt;
                        break;
                    case "Honorable":
                        outlook |= FactionOutlook.Honorable;
                        break;
                }
            }

            return outlook;
        }

        private void EnsureScenarioFactionRelations(uint currentTick)
        {
            if (_friendlyAffiliationEntity == Entity.Null || _hostileAffiliationEntity == Entity.Null)
            {
                return;
            }

            EnsureScenarioFactionProfile(_friendlyAffiliationEntity, 0);
            EnsureScenarioFactionProfile(_hostileAffiliationEntity, 1);

            EnsureFactionRelation(_friendlyAffiliationEntity, _hostileAffiliationEntity, -80, currentTick);
            EnsureFactionRelation(_hostileAffiliationEntity, _friendlyAffiliationEntity, -80, currentTick);
        }

        private void EnsureFactionRelation(Entity selfFaction, Entity otherFaction, sbyte score, uint tick)
        {
            if (!EntityManager.HasComponent<Space4XFaction>(selfFaction) ||
                !EntityManager.HasComponent<Space4XFaction>(otherFaction))
            {
                return;
            }

            if (!EntityManager.HasBuffer<FactionRelationEntry>(selfFaction))
            {
                EntityManager.AddBuffer<FactionRelationEntry>(selfFaction);
            }

            var otherProfile = EntityManager.GetComponentData<Space4XFaction>(otherFaction);
            var buffer = EntityManager.GetBuffer<FactionRelationEntry>(selfFaction);

            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                if (entry.Relation.OtherFaction == otherFaction || entry.Relation.OtherFactionId == otherProfile.FactionId)
                {
                    entry.Relation.OtherFaction = otherFaction;
                    entry.Relation.OtherFactionId = otherProfile.FactionId;
                    entry.Relation.Score = score;
                    entry.Relation.LastInteractionTick = tick;
                    buffer[i] = entry;
                    return;
                }
            }

            buffer.Add(new FactionRelationEntry
            {
                Relation = new FactionRelation
                {
                    OtherFaction = otherFaction,
                    OtherFactionId = otherProfile.FactionId,
                    Score = score,
                    Trust = (half)0f,
                    Fear = (half)0.5f,
                    Respect = (half)0f,
                    TradeVolume = 0f,
                    RecentCombats = 0,
                    LastInteractionTick = tick
                }
            });
        }

        private void SpawnEscorts(Entity carrierEntity, float3 carrierPosition, CombatComponentData combatData, byte scenarioSide, uint currentTick, float fixedDt)
        {
            if (combatData == null || combatData.escortCount <= 0)
            {
                return;
            }

            var releaseSeconds = combatData.escortRelease_s > 0f ? combatData.escortRelease_s : 30f;
            var releaseTicks = (uint)math.ceil(releaseSeconds / math.max(1e-6f, fixedDt));
            var releaseTick = currentTick + math.max(1u, releaseTicks);

            for (int i = 0; i < combatData.escortCount; i++)
            {
                var entity = EntityManager.CreateEntity();
                var offset = new float3(3f * (i + 1), 0f, -3f * (i + 1));
                EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(carrierPosition + offset, quaternion.identity, 1f));
                EntityManager.AddComponent<SpatialIndexedTag>(entity);
                EntityManager.AddComponent<CommunicationModuleTag>(entity);
                EntityManager.AddComponentData(entity, MediumContext.Vacuum);

                EntityManager.AddComponentData(entity, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = 6f,
                    CurrentSpeed = 0f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = currentTick
                });

                EntityManager.AddComponentData(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.MovingToTarget,
                    CurrentGoal = VesselAIState.Goal.Escort,
                    TargetEntity = carrierEntity,
                    TargetPosition = float3.zero,
                    StateTimer = 0f,
                    StateStartTick = currentTick
                });

                EntityManager.AddComponentData(entity, new ChildVesselTether
                {
                    ParentCarrier = carrierEntity,
                    MaxTetherRange = 45f,
                    CanPatrol = 0
                });

                EntityManager.AddComponentData(entity, new EscortAssignment
                {
                    Target = carrierEntity,
                    AssignedTick = currentTick,
                    ReleaseTick = releaseTick,
                    Released = 0
                });

                EntityManager.AddComponentData(entity, new ScenarioSide
                {
                    Side = scenarioSide
                });
                var law = scenarioSide == 1 ? -0.7f : 0.7f;
                EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
                AddScenarioAffiliation(entity, scenarioSide);
                var escortDisposition = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
                if (scenarioSide == 1)
                {
                    escortDisposition |= EntityDispositionFlags.Hostile;
                }
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = escortDisposition
                });
                var hasEscortLoadout = TryApplyEscortShipDefinition(entity, combatData, DefaultModuleLoadoutKind.Escort);
                if (!hasEscortLoadout)
                {
                    EnsureDefaultModuleLoadout(entity, DefaultModuleLoadoutKind.Escort);
                }
            }
        }

        private enum DefaultModuleLoadoutKind : byte
        {
            Carrier,
            MiningVessel,
            Escort
        }

        private bool TryApplySpawnShipDefinition(Entity owner, MiningSpawnDefinition spawn, DefaultModuleLoadoutKind loadoutKind)
        {
            var ship = ResolveShipDefinition(spawn);
            return ApplyShipDefinition(owner, ship, loadoutKind);
        }

        private bool TryApplyEscortShipDefinition(Entity owner, CombatComponentData combatData, DefaultModuleLoadoutKind loadoutKind)
        {
            var ship = ResolveEscortShipDefinition(combatData);
            return ApplyShipDefinition(owner, ship, loadoutKind);
        }

        private void EnsureDefaultModuleLoadout(Entity owner, DefaultModuleLoadoutKind loadoutKind)
        {
            if (!_applyDefaultModuleLoadouts)
            {
                return;
            }

            if (EntityManager.HasComponent<CarrierModuleSlot>(owner))
            {
                return;
            }

            EnsureModuleOwnerState(owner, loadoutKind);

            EntityManager.AddBuffer<CarrierModuleSlot>(owner);
            if (!EntityManager.HasBuffer<ModuleAttachment>(owner))
            {
                EntityManager.AddBuffer<ModuleAttachment>(owner);
            }

            var slots = EntityManager.GetBuffer<CarrierModuleSlot>(owner);
            var attachments = EntityManager.GetBuffer<ModuleAttachment>(owner);
            slots.Clear();
            attachments.Clear();

            switch (loadoutKind)
            {
                case DefaultModuleLoadoutKind.Carrier:
                    AddModuleSlot(owner, "reactor-mk2", out _);
                    AddModuleSlot(owner, "engine-mk2", out _);
                    AddModuleSlot(owner, "bridge-mk1", out _);
                    AddModuleSlot(owner, "cockpit-mk1", out _);
                    AddModuleSlot(owner, "shield-m-1", out _);
                    AddModuleSlot(owner, "armor-s-1", out _);
                    AddModuleSlot(owner, "scanner-s-1", out _);
                    AddModuleSlot(owner, "laser-s-1", out _);
                    AddModuleSlot(owner, "pd-s-1", out _);
                    AddModuleSlot(owner, "missile-m-1", out _);
                    AddModuleSlot(owner, "ammo-bay-s-1", out _);
                    break;
                case DefaultModuleLoadoutKind.MiningVessel:
                    AddModuleSlot(owner, "reactor-mk1", out _);
                    AddModuleSlot(owner, "engine-mk1", out _);
                    AddModuleSlot(owner, "bridge-mk1", out _);
                    AddModuleSlot(owner, "cockpit-mk1", out _);
                    AddModuleSlot(owner, "shield-s-1", out _);
                    AddModuleSlot(owner, "armor-s-1", out _);
                    AddModuleSlot(owner, "scanner-s-1", out _);
                    AddModuleSlot(owner, "pd-s-1", out _);
                    AddModuleSlot(owner, "ammo-bay-s-1", out _);
                    break;
                case DefaultModuleLoadoutKind.Escort:
                    AddModuleSlot(owner, "reactor-mk1", out _);
                    AddModuleSlot(owner, "engine-mk1", out _);
                    AddModuleSlot(owner, "bridge-mk1", out _);
                    AddModuleSlot(owner, "cockpit-mk1", out _);
                    AddModuleSlot(owner, "shield-s-1", out _);
                    AddModuleSlot(owner, "armor-s-1", out _);
                    AddModuleSlot(owner, "scanner-s-1", out _);
                    AddModuleSlot(owner, "laser-s-1", out _);
                    AddModuleSlot(owner, "pd-s-1", out _);
                    AddModuleSlot(owner, "missile-s-1", out _);
                    AddModuleSlot(owner, "ammo-bay-s-1", out _);
                    break;
            }
        }

        private void EnsureModuleOwnerState(Entity owner, DefaultModuleLoadoutKind loadoutKind)
        {
            if (!EntityManager.HasComponent<ModuleStatAggregate>(owner))
            {
                EntityManager.AddComponentData(owner, new ModuleStatAggregate
                {
                    SpeedMultiplier = 1f,
                    CargoMultiplier = 1f,
                    EnergyMultiplier = 1f,
                    RefitRateMultiplier = 1f,
                    RepairRateMultiplier = 1f,
                    ActiveModuleCount = 0
                });
            }

            if (!EntityManager.HasComponent<SupplyStatus>(owner))
            {
                var supply = loadoutKind == DefaultModuleLoadoutKind.Carrier
                    ? SupplyStatus.DefaultCarrier
                    : SupplyStatus.DefaultVessel;
                EntityManager.AddComponentData(owner, supply);
            }
        }

        private bool ApplyShipDefinition(Entity owner, ShipDefinitionData ship, DefaultModuleLoadoutKind loadoutKind)
        {
            if (ship == null)
            {
                return false;
            }

            ApplyHullId(owner, ship);

            if (ship.modules == null || ship.modules.Count == 0)
            {
                return false;
            }

            EnsureModuleOwnerState(owner, loadoutKind);

            if (EntityManager.HasBuffer<CarrierModuleSlot>(owner))
            {
                if (EntityManager.GetBuffer<CarrierModuleSlot>(owner).Length > 0)
                {
                    return false;
                }
            }
            else
            {
                EntityManager.AddBuffer<CarrierModuleSlot>(owner);
            }

            if (!EntityManager.HasBuffer<ModuleAttachment>(owner))
            {
                EntityManager.AddBuffer<ModuleAttachment>(owner);
            }

            var slots = EntityManager.GetBuffer<CarrierModuleSlot>(owner);
            var attachments = EntityManager.GetBuffer<ModuleAttachment>(owner);
            slots.Clear();
            attachments.Clear();

            var totalMass = 0f;
            var moduleCount = 0;

            for (int i = 0; i < ship.modules.Count; i++)
            {
                var moduleId = ship.modules[i];
                if (string.IsNullOrWhiteSpace(moduleId))
                {
                    continue;
                }

                if (AddModuleSlot(owner, moduleId, out var massTons))
                {
                    totalMass += massTons;
                    moduleCount++;
                }
            }

            if (ship.massCap > 0f && totalMass > ship.massCap + 0.01f)
            {
                Debug.LogWarning($"[Space4XMiningScenario] Ship mass cap exceeded (hull='{ship.hullId ?? "unknown"}' total={totalMass:F1} cap={ship.massCap:F1} modules={moduleCount}).");
            }

            return moduleCount > 0;
        }

        private void ApplyHullId(Entity owner, ShipDefinitionData ship)
        {
            if (ship == null || string.IsNullOrWhiteSpace(ship.hullId))
            {
                return;
            }

            var hullId = new FixedString64Bytes(ship.hullId);

            if (EntityManager.HasComponent<Carrier>(owner))
            {
                if (EntityManager.HasComponent<CarrierHullId>(owner))
                {
                    EntityManager.SetComponentData(owner, new CarrierHullId { HullId = hullId });
                }
                else
                {
                    EntityManager.AddComponentData(owner, new CarrierHullId { HullId = hullId });
                }
            }

            if (EntityManager.HasComponent<HullId>(owner))
            {
                EntityManager.SetComponentData(owner, new HullId { Id = hullId });
            }
            else
            {
                EntityManager.AddComponentData(owner, new HullId { Id = hullId });
            }
        }

        private bool AddModuleSlot(Entity owner, string moduleId, out float massTons)
        {
            var module = CreateModuleEntity(moduleId, out var slotSize, out massTons);
            if (module == Entity.Null)
            {
                massTons = 0f;
                return false;
            }

            var slots = EntityManager.GetBuffer<CarrierModuleSlot>(owner);
            var attachments = EntityManager.GetBuffer<ModuleAttachment>(owner);
            var slotIndex = slots.Length;
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = slotIndex,
                SlotSize = slotSize,
                CurrentModule = module,
                TargetModule = module,
                RefitProgress = 0f,
                State = Space4X.Registry.ModuleSlotState.Active
            });

            attachments.Add(new ModuleAttachment { Module = module });
            return true;
        }

        private Entity CreateModuleEntity(string moduleId, out ModuleSlotSize slotSize, out float massTons)
        {
            slotSize = ModuleSlotSize.Small;
            massTons = 0f;
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return Entity.Null;
            }

            var moduleIdFixed = new FixedString64Bytes(moduleId);
            var hasSpec = ModuleCatalogUtility.TryGetModuleSpec(EntityManager, moduleIdFixed, out var spec);
            if (hasSpec)
            {
                slotSize = ConvertMountSize(spec.RequiredSize);
                massTons = math.max(0f, spec.MassTons);
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new ModuleTypeId { Value = moduleIdFixed });
            EntityManager.AddComponentData(entity, new ModuleSlotRequirement
            {
                SlotSize = slotSize
            });

            EntityManager.AddComponentData(entity, new ModuleStatModifier
            {
                SpeedMultiplier = 1f,
                CargoMultiplier = 1f,
                EnergyMultiplier = 1f,
                RefitRateMultiplier = 1f,
                RepairRateMultiplier = 1f
            });

            var maxHealth = 100f;
            var efficiency = hasSpec ? math.clamp(spec.DefaultEfficiency, 0.1f, 1f) : 1f;
            EntityManager.AddComponentData(entity, new ModuleHealth
            {
                MaxHealth = maxHealth,
                CurrentHealth = maxHealth * efficiency,
                MaxFieldRepairHealth = maxHealth * 0.8f,
                DegradationPerSecond = 0f,
                RepairPriority = 128,
                Failed = 0
            });

            return entity;
        }

        private static ModuleSlotSize ConvertMountSize(MountSize size)
        {
            return size switch
            {
                MountSize.S => ModuleSlotSize.Small,
                MountSize.M => ModuleSlotSize.Medium,
                MountSize.L => ModuleSlotSize.Large,
                _ => ModuleSlotSize.Medium
            };
        }

        private ShipDefinitionData ResolveShipDefinition(MiningSpawnDefinition spawn)
        {
            if (spawn == null)
            {
                return null;
            }

            ShipDefinitionData templateShip = null;
            if (!string.IsNullOrWhiteSpace(spawn.shipTemplateId))
            {
                templateShip = ResolveShipTemplate(spawn.shipTemplateId);
            }

            if (spawn.ship == null)
            {
                return CloneShipDefinition(templateShip);
            }

            if (templateShip == null)
            {
                return CloneShipDefinition(spawn.ship);
            }

            return MergeShipDefinition(templateShip, spawn.ship);
        }

        private ShipDefinitionData ResolveEscortShipDefinition(CombatComponentData combatData)
        {
            if (combatData == null)
            {
                return null;
            }

            ShipDefinitionData templateShip = null;
            if (!string.IsNullOrWhiteSpace(combatData.escortShipTemplateId))
            {
                templateShip = ResolveShipTemplate(combatData.escortShipTemplateId);
            }

            if (combatData.escortShip == null)
            {
                return CloneShipDefinition(templateShip);
            }

            if (templateShip == null)
            {
                return CloneShipDefinition(combatData.escortShip);
            }

            return MergeShipDefinition(templateShip, combatData.escortShip);
        }

        private static ShipDefinitionData CloneShipDefinition(ShipDefinitionData source)
        {
            if (source == null)
            {
                return null;
            }

            return new ShipDefinitionData
            {
                hullId = source.hullId,
                massCap = source.massCap,
                modules = source.modules != null ? new List<string>(source.modules) : null
            };
        }

        private static ShipDefinitionData MergeShipDefinition(ShipDefinitionData baseDefinition, ShipDefinitionData overrideDefinition)
        {
            if (baseDefinition == null)
            {
                return CloneShipDefinition(overrideDefinition);
            }

            if (overrideDefinition == null)
            {
                return CloneShipDefinition(baseDefinition);
            }

            var merged = CloneShipDefinition(baseDefinition);

            if (!string.IsNullOrWhiteSpace(overrideDefinition.hullId))
            {
                merged.hullId = overrideDefinition.hullId;
            }

            if (overrideDefinition.massCap > 0f)
            {
                merged.massCap = overrideDefinition.massCap;
            }

            if (overrideDefinition.modules != null && overrideDefinition.modules.Count > 0)
            {
                merged.modules = new List<string>(overrideDefinition.modules);
            }

            return merged;
        }

        private void EnsureCarrierAuthorityAndCrew(Entity carrierEntity, float lawfulness, uint currentTick)
        {
            EntityManager.AddComponentData(carrierEntity, new CaptainOrder
            {
                Type = CaptainOrderType.None,
                Status = CaptainOrderStatus.None,
                Priority = 0,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                IssuedTick = currentTick,
                TimeoutTick = 0,
                IssuingAuthority = Entity.Null
            });
            EntityManager.AddComponentData(carrierEntity, CaptainState.Default);
            EntityManager.AddComponentData(carrierEntity, CaptainReadiness.Standard);

            var config = StrikeCraftPilotProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftPilotProfileConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var crewEntities = new Entity[6];
            crewEntities[0] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)90,
                    Tactics = (half)70,
                    Logistics = (half)60,
                    Diplomacy = (half)60,
                    Engineering = (half)40,
                    Resolve = (half)85
                },
                BehaviorDisposition.FromValues(0.8f, 0.6f, 0.8f, 0.4f, 0.45f, 0.7f));

            crewEntities[1] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)75,
                    Tactics = (half)55,
                    Logistics = (half)80,
                    Diplomacy = (half)50,
                    Engineering = (half)45,
                    Resolve = (half)70
                },
                BehaviorDisposition.FromValues(0.75f, 0.6f, 0.7f, 0.45f, 0.4f, 0.7f));

            crewEntities[2] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)65,
                    Tactics = (half)80,
                    Logistics = (half)50,
                    Diplomacy = (half)45,
                    Engineering = (half)40,
                    Resolve = (half)60
                },
                BehaviorDisposition.FromValues(0.65f, 0.55f, 0.7f, 0.5f, 0.45f, 0.6f));

            crewEntities[3] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)55,
                    Tactics = (half)75,
                    Logistics = (half)45,
                    Diplomacy = (half)60,
                    Engineering = (half)45,
                    Resolve = (half)55
                },
                BehaviorDisposition.FromValues(0.6f, 0.7f, 0.55f, 0.35f, 0.35f, 0.65f));

            crewEntities[4] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)70,
                    Tactics = (half)70,
                    Logistics = (half)55,
                    Diplomacy = (half)50,
                    Engineering = (half)45,
                    Resolve = (half)60
                },
                BehaviorDisposition.FromValues(0.7f, 0.55f, 0.65f, 0.5f, 0.6f, 0.55f));

            crewEntities[5] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)55,
                    Tactics = (half)55,
                    Logistics = (half)60,
                    Diplomacy = (half)45,
                    Engineering = (half)85,
                    Resolve = (half)55
                },
                BehaviorDisposition.FromValues(0.65f, 0.7f, 0.5f, 0.4f, 0.3f, 0.75f));

            var crew = EntityManager.AddBuffer<PlatformCrewMember>(carrierEntity);
            for (int i = 0; i < crewEntities.Length; i++)
            {
                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = crewEntities[i],
                    RoleId = 0
                });
            }
        }

        private void ApplyCrewTemplate(Entity carrierEntity, string carrierId, float lawfulness, uint currentTick)
        {
            if (_scenarioData?.scenarioConfig?.crewTemplates == null ||
                string.IsNullOrWhiteSpace(carrierId))
            {
                return;
            }

            CrewTemplateConfigData template = null;
            for (int i = 0; i < _scenarioData.scenarioConfig.crewTemplates.Count; i++)
            {
                var candidate = _scenarioData.scenarioConfig.crewTemplates[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.carrierId))
                {
                    continue;
                }

                if (string.Equals(candidate.carrierId, carrierId, StringComparison.OrdinalIgnoreCase))
                {
                    template = candidate;
                    break;
                }
            }

            var resolvedMembers = ResolveCrewTemplateMembers(template);
            if (resolvedMembers == null || resolvedMembers.Count == 0)
            {
                return;
            }

            var config = StrikeCraftPilotProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftPilotProfileConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var crewBuffer = EntityManager.HasBuffer<PlatformCrewMember>(carrierEntity)
                ? EntityManager.GetBuffer<PlatformCrewMember>(carrierEntity)
                : EntityManager.AddBuffer<PlatformCrewMember>(carrierEntity);
            crewBuffer.Clear();

            for (int i = 0; i < resolvedMembers.Count; i++)
            {
                var member = ResolveCrewMemberTemplate(resolvedMembers[i]);
                if (member == null)
                {
                    continue;
                }

                ResolveCrewPreset(member.statsPreset, member.skills, out var stats, out var disposition);
                var crewEntity = CreateCrewEntity(lawfulness, config, stats, disposition);
                EnsureCrewEntityId(crewEntity, member.name);
                EnsureCrewAnatomyPreset(crewEntity, member.anatomyPreset, member.conditions);
                EnsureCrewLodTier(crewEntity, (byte)Space4X.Runtime.Space4XEntityLodTierKind.Lod0);
                crewBuffer.Add(new PlatformCrewMember
                {
                    CrewEntity = crewEntity,
                    RoleId = ResolveSeatRoleId(member.seatRole)
                });
            }
        }

        private static void ResolveCrewPreset(string statsPreset, SkillSetData skillsOverride, out IndividualStats stats, out BehaviorDisposition disposition)
        {
            ResolveCrewPreset(statsPreset, out stats, out disposition);
            if (skillsOverride == null)
            {
                return;
            }

            stats.Command = (half)skillsOverride.command;
            stats.Tactics = (half)skillsOverride.tactics;
            stats.Logistics = (half)skillsOverride.logistics;
            stats.Diplomacy = (half)skillsOverride.diplomacy;
            stats.Engineering = (half)skillsOverride.engineering;
            stats.Resolve = (half)skillsOverride.resolve;
        }

        private static void ResolveCrewPreset(string statsPreset, out IndividualStats stats, out BehaviorDisposition disposition)
        {
            var preset = statsPreset?.Trim().ToLowerInvariant();
            if (preset == "elite")
            {
                stats = new IndividualStats
                {
                    Command = (half)90,
                    Tactics = (half)80,
                    Logistics = (half)70,
                    Diplomacy = (half)70,
                    Engineering = (half)65,
                    Resolve = (half)90
                };
                disposition = BehaviorDisposition.FromValues(0.85f, 0.6f, 0.8f, 0.35f, 0.5f, 0.75f);
                return;
            }

            if (preset == "rookie")
            {
                stats = new IndividualStats
                {
                    Command = (half)45,
                    Tactics = (half)40,
                    Logistics = (half)45,
                    Diplomacy = (half)40,
                    Engineering = (half)40,
                    Resolve = (half)40
                };
                disposition = BehaviorDisposition.FromValues(0.55f, 0.55f, 0.5f, 0.6f, 0.25f, 0.5f);
                return;
            }

            stats = new IndividualStats
            {
                Command = (half)65,
                Tactics = (half)60,
                Logistics = (half)60,
                Diplomacy = (half)55,
                Engineering = (half)50,
                Resolve = (half)60
            };
            disposition = BehaviorDisposition.FromValues(0.7f, 0.6f, 0.65f, 0.45f, 0.4f, 0.6f);
        }

        private static int ResolveSeatRoleId(string seatRole)
        {
            return 0;
        }

        private void EnsureCrewAnatomyPreset(Entity crewEntity, string anatomyPreset, List<string> conditions)
        {
            if (!EntityManager.HasComponent<SimIndividualTag>(crewEntity))
            {
                EntityManager.AddComponent<SimIndividualTag>(crewEntity);
            }

            if (!EntityManager.HasComponent<DerivedCapacities>(crewEntity))
            {
                EntityManager.AddComponentData(crewEntity, new DerivedCapacities
                {
                    Sight = 1f,
                    Manipulation = 1f,
                    Consciousness = 1f,
                    ReactionTime = 1f,
                    Boarding = 1f
                });
            }

            if (!EntityManager.HasBuffer<AnatomyPart>(crewEntity))
            {
                var parts = EntityManager.AddBuffer<AnatomyPart>(crewEntity);
                parts.Add(new AnatomyPart { PartId = AnatomyPartIds.Head, ParentIndex = -1, Coverage = 1f, Tags = AnatomyPartTags.Internal });
                parts.Add(new AnatomyPart { PartId = AnatomyPartIds.EyeLeft, ParentIndex = 0, Coverage = 0.5f, Tags = AnatomyPartTags.Sensory });
                parts.Add(new AnatomyPart { PartId = AnatomyPartIds.EyeRight, ParentIndex = 0, Coverage = 0.5f, Tags = AnatomyPartTags.Sensory });
                parts.Add(new AnatomyPart { PartId = AnatomyPartIds.Brain, ParentIndex = 0, Coverage = 1f, Tags = AnatomyPartTags.Internal | AnatomyPartTags.Vital });
            }

            var preset = anatomyPreset?.Trim().ToLowerInvariant();
            if (conditions != null && conditions.Count > 0)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    var condition = conditions[i];
                    if (string.Equals(condition, "one_eye_missing", StringComparison.OrdinalIgnoreCase))
                    {
                        preset = "one_eye_missing";
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            var capacities = EntityManager.HasComponent<DerivedCapacities>(crewEntity)
                ? EntityManager.GetComponentData<DerivedCapacities>(crewEntity)
                : new DerivedCapacities
                {
                    Sight = 1f,
                    Manipulation = 1f,
                    Consciousness = 1f,
                    ReactionTime = 1f,
                    Boarding = 1f
                };

            var conditionBuffer = EntityManager.HasBuffer<Condition>(crewEntity)
                ? EntityManager.GetBuffer<Condition>(crewEntity)
                : EntityManager.AddBuffer<Condition>(crewEntity);
            conditionBuffer.Clear();

            if (preset == "one_eye_missing")
            {
                conditionBuffer.Add(new Condition
                {
                    TargetPartId = AnatomyPartIds.EyeLeft,
                    Severity = 1f,
                    StageId = 1,
                    Flags = ConditionFlags.Missing | ConditionFlags.OneEyeMissing
                });

                capacities.Sight = math.max(0.6f, capacities.Sight * 0.75f);
                capacities.Boarding = math.max(0.4f, capacities.Boarding * 0.6f);
            }

            EntityManager.SetComponentData(crewEntity, capacities);
        }

        private void EnsureCrewEntityId(Entity crewEntity, string name)
        {
            var idValue = string.IsNullOrWhiteSpace(name)
                ? $"crew-{crewEntity.Index}"
                : name.Trim();
            var fixedId = new FixedString64Bytes(idValue);
            if (EntityManager.HasComponent<Space4XEntityId>(crewEntity))
            {
                EntityManager.SetComponentData(crewEntity, new Space4XEntityId { Id = fixedId });
            }
            else
            {
                EntityManager.AddComponentData(crewEntity, new Space4XEntityId { Id = fixedId });
            }
        }

        private void EnsureCrewLodTier(Entity crewEntity, byte tier)
        {
            if (EntityManager.HasComponent<Space4XEntityLodTier>(crewEntity))
            {
                EntityManager.SetComponentData(crewEntity, new Space4XEntityLodTier { Tier = tier });
            }
            else
            {
                EntityManager.AddComponentData(crewEntity, new Space4XEntityLodTier { Tier = tier });
            }
        }

        private string ResolveCrewTemplateId(CrewTemplateConfigData template)
        {
            if (template == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(template.crewTemplateId))
            {
                return template.crewTemplateId;
            }

            return template.templateId;
        }

        private List<NamedCrewMemberData> ResolveCrewTemplateMembers(CrewTemplateConfigData template)
        {
            var members = new List<NamedCrewMemberData>();
            var templateId = ResolveCrewTemplateId(template);
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                var fileTemplate = LoadCrewTemplateFile(templateId);
                if (fileTemplate != null && fileTemplate.namedCrew != null)
                {
                    for (int i = 0; i < fileTemplate.namedCrew.Count; i++)
                    {
                        var clone = CloneNamedCrewMember(fileTemplate.namedCrew[i]);
                        if (clone != null)
                        {
                            members.Add(clone);
                        }
                    }
                }
            }

            if (template?.overrides != null && template.overrides.Count > 0)
            {
                ApplyCrewOverrides(members, template.overrides);
            }

            if (template?.namedCrew != null)
            {
                for (int i = 0; i < template.namedCrew.Count; i++)
                {
                    var clone = CloneNamedCrewMember(template.namedCrew[i]);
                    if (clone != null)
                    {
                        members.Add(clone);
                    }
                }
            }

            return members;
        }

        private NamedCrewMemberData ResolveCrewMemberTemplate(NamedCrewMemberData member)
        {
            if (member == null)
            {
                return null;
            }

            var resolved = CloneNamedCrewMember(member);
            if (resolved == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(resolved.entityTemplateId))
            {
                var entityTemplate = LoadEntityTemplateFile(resolved.entityTemplateId);
                if (entityTemplate != null)
                {
                    ApplyEntityTemplate(resolved, entityTemplate);
                }
            }

            return resolved;
        }

        private void ApplyEntityTemplate(NamedCrewMemberData member, EntityTemplateFileData template)
        {
            if (member == null || template == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(member.statsPreset) && !string.IsNullOrWhiteSpace(template.statsPreset))
            {
                member.statsPreset = template.statsPreset;
            }

            if (member.skills == null && template.skills != null)
            {
                member.skills = CloneSkillSet(template.skills);
            }

            if (string.IsNullOrWhiteSpace(member.behaviorProfileId) && !string.IsNullOrWhiteSpace(template.behaviorProfileId))
            {
                member.behaviorProfileId = template.behaviorProfileId;
            }

            if (string.IsNullOrWhiteSpace(member.anatomyPreset) && !string.IsNullOrWhiteSpace(template.anatomyPreset))
            {
                member.anatomyPreset = template.anatomyPreset;
            }

            if ((member.conditions == null || member.conditions.Count == 0) &&
                template.conditions != null && template.conditions.Count > 0)
            {
                member.conditions = new List<string>(template.conditions);
            }
        }

        private void ApplyCrewOverrides(List<NamedCrewMemberData> members, List<NamedCrewMemberData> overrides)
        {
            if (members == null || overrides == null)
            {
                return;
            }

            for (int i = 0; i < overrides.Count; i++)
            {
                var candidate = overrides[i];
                if (candidate == null)
                {
                    continue;
                }

                NamedCrewMemberData target = null;
                if (!string.IsNullOrWhiteSpace(candidate.name))
                {
                    for (int j = 0; j < members.Count; j++)
                    {
                        var member = members[j];
                        if (member != null &&
                            string.Equals(member.name, candidate.name, StringComparison.OrdinalIgnoreCase))
                        {
                            target = member;
                            break;
                        }
                    }
                }

                if (target == null)
                {
                    members.Add(CloneNamedCrewMember(candidate));
                    continue;
                }

                ApplyCrewOverride(target, candidate);
            }
        }

        private void ApplyCrewOverride(NamedCrewMemberData target, NamedCrewMemberData overrideData)
        {
            if (target == null || overrideData == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(overrideData.seatRole))
            {
                target.seatRole = overrideData.seatRole;
            }

            if (!string.IsNullOrWhiteSpace(overrideData.statsPreset))
            {
                target.statsPreset = overrideData.statsPreset;
            }

            if (overrideData.skills != null)
            {
                target.skills = CloneSkillSet(overrideData.skills);
            }

            if (!string.IsNullOrWhiteSpace(overrideData.behaviorProfileId))
            {
                target.behaviorProfileId = overrideData.behaviorProfileId;
            }

            if (!string.IsNullOrWhiteSpace(overrideData.anatomyPreset))
            {
                target.anatomyPreset = overrideData.anatomyPreset;
            }

            if (!string.IsNullOrWhiteSpace(overrideData.entityTemplateId))
            {
                target.entityTemplateId = overrideData.entityTemplateId;
            }

            if (overrideData.conditions != null && overrideData.conditions.Count > 0)
            {
                target.conditions = new List<string>(overrideData.conditions);
            }
        }

        private NamedCrewMemberData CloneNamedCrewMember(NamedCrewMemberData source)
        {
            if (source == null)
            {
                return null;
            }

            return new NamedCrewMemberData
            {
                name = source.name,
                seatRole = source.seatRole,
                statsPreset = source.statsPreset,
                skills = CloneSkillSet(source.skills),
                behaviorProfileId = source.behaviorProfileId,
                anatomyPreset = source.anatomyPreset,
                entityTemplateId = source.entityTemplateId,
                conditions = source.conditions != null ? new List<string>(source.conditions) : null
            };
        }

        private SkillSetData CloneSkillSet(SkillSetData source)
        {
            if (source == null)
            {
                return null;
            }

            return new SkillSetData
            {
                command = source.command,
                tactics = source.tactics,
                logistics = source.logistics,
                diplomacy = source.diplomacy,
                engineering = source.engineering,
                resolve = source.resolve
            };
        }

        private CrewTemplateFileData LoadCrewTemplateFile(string templateId)
        {
            var path = ResolveTemplatePath(templateId);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[Space4XMiningScenario] Crew template missing: {templateId} ({path})");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<CrewTemplateFileData>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XMiningScenario] Failed to parse crew template {templateId}: {ex.Message}");
                return null;
            }
        }

        private EntityTemplateFileData LoadEntityTemplateFile(string templateId)
        {
            var path = ResolveTemplatePath(templateId);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[Space4XMiningScenario] Entity template missing: {templateId} ({path})");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<EntityTemplateFileData>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XMiningScenario] Failed to parse entity template {templateId}: {ex.Message}");
                return null;
            }
        }

        private ShipTemplateFileData LoadShipTemplateFile(string templateId)
        {
            var path = ResolveTemplatePath(templateId);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[Space4XMiningScenario] Ship template missing: {templateId} ({path})");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<ShipTemplateFileData>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XMiningScenario] Failed to parse ship template {templateId}: {ex.Message}");
                return null;
            }
        }

        private ShipDefinitionData ResolveShipTemplate(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return null;
            }

            var template = LoadShipTemplateFile(templateId);
            if (template == null)
            {
                return null;
            }

            if (template.ship != null)
            {
                return CloneShipDefinition(template.ship);
            }

            if (template.modules == null && string.IsNullOrWhiteSpace(template.hullId) && template.massCap <= 0f)
            {
                return null;
            }

            return new ShipDefinitionData
            {
                hullId = template.hullId,
                massCap = template.massCap,
                modules = template.modules != null ? new List<string>(template.modules) : null
            };
        }

        private string ResolveTemplateRoot(string scenarioPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return null;
            }

            var scenarioDir = Path.GetDirectoryName(scenarioPath);
            if (string.IsNullOrWhiteSpace(scenarioDir))
            {
                return null;
            }

            return Path.Combine(scenarioDir, "Templates");
        }

        private string ResolveTemplatePath(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return null;
            }

            var root = _templateRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var fileName = templateId.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? templateId
                : $"{templateId}.json";
            return Path.Combine(root, fileName);
        }

        private Entity CreateCrewEntity(
            float lawfulness,
            in StrikeCraftPilotProfileConfig config,
            in IndividualStats stats,
            in BehaviorDisposition disposition)
        {
            var crew = EntityManager.CreateEntity();
            EntityManager.AddComponentData(crew, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));
            EntityManager.AddComponentData(crew, stats);
            EntityManager.AddComponentData(crew, disposition);
            EntityManager.AddComponentData(crew, new CrewSkill
            {
                Value = ((float)stats.Command
                         + (float)stats.Tactics
                         + (float)stats.Logistics
                         + (float)stats.Diplomacy
                         + (float)stats.Engineering
                         + (float)stats.Resolve) / 6f
            });

            var StanceId = ResolveStanceId(config, lawfulness);
            EntityManager.AddBuffer<StanceEntry>(crew);
            EntityManager.AddBuffer<TopStance>(crew);
            var stanceEntries = EntityManager.GetBuffer<StanceEntry>(crew);
            var topStances = EntityManager.GetBuffer<TopStance>(crew);
            stanceEntries.Add(new StanceEntry
            {
                StanceId = StanceId,
                Weight = (half)1f
            });
            topStances.Add(new TopStance
            {
                StanceId = StanceId,
                Weight = (half)1f
            });

            return crew;
        }

        private static StanceId ResolveStanceId(in StrikeCraftPilotProfileConfig config, float lawfulness)
        {
            if (lawfulness >= config.LoyalistLawThreshold)
            {
                return config.FriendlyStance;
            }

            if (lawfulness <= config.MutinousLawThreshold)
            {
                return config.HostileStance;
            }

            return config.NeutralStance;
        }

        private float3 GetPosition(float[] position)
        {
            if (position != null && position.Length >= 2)
            {
                float x = position[0];
                float z = position[1];
                float y = position.Length > 2 ? position[2] : ResolveSpawnHeight(x, z);
                return new float3(x, y, z);
            }
            // Deterministic fallback to keep spawns separated when position data is missing.
            var index = _spawnedEntities?.Count ?? 0;
            var angle = math.radians(137.5f * index);
            var radius = 20f + (index % 5) * 5f;
            var xFallback = math.cos(angle) * radius;
            var zFallback = math.sin(angle) * radius;
            var yFallback = ResolveSpawnHeight(xFallback, zFallback);
            return new float3(xFallback, yFallback, zFallback);
        }

        private static float ResolveSpawnHeight(float x, float z)
        {
            uint hash = (uint)math.hash(new float2(x, z));
            float normalized = hash / (float)uint.MaxValue;
            return (normalized * 2f - 1f) * DefaultSpawnVerticalRange;
        }

        private static Space4XFleetPosture ParseFleetPosture(string posture)
        {
            return posture switch
            {
                "Engaging" => Space4XFleetPosture.Engaging,
                "Retreating" => Space4XFleetPosture.Retreating,
                _ => Space4XFleetPosture.Patrol
            };
        }

        private static StrikeCraftRole ParseStrikeCraftRole(string role)
        {
            return role switch
            {
                "Interceptor" => StrikeCraftRole.Interceptor,
                "Bomber" => StrikeCraftRole.Bomber,
                "Recon" => StrikeCraftRole.Recon,
                "Suppression" => StrikeCraftRole.Suppression,
                "EWar" => StrikeCraftRole.EWar,
                _ => StrikeCraftRole.Fighter
            };
        }

        private static Space4XScenarioActionKind ParseScenarioActionKind(string kind)
        {
            return kind switch
            {
                "TriggerIntercept" => Space4XScenarioActionKind.TriggerIntercept,
                "EconomyEnable" => Space4XScenarioActionKind.EconomyEnable,
                "ProdCreateBusiness" => Space4XScenarioActionKind.ProdCreateBusiness,
                "ProdAddItem" => Space4XScenarioActionKind.ProdAddItem,
                "ProdRequest" => Space4XScenarioActionKind.ProdRequest,
                _ => Space4XScenarioActionKind.MoveFleet
            };
        }

        private static BusinessType ParseBusinessType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return BusinessType.Blacksmith;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "sawmill":
                    return BusinessType.Sawmill;
                case "quarry":
                    return BusinessType.Quarry;
                case "mill":
                    return BusinessType.Mill;
                case "herbalist":
                    return BusinessType.Herbalist;
                case "wainwright":
                    return BusinessType.Wainwright;
                case "builder":
                    return BusinessType.Builder;
                case "alchemist":
                    return BusinessType.Alchemist;
                case "blacksmith":
                default:
                    return BusinessType.Blacksmith;
            }
        }

        private ResourceType ParseResourceType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return ResourceType.Minerals;
            }

            var normalized = type.Trim()
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace(".", string.Empty)
                .ToLowerInvariant();

            if (normalized.StartsWith("space4xresource"))
            {
                normalized = normalized.Substring("space4xresource".Length);
            }

            return normalized switch
            {
                "minerals" => ResourceType.Minerals,
                "raremetals" => ResourceType.RareMetals,
                "energycrystals" => ResourceType.EnergyCrystals,
                "organicmatter" => ResourceType.OrganicMatter,
                "ore" => ResourceType.Ore,
                "volatiles" => ResourceType.Volatiles,
                "transplutonicore" => ResourceType.TransplutonicOre,
                "exoticgases" => ResourceType.ExoticGases,
                "volatilemotes" => ResourceType.VolatileMotes,
                "industrialcrystals" => ResourceType.IndustrialCrystals,
                "isotopes" => ResourceType.Isotopes,
                "heavywater" => ResourceType.HeavyWater,
                "liquidozone" => ResourceType.LiquidOzone,
                "strontiumclathrates" => ResourceType.StrontiumClathrates,
                "salvagecomponents" => ResourceType.SalvageComponents,
                "boostergas" => ResourceType.BoosterGas,
                "relicdata" => ResourceType.RelicData,
                "food" => ResourceType.Food,
                "water" => ResourceType.Water,
                "supplies" => ResourceType.Supplies,
                "fuel" => ResourceType.Fuel,
                _ => ResourceType.Minerals
            };
        }

        private static StanceId ParseStanceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return StanceId.Neutral;
            }

            return value switch
            {
                "Loyalist" => StanceId.Loyalist,
                "Opportunist" => StanceId.Opportunist,
                "Fanatic" => StanceId.Fanatic,
                "Mutinous" => StanceId.Mutinous,
                _ => StanceId.Neutral
            };
        }

        private static PersonalRelationKind ParsePersonalRelationKind(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return PersonalRelationKind.None;
            }

            return value switch
            {
                "BestFriend" => PersonalRelationKind.Friend,
                "BestFriends" => PersonalRelationKind.Friend,
                "Friend" => PersonalRelationKind.Friend,
                "Comrade" => PersonalRelationKind.Comrade,
                "Rival" => PersonalRelationKind.Rival,
                "Family" => PersonalRelationKind.Family,
                "Mentor" => PersonalRelationKind.Mentor,
                "Protege" => PersonalRelationKind.Protege,
                "Debtor" => PersonalRelationKind.Debtor,
                "Creditor" => PersonalRelationKind.Creditor,
                "BloodFeud" => PersonalRelationKind.BloodFeud,
                _ => PersonalRelationKind.None
            };
        }

        private IndividualProfileData FindIndividualProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || _scenarioData == null || _scenarioData.individuals == null)
            {
                return null;
            }

            for (int i = 0; i < _scenarioData.individuals.Count; i++)
            {
                var profile = _scenarioData.individuals[i];
                if (profile != null && string.Equals(profile.id, profileId, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
        }

        private static EntityDispositionFlags ResolveDisposition(string rawValue, EntityDispositionFlags fallback)
        {
            return TryParseDisposition(rawValue, out var parsed) ? parsed : fallback;
        }

        private static bool TryParseDisposition(string rawValue, out EntityDispositionFlags flags)
        {
            flags = EntityDispositionFlags.None;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var trimmed = rawValue.Trim();
            if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ushort.TryParse(trimmed, out var numeric))
            {
                flags = (EntityDispositionFlags)numeric;
                return true;
            }

            var tokens = trimmed.Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var matched = false;
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (string.Equals(token, "Civilian", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Civilian;
                    matched = true;
                }
                else if (string.Equals(token, "Trader", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Trader;
                    matched = true;
                }
                else if (string.Equals(token, "Combatant", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(token, "Combat", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Combatant;
                    matched = true;
                }
                else if (string.Equals(token, "Hostile", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Hostile;
                    matched = true;
                }
                else if (string.Equals(token, "Military", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Military;
                    matched = true;
                }
                else if (string.Equals(token, "Mining", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Mining;
                    matched = true;
                }
                else if (string.Equals(token, "Hauler", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Hauler;
                    matched = true;
                }
                else if (string.Equals(token, "Support", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Support;
                    matched = true;
                }
            }

            return matched;
        }

        private static EntityDispositionFlags BuildCarrierDisposition(CombatComponentData combatData, bool isHostile)
        {
            var flags = EntityDispositionFlags.Support;
            var hasCombat = isHostile ||
                            (combatData != null &&
                             (combatData.canIntercept || combatData.strikeCraftCount > 0 || combatData.escortCount > 0));
            if (hasCombat)
            {
                flags |= EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
            }
            else
            {
                flags |= EntityDispositionFlags.Civilian;
            }

            if (isHostile)
            {
                flags |= EntityDispositionFlags.Hostile;
            }

            return flags;
        }

        private static EntityDispositionFlags BuildMiningDisposition(byte scenarioSide)
        {
            var flags = EntityDispositionFlags.Mining | EntityDispositionFlags.Civilian;
            if (scenarioSide == 1)
            {
                flags |= EntityDispositionFlags.Hostile;
            }

            return flags;
        }

        private static TerrainModificationToolKind ParseMiningToolKind(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TerrainModificationToolKind.Drill;
            }

            if (value.Equals("laser", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationToolKind.Laser;
            }

            if (value.Equals("microwave", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationToolKind.Microwave;
            }

            if (value.Equals("drill", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationToolKind.Drill;
            }

            return TerrainModificationToolKind.Drill;
        }

        private static TerrainModificationShape ParseMiningToolShape(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TerrainModificationShape.Brush;
            }

            if (value.Equals("tunnel", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationShape.Tunnel;
            }

            if (value.Equals("ramp", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationShape.Ramp;
            }

            if (value.Equals("brush", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationShape.Brush;
            }

            return TerrainModificationShape.Brush;
        }
    }

    [System.Serializable]
    public class MiningScenarioJson
    {
        public int seed;
        public float duration_s;
        public MiningScenarioConfigData scenarioConfig;
        public StrikeCraftDogfightConfigData dogfightConfig;
        public StanceTuningConfigData stanceConfig;
        public List<MiningSpawnDefinition> spawn;
        public List<MiningScenarioAction> actions;
        public MiningTelemetryExpectations telemetryExpectations;
        public List<IndividualProfileData> individuals;
        public List<PersonalRelationData> personalRelations;
    }

    [System.Serializable]
    public class MiningScenarioConfigData
    {
        public string spawnLane;
        public bool disableLegacyMining;
        public bool disableLegacyPatrol;
        public bool applyFloatingOrigin;
        public bool applyReferenceFrames;
        public bool applyDefaultModuleLoadouts;
        public OrbitalBandConfigData orbitalBand;
        public RenderFrameConfigData renderFrame;
        public List<string> friendlyFactionOutlook;
        public List<string> hostileFactionOutlook;
        public SensorsBeatConfigData sensorsBeat;
        public CommsBeatConfigData commsBeat;
        public FleetcrawlScenarioConfigData fleetCrawl;
        public List<HeadlessQuestionConfigData> headlessQuestions;
        public List<CrewTemplateConfigData> crewTemplates;
    }

    [System.Serializable]
    public class FleetcrawlScenarioConfigData
    {
        public string contractId;
        public string runDifficulty;
        public int depthStart;
        public List<FleetcrawlRoomPlanEntryData> roomPlan;
    }

    [System.Serializable]
    public class FleetcrawlRoomPlanEntryData
    {
        public string archetype;
        public string roomClass;
        public string systemSize;
        public int threatLevel;
        public List<string> wildcards;
    }

    [System.Serializable]
    public class OrbitalBandConfigData
    {
        public int enabled = -1;
        public float innerRadius = -1f;
        public float outerRadius = -1f;
        public float distanceScale = -1f;
        public float speedScale = -1f;
        public float rangeScale = -1f;
        public float presentationScale = -1f;
        public float enterMultiplier = -1f;
        public float exitMultiplier = -1f;
    }

    [System.Serializable]
    public class RenderFrameConfigData
    {
        public int enabled = -1;
        public int useBandScale = -1;
        public float surfaceScale = -1f;
        public float orbitalScale = -1f;
        public float deepScale = -1f;
        public float surfaceEnterMultiplier = -1f;
        public float surfaceExitMultiplier = -1f;
        public float orbitalEnterMultiplier = -1f;
        public float orbitalExitMultiplier = -1f;
        public int minHoldTicks = -1;
    }

    [System.Serializable]
    public class SensorsBeatConfigData
    {
        public string observerCarrierId;
        public string targetCarrierId;
        public float acquireStart_s;
        public float acquireDuration_s;
        public float dropStart_s;
        public float dropDuration_s;
        public float observerRange;
        public float observerUpdateInterval;
        public int observerMaxTrackedTargets;
    }

    [System.Serializable]
    public class CommsBeatConfigData
    {
        public string senderCarrierId;
        public string receiverCarrierId;
        public string payloadId;
        public float start_s;
        public float duration_s;
        public float interval_s;
        public int transportMask;
        public bool requireAck;
    }

    [System.Serializable]
    public class HeadlessQuestionConfigData
    {
        public string id;
        public bool required;
    }

    [System.Serializable]
    public class CrewTemplateConfigData
    {
        public string carrierId;
        public string crewTemplateId;
        public string templateId;
        public List<NamedCrewMemberData> overrides;
        public List<NamedCrewMemberData> namedCrew;
    }

    [System.Serializable]
    public class NamedCrewMemberData
    {
        public string name;
        public string seatRole;
        public string statsPreset;
        public SkillSetData skills;
        public string behaviorProfileId;
        public string anatomyPreset;
        public string entityTemplateId;
        public List<string> conditions;
    }

    [System.Serializable]
    public class SkillSetData
    {
        public float command;
        public float tactics;
        public float logistics;
        public float diplomacy;
        public float engineering;
        public float resolve;
    }

    [System.Serializable]
    public class EntityTemplateFileData
    {
        public string templateId;
        public string statsPreset;
        public SkillSetData skills;
        public string behaviorProfileId;
        public string anatomyPreset;
        public List<string> conditions;
    }

    [System.Serializable]
    public class CrewTemplateFileData
    {
        public string templateId;
        public List<NamedCrewMemberData> namedCrew;
    }

    [System.Serializable]
    public class ShipTemplateFileData
    {
        public string templateId;
        public string governanceMode;
        public string hullId;
        public float massCap;
        public List<string> modules;
        public ShipDefinitionData ship;
        public List<StationRequirementData> stationRequirements;
    }

    [System.Serializable]
    public class StationRequirementData
    {
        public string seatRole;
        public int minCount;
    }

    [System.Serializable]
    public class StrikeCraftDogfightConfigData
    {
        public float acquireRadius;
        public float coneDegrees;
        public float navConstantN;
        public float breakOffDistance;
        public int breakOffTicks;
        public float rejoinRadius;
        public float[] rejoinOffset;
        public float jinkStrength;
    }

    [System.Serializable]
    public class StanceTuningConfigData
    {
        public StanceTuningEntryData aggressive;
        public StanceTuningEntryData balanced;
        public StanceTuningEntryData defensive;
        public StanceTuningEntryData evasive;
    }

    [System.Serializable]
    public class StanceTuningEntryData
    {
        public float avoidanceRadius;
        public float avoidanceStrength;
        public float speedMultiplier;
        public float rotationMultiplier;
        public float maintainFormationWhenAttacking;
        public float evasionJinkStrength;
        public float autoEngageRadius;
        public float abortAttackOnDamageThreshold;
        public bool returnToPatrolAfterCombat;
        public bool commandOverrideDropsToNeutral;
        public float attackMoveBearingWeight;
        public float attackMoveDestinationWeight;
    }

    [System.Serializable]
    public class MiningScenarioAction
    {
        public float time_s;
        public string kind;
        public string fleetId;
        public float[] targetPosition;
        public string requesterFleetId;
        public string targetFleetId;
        public string description;
        public string businessId;
        public string businessType;
        public string itemId;
        public string recipeId;
        public float quantity;
        public float capacity;
    }

    [System.Serializable]
    public class MiningSpawnDefinition
    {
        public string kind;
        public string entityId;
        public float[] position;
        public string shipTemplateId;
        public ShipDefinitionData ship;
        public string carrierId;
        public string toolKind;
        public string toolShape;
        public bool toolShapeOverride;
        public float toolRadiusOverride;
        public float toolRadiusMultiplier;
        public float toolStepLengthOverride;
        public float toolStepLengthMultiplier;
        public float toolDigUnitsPerMeterOverride;
        public float toolMinStepLengthOverride;
        public float toolMaxStepLengthOverride;
        public float toolYieldMultiplier;
        public float toolHeatDeltaMultiplier;
        public float toolInstabilityDeltaMultiplier;
        public int toolDamageDeltaOverride;
        public int toolDamageThresholdOverride;
        public string resourceId;
        public float miningEfficiency;
        public float speed;
        public float cargoCapacity;
        public float unitsRemaining;
        public float gatherRatePerWorker;
        public int maxSimultaneousWorkers;
        public bool startDocked;
        public string pilotProfileId;
        public string disposition;
        public MiningComponentData components;
    }

    [System.Serializable]
    public class MiningComponentData
    {
        public List<ResourceStorageData> ResourceStorage;
        public FleetComponentData Fleet;
        public CombatComponentData Combat;
    }

    [System.Serializable]
    public class FleetComponentData
    {
        public string fleetId;
        public string posture;
        public int shipCount;
    }

    [System.Serializable]
    public class CombatComponentData
    {
        public bool canIntercept;
        public float interceptSpeed;
        public bool isHostile;
        public int strikeCraftCount;
        public string strikeCraftRole;
        public int escortCount;
        public float escortRelease_s;
        public string escortShipTemplateId;
        public ShipDefinitionData escortShip;
        public string pilotProfileId;
        public List<string> pilotProfileIds;
    }

    [System.Serializable]
    public class IndividualProfileData
    {
        public string id;
        public float law;
        public float good;
        public float integrity;
        public ushort raceId;
        public ushort cultureId;
        public List<StanceWeightData> stances;
        public List<StanceWeightData> outlooks; // legacy
        public BehaviorDispositionData behaviorDisposition;

        public List<StanceWeightData> ResolveStances() => stances ?? outlooks;
    }

    [System.Serializable]
    public class PersonalRelationData
    {
        public string idA;
        public string idB;
        public string kind;
        public float score;
        public float trust;
        public float fear;
    }

    [System.Serializable]
    public class BehaviorDispositionData
    {
        public bool enabled;
        public float compliance;
        public float caution;
        public float formationAdherence;
        public float riskTolerance;
        public float aggression;
        public float patience;
    }

    [System.Serializable]
    public class StanceWeightData
    {
        public string stanceId;
        public string outlookId; // legacy
        public float weight;

        public string ResolveStanceId()
        {
            if (!string.IsNullOrWhiteSpace(stanceId))
            {
                return stanceId;
            }

            return outlookId;
        }
    }

    [System.Serializable]
    public class ResourceStorageData
    {
        public string type;
        public float capacity;
    }

    [System.Serializable]
    public class MiningTelemetryExpectations
    {
        public bool expectMiningYield;
        public bool expectCarrierPickup;
        public bool expectInterceptAttempts;
        public bool expectFleetRegistry;
        public bool expectResourceRegistry;
        public MiningTelemetryExport export;
    }

    [System.Serializable]
    public class MiningTelemetryExport
    {
        public string csv;
        public string json;
    }
}
