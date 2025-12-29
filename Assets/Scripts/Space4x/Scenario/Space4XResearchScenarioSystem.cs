using System;
using System.Collections.Generic;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Research;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4x.Scenario
{
    /// <summary>
    /// Loads and executes the research scenario from JSON, spawning anomalies and research carriers.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class Space4XResearchScenarioSystem : SystemBase
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private bool _hasLoaded;
        private ResearchScenarioJson _scenarioData;
        private Dictionary<string, Entity> _spawnedEntities;

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

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                return;
            }

            var scenarioIdString = scenarioInfo.ScenarioId.ToString();
            if (!scenarioIdString.Contains("research", StringComparison.OrdinalIgnoreCase))
            {
                Enabled = false;
                return;
            }

            var scenarioPath = FindScenarioPath(scenarioIdString);
            if (string.IsNullOrEmpty(scenarioPath) || !File.Exists(scenarioPath))
            {
                UnityDebug.LogWarning($"[Space4XResearchScenario] Scenario file not found: {scenarioPath}");
                Enabled = false;
                return;
            }

            var jsonText = File.ReadAllText(scenarioPath);
            _scenarioData = JsonUtility.FromJson<ResearchScenarioJson>(jsonText);
            if (_scenarioData == null || _scenarioData.spawn == null)
            {
                UnityDebug.LogError("[Space4XResearchScenario] Failed to parse scenario JSON");
                Enabled = false;
                return;
            }

            _spawnedEntities = new Dictionary<string, Entity>();
            SpawnEntities();

            _hasLoaded = true;
            Enabled = false;
        }

        private string FindScenarioPath(string scenarioId)
        {
            var envPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                var fullEnvPath = Path.GetFullPath(envPath);
                if (File.Exists(fullEnvPath))
                {
                    return fullEnvPath;
                }
            }

            var possiblePaths = new[]
            {
                Path.Combine(Application.dataPath, "Scenarios", $"{scenarioId}.json"),
                Path.Combine(Application.dataPath, "..", "Assets", "Scenarios", $"{scenarioId}.json"),
                Path.Combine(Application.streamingAssetsPath, "Scenarios", $"{scenarioId}.json")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private void SpawnEntities()
        {
            foreach (var spawn in _scenarioData.spawn)
            {
                switch (spawn.kind)
                {
                    case "ResearchAnomaly":
                        SpawnAnomaly(spawn);
                        break;
                    case "Carrier":
                        SpawnResearchCarrier(spawn);
                        break;
                }
            }
        }

        private void SpawnAnomaly(ResearchSpawnDefinition spawn)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);

            var configData = spawn.anomalyConfig ?? new ResearchAnomalyConfigData();
            var fieldId = ParseScienceField(configData.fieldId, configData.fieldIdValue);
            EntityManager.AddComponentData(entity, new AnomalyConfig
            {
                FieldId = fieldId,
                BaseYieldPerTick = configData.baseYieldPerTick,
                ChargeCapacity = configData.chargeCapacity,
                RechargePerTick = configData.rechargePerTick,
                BandwidthCost = configData.bandwidthCost,
                IsPermanent = (byte)math.clamp(configData.isPermanent, 0, 1)
            });

            var stateData = spawn.anomalyState ?? new ResearchAnomalyStateData
            {
                remainingCharge = configData.chargeCapacity
            };
            EntityManager.AddComponentData(entity, new AnomalyState
            {
                RemainingCharge = stateData.remainingCharge,
                ActiveHarvesters = (byte)math.clamp(stateData.activeHarvesters, 0, 255),
                RechargeProgress = stateData.rechargeProgress
            });

            var visualData = spawn.anomalyVisual ?? new ResearchAnomalyVisualData();
            EntityManager.AddComponentData(entity, new AnomalyVisualIntent
            {
                FieldId = ParseScienceField(visualData.fieldId, visualData.fieldIdValue, fieldId),
                StrengthBucket = (byte)math.clamp(visualData.strengthBucket, 0, 255)
            });

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void SpawnResearchCarrier(ResearchSpawnDefinition spawn)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);
            EntityManager.AddComponent<CarrierResearchModuleTag>(entity);

            var assignmentData = spawn.researchAssignment ?? new ResearchAssignmentData();
            EntityManager.AddComponentData(entity, new CarrierResearchAssignment
            {
                TargetAnomaly = Entity.Null,
                CooldownTicks = assignmentData.cooldownTicks,
                NextHarvestInTicks = assignmentData.nextHarvestInTicks
            });

            var bandwidthData = spawn.researchBandwidth ?? new ResearchBandwidthData();
            EntityManager.AddComponentData(entity, new ResearchBandwidthState
            {
                CurrentBandwidth = bandwidthData.currentBandwidth,
                Capacity = math.max(bandwidthData.capacity, bandwidthData.currentBandwidth),
                RefillPerTick = bandwidthData.refillPerTick,
                LossFraction = math.clamp(bandwidthData.lossFraction, 0f, 1f)
            });

            var harvestData = spawn.researchHarvest ?? new ResearchHarvestData();
            EntityManager.AddComponentData(entity, new ResearchHarvestState
            {
                PendingRawPoints = harvestData.pendingRawPoints,
                RefinedPoints = harvestData.refinedPoints,
                FocusField = ParseScienceField(harvestData.focusField, harvestData.focusFieldValue)
            });

            if (spawn.threatIntent != null)
            {
                EntityManager.AddComponentData(entity, new CarrierThreatIntent
                {
                    Intent = ParseThreatIntent(spawn.threatIntent.intent, spawn.threatIntent.intentValue),
                    Confidence = (byte)math.clamp(spawn.threatIntent.confidence, 0, 255)
                });
            }

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private static float3 GetPosition(float[] position)
        {
            if (position == null || position.Length < 2)
            {
                return float3.zero;
            }

            return new float3(position[0], position.Length > 2 ? position[2] : 0f, position[1]);
        }

        private static ScienceFieldId ParseScienceField(string fieldId, int fieldIdValue, ScienceFieldId fallback = ScienceFieldId.None)
        {
            if (!string.IsNullOrWhiteSpace(fieldId))
            {
                switch (fieldId.Trim())
                {
                    case "Materials":
                        return ScienceFieldId.Materials;
                    case "Propulsion":
                        return ScienceFieldId.Propulsion;
                    case "Data":
                        return ScienceFieldId.Data;
                    case "Subspace":
                        return ScienceFieldId.Subspace;
                    case "Biology":
                        return ScienceFieldId.Biology;
                }
            }

            if (fieldIdValue > 0 && fieldIdValue <= (int)ScienceFieldId.Biology)
            {
                return (ScienceFieldId)fieldIdValue;
            }

            return fallback;
        }

        private static ThreatIntentId ParseThreatIntent(string intent, int intentValue)
        {
            if (!string.IsNullOrWhiteSpace(intent))
            {
                switch (intent.Trim())
                {
                    case "Probe":
                        return ThreatIntentId.Probe;
                    case "Ambush":
                        return ThreatIntentId.Ambush;
                    case "Escalate":
                        return ThreatIntentId.Escalate;
                }
            }

            if (intentValue > 0 && intentValue <= (int)ThreatIntentId.Escalate)
            {
                return (ThreatIntentId)intentValue;
            }

            return ThreatIntentId.None;
        }

        [Serializable]
        private sealed class ResearchScenarioJson
        {
            public int seed;
            public float duration_s;
            public List<ResearchSpawnDefinition> spawn;
        }

        [Serializable]
        private sealed class ResearchSpawnDefinition
        {
            public string kind;
            public string entityId;
            public float[] position;
            public ResearchAnomalyConfigData anomalyConfig;
            public ResearchAnomalyStateData anomalyState;
            public ResearchAnomalyVisualData anomalyVisual;
            public ResearchAssignmentData researchAssignment;
            public ResearchBandwidthData researchBandwidth;
            public ResearchHarvestData researchHarvest;
            public ResearchThreatIntentData threatIntent;
        }

        [Serializable]
        private sealed class ResearchAnomalyConfigData
        {
            public string fieldId;
            public int fieldIdValue;
            public float baseYieldPerTick;
            public float chargeCapacity;
            public float rechargePerTick;
            public float bandwidthCost;
            public int isPermanent;
        }

        [Serializable]
        private sealed class ResearchAnomalyStateData
        {
            public float remainingCharge;
            public int activeHarvesters;
            public float rechargeProgress;
        }

        [Serializable]
        private sealed class ResearchAnomalyVisualData
        {
            public string fieldId;
            public int fieldIdValue;
            public int strengthBucket;
        }

        [Serializable]
        private sealed class ResearchAssignmentData
        {
            public float cooldownTicks;
            public float nextHarvestInTicks;
        }

        [Serializable]
        private sealed class ResearchBandwidthData
        {
            public float currentBandwidth;
            public float capacity;
            public float refillPerTick;
            public float lossFraction;
        }

        [Serializable]
        private sealed class ResearchHarvestData
        {
            public float pendingRawPoints;
            public float refinedPoints;
            public string focusField;
            public int focusFieldValue;
        }

        [Serializable]
        private sealed class ResearchThreatIntentData
        {
            public string intent;
            public int intentValue;
            public int confidence;
        }
    }
}
