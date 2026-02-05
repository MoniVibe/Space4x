
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.WorldGen;
using Space4X.Registry;
using Space4XAlignmentTriplet = Space4X.Registry.AlignmentTriplet;
using Space4XIndividualStats = Space4X.Registry.IndividualStats;
using Space4XResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using Space4XResourceSourceState = Space4X.Registry.ResourceSourceState;
using Space4XResourceTypeId = Space4X.Registry.ResourceTypeId;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSimServerHttpSystem))]
    public partial struct Space4XSimServerPersistenceSystem : ISystem
    {
        private bool _autosaveScheduled;
        private double _nextAutosaveAt;

        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSimServerConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (TryProcessLoad(ref state))
            {
                return;
            }

            ProcessSaveRequests(ref state);
            ProcessAutosave(ref state);
        }

        private bool TryProcessLoad(ref SystemState state)
        {
            if (!Space4XSimHttpServer.TryDequeueLoad(out var json))
            {
                return false;
            }

            DrainLoadQueue();

            LoadRequest request = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    request = JsonUtility.FromJson<LoadRequest>(json);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XSimServer] Failed to parse load JSON: {ex.Message}");
                }
            }

            var path = ResolveLoadPath(request);
            if (string.IsNullOrWhiteSpace(path))
            {
                UnityEngine.Debug.LogWarning("[Space4XSimServer] No save file found for load request.");
                return true;
            }

            SimSaveData data;
            try
            {
                var payload = File.ReadAllText(path);
                data = JsonUtility.FromJson<SimSaveData>(payload);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XSimServer] Failed to read save file '{path}': {ex.Message}");
                return true;
            }

            if (data == null || data.version <= 0)
            {
                UnityEngine.Debug.LogWarning("[Space4XSimServer] Save data invalid or empty.");
                return true;
            }

            if (data.version != Space4XSimServerPaths.SaveVersion)
            {
                UnityEngine.Debug.LogWarning($"[Space4XSimServer] Save version mismatch. Expected {Space4XSimServerPaths.SaveVersion}, got {data.version}.");
                return true;
            }

            if (!ApplyLoadData(ref state, data))
            {
                UnityEngine.Debug.LogWarning("[Space4XSimServer] Failed to apply save data.");
                return true;
            }

            _autosaveScheduled = false;
            UnityEngine.Debug.Log($"[Space4XSimServer] Loaded save '{Path.GetFileName(path)}'.");
            return true;
        }

        private void ProcessSaveRequests(ref SystemState state)
        {
            var processed = 0;
            while (processed < 4 && Space4XSimHttpServer.TryDequeueSave(out var json))
            {
                SaveRequest request = null;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        request = JsonUtility.FromJson<SaveRequest>(json);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[Space4XSimServer] Failed to parse save JSON: {ex.Message}");
                    }
                }

                SaveSnapshot(ref state, request, isAutosave: false);
                processed++;
            }
        }

        private void ProcessAutosave(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out Space4XSimServerConfig config))
            {
                return;
            }

            if (config.AutosaveSeconds <= 0f)
            {
                _autosaveScheduled = false;
                return;
            }

            var now = GetWorldSeconds(ref state);
            if (!_autosaveScheduled)
            {
                _nextAutosaveAt = now + config.AutosaveSeconds;
                _autosaveScheduled = true;
                return;
            }

            if (now < _nextAutosaveAt)
            {
                return;
            }

            var request = new SaveRequest { slot = "autosave", overwrite = false };
            SaveSnapshot(ref state, request, isAutosave: true);
            _nextAutosaveAt = now + config.AutosaveSeconds;
        }

        private double GetWorldSeconds(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return timeState.WorldSeconds;
            }

            return state.WorldUnmanaged.Time.ElapsedTime;
        }

        private void DrainLoadQueue()
        {
            while (Space4XSimHttpServer.TryDequeueLoad(out _))
            {
            }
        }

        private string ResolveLoadPath(LoadRequest request)
        {
            Space4XSimServerPaths.EnsureDirectories();
            var saveDir = Space4XSimServerPaths.SaveDir;
            if (!Directory.Exists(saveDir))
            {
                return null;
            }

            var slot = request?.ResolveSlot();
            var useLatest = request?.ResolveLatest() ?? string.IsNullOrWhiteSpace(slot);
            if (useLatest)
            {
                return FindLatestSave(saveDir);
            }

            var requested = Path.GetFileName(slot ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(requested) && requested.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = Path.Combine(saveDir, requested);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var sanitized = Space4XSimServerPaths.SanitizeSlot(slot ?? string.Empty);
            var exact = Path.Combine(saveDir, sanitized + ".json");
            if (File.Exists(exact))
            {
                return exact;
            }

            var matches = Directory.GetFiles(saveDir, sanitized + "*.json", SearchOption.TopDirectoryOnly);
            if (matches.Length == 0)
            {
                return null;
            }

            Array.Sort(matches, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return matches[0];
        }

        private string FindLatestSave(string saveDir)
        {
            var files = Directory.GetFiles(saveDir, "*.json", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                return null;
            }

            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return files[0];
        }

        private void SaveSnapshot(ref SystemState state, SaveRequest request, bool isAutosave)
        {
            var slot = request?.ResolveSlot();
            if (string.IsNullOrWhiteSpace(slot))
            {
                slot = isAutosave ? "autosave" : "manual";
            }

            var overwrite = request?.overwrite ?? false;
            if (isAutosave && request == null)
            {
                overwrite = false;
            }

            Space4XSimServerPaths.EnsureDirectories();
            var path = Space4XSimServerPaths.BuildSavePath(slot, overwrite, out var resolvedSlot);

            var data = CaptureSaveData(ref state);
            var json = JsonUtility.ToJson(data, false);
            if (string.IsNullOrWhiteSpace(json))
            {
                UnityEngine.Debug.LogWarning("[Space4XSimServer] Save payload empty.");
                return;
            }

            try
            {
                var tmpPath = path + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Copy(tmpPath, path, true);
                File.Delete(tmpPath);
                Space4XSimServerPaths.TrimSaves();
                UnityEngine.Debug.Log($"[Space4XSimServer] Saved '{Path.GetFileName(path)}' (slot={resolvedSlot}).");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XSimServer] Failed to write save '{path}': {ex.Message}");
            }
        }

        private SimSaveData CaptureSaveData(ref SystemState state)
        {
            var data = new SimSaveData
            {
                version = Space4XSimServerPaths.SaveVersion,
                createdUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                config = SimConfigData.FromConfig(SystemAPI.GetSingleton<Space4XSimServerConfig>()),
                time = CaptureTime(ref state)
            };

            data.factions = CaptureFactions(ref state);
            data.systems = CaptureSystems(ref state);
            data.colonies = CaptureColonies(ref state);
            data.resources = CaptureResources(ref state);
            data.anomalies = CaptureAnomalies(ref state);
            data.missionOffers = CaptureMissionOffers(ref state);
            data.missionAssignments = CaptureMissionAssignments(ref state);

            return data;
        }

        private SimTimeData CaptureTime(ref SystemState state)
        {
            var timeState = SystemAPI.TryGetSingleton<TimeState>(out var time) ? time : default;
            var scalars = SystemAPI.TryGetSingleton<SimulationScalars>(out var scalarState) ? scalarState : SimulationScalars.Default;

            return new SimTimeData
            {
                tick = timeState.Tick,
                worldSeconds = timeState.WorldSeconds,
                fixedDeltaTime = timeState.FixedDeltaTime,
                currentSpeedMultiplier = timeState.CurrentSpeedMultiplier,
                elapsedTime = timeState.ElapsedTime,
                deltaTime = timeState.DeltaTime,
                deltaSeconds = timeState.DeltaSeconds,
                isPaused = timeState.IsPaused,
                timeScale = scalars.TimeScale
            };
        }
        private FactionData[] CaptureFactions(ref SystemState state)
        {
            var list = new List<FactionData>(16);
            var entityManager = state.EntityManager;
            var factionLookup = state.GetComponentLookup<Space4XFaction>(true);

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                var data = new FactionData
                {
                    factionId = faction.ValueRO.FactionId,
                    type = (byte)faction.ValueRO.Type,
                    outlook = (ushort)faction.ValueRO.Outlook,
                    aggression = (float)faction.ValueRO.Aggression,
                    riskTolerance = (float)faction.ValueRO.RiskTolerance,
                    expansionDrive = (float)faction.ValueRO.ExpansionDrive,
                    tradeFocus = (float)faction.ValueRO.TradeFocus,
                    researchFocus = (float)faction.ValueRO.ResearchFocus,
                    militaryFocus = (float)faction.ValueRO.MilitaryFocus
                };

                if (entityManager.HasComponent<AffiliationRelation>(entity))
                {
                    data.name = entityManager.GetComponentData<AffiliationRelation>(entity).AffiliationName.ToString();
                }

                if (entityManager.HasComponent<FactionResources>(entity))
                {
                    data.resources = FactionResourcesData.From(entityManager.GetComponentData<FactionResources>(entity));
                }

                if (entityManager.HasComponent<Space4XTerritoryControl>(entity))
                {
                    data.territory = TerritoryData.From(entityManager.GetComponentData<Space4XTerritoryControl>(entity));
                }

                if (entityManager.HasComponent<TechLevel>(entity))
                {
                    data.tech = TechLevelData.From(entityManager.GetComponentData<TechLevel>(entity));
                }

                if (entityManager.HasComponent<TechDiffusionState>(entity))
                {
                    var diffusion = entityManager.GetComponentData<TechDiffusionState>(entity);
                    var sourceId = ResolveFactionId(factionLookup, diffusion.SourceEntity, 0);
                    data.diffusion = TechDiffusionData.From(diffusion, sourceId);
                }

                if (entityManager.HasComponent<Space4XFactionDirective>(entity))
                {
                    data.directive = DirectiveData.From(entityManager.GetComponentData<Space4XFactionDirective>(entity));
                }

                data.leader = CaptureLeader(entityManager, entity);

                if (entityManager.HasBuffer<FactionRelationEntry>(entity))
                {
                    var buffer = entityManager.GetBuffer<FactionRelationEntry>(entity);
                    if (buffer.Length > 0)
                    {
                        var relations = new RelationData[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            var relation = buffer[i].Relation;
                            var otherId = relation.OtherFactionId;
                            if (otherId == 0 && relation.OtherFaction != Entity.Null)
                            {
                                otherId = ResolveFactionId(factionLookup, relation.OtherFaction, 0);
                            }
                            relations[i] = RelationData.From(relation, otherId);
                        }
                        data.relations = relations;
                    }
                }

                if (entityManager.HasBuffer<Space4XContactStanding>(entity))
                {
                    var buffer = entityManager.GetBuffer<Space4XContactStanding>(entity);
                    if (buffer.Length > 0)
                    {
                        var contacts = new ContactData[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            var entry = buffer[i];
                            contacts[i] = new ContactData
                            {
                                contactFactionId = entry.ContactFactionId,
                                standing = (float)entry.Standing,
                                loyaltyPoints = entry.LoyaltyPoints,
                                tier = entry.Tier
                            };
                        }
                        data.contacts = contacts;
                    }
                }

                if (entityManager.HasComponent<ReverseEngineeringState>(entity))
                {
                    data.reverseEngineeringState = ReverseEngineeringStateData.From(entityManager.GetComponentData<ReverseEngineeringState>(entity));
                }

                if (entityManager.HasBuffer<ReverseEngineeringEvidence>(entity))
                {
                    var buffer = entityManager.GetBuffer<ReverseEngineeringEvidence>(entity);
                    if (buffer.Length > 0)
                    {
                        var evidence = new ReverseEngineeringEvidenceData[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            evidence[i] = ReverseEngineeringEvidenceData.From(buffer[i]);
                        }
                        data.reverseEngineeringEvidence = evidence;
                    }
                }

                if (entityManager.HasBuffer<ReverseEngineeringTask>(entity))
                {
                    var buffer = entityManager.GetBuffer<ReverseEngineeringTask>(entity);
                    if (buffer.Length > 0)
                    {
                        var tasks = new ReverseEngineeringTaskData[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            tasks[i] = ReverseEngineeringTaskData.From(buffer[i]);
                        }
                        data.reverseEngineeringTasks = tasks;
                    }
                }

                if (entityManager.HasBuffer<ReverseEngineeringBlueprintVariant>(entity))
                {
                    var buffer = entityManager.GetBuffer<ReverseEngineeringBlueprintVariant>(entity);
                    if (buffer.Length > 0)
                    {
                        var variants = new ReverseEngineeringVariantData[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            variants[i] = ReverseEngineeringVariantData.From(buffer[i]);
                        }
                        data.reverseEngineeringVariants = variants;
                    }
                }

                if (entityManager.HasBuffer<ReverseEngineeringBlueprintProgress>(entity))
                {
                    var buffer = entityManager.GetBuffer<ReverseEngineeringBlueprintProgress>(entity);
                    if (buffer.Length > 0)
                    {
                        var progress = new ReverseEngineeringProgressData[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            progress[i] = ReverseEngineeringProgressData.From(buffer[i]);
                        }
                        data.reverseEngineeringProgress = progress;
                    }
                }

                list.Add(data);
            }

            return list.ToArray();
        }

        private SystemData[] CaptureSystems(ref SystemState state)
        {
            var list = new List<SystemData>(64);
            var entityManager = state.EntityManager;

            foreach (var (system, transform, entity) in SystemAPI.Query<RefRO<Space4XStarSystem>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var data = new SystemData
                {
                    systemId = system.ValueRO.SystemId,
                    ownerFactionId = system.ValueRO.OwnerFactionId,
                    ringIndex = system.ValueRO.RingIndex,
                    position = ToVector3(transform.ValueRO.Position)
                };

                if (entityManager.HasBuffer<Space4XSystemTrait>(entity))
                {
                    var buffer = entityManager.GetBuffer<Space4XSystemTrait>(entity);
                    if (buffer.Length > 0)
                    {
                        var traits = new SystemTraitData[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            traits[i] = new SystemTraitData
                            {
                                kind = (byte)buffer[i].Kind,
                                intensity = (float)buffer[i].Intensity
                            };
                        }

                        data.traits = traits;
                    }
                }

                list.Add(data);
            }

            return list.ToArray();
        }

        private ColonyData[] CaptureColonies(ref SystemState state)
        {
            var list = new List<ColonyData>(32);
            var entityManager = state.EntityManager;
            var factionLookup = state.GetComponentLookup<Space4XFaction>(true);

            foreach (var (colony, transform, entity) in SystemAPI.Query<RefRO<Space4XColony>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var data = new ColonyData
                {
                    colonyId = colony.ValueRO.ColonyId.ToString(),
                    population = colony.ValueRO.Population,
                    storedResources = colony.ValueRO.StoredResources,
                    status = (byte)colony.ValueRO.Status,
                    sectorId = colony.ValueRO.SectorId,
                    position = ToVector3(transform.ValueRO.Position)
                };

                if (entityManager.HasBuffer<AffiliationTag>(entity))
                {
                    var affiliations = entityManager.GetBuffer<AffiliationTag>(entity);
                    for (int i = 0; i < affiliations.Length; i++)
                    {
                        var tag = affiliations[i];
                        if (tag.Target == Entity.Null || !factionLookup.HasComponent(tag.Target))
                        {
                            continue;
                        }

                        data.factionId = factionLookup[tag.Target].FactionId;
                        data.loyalty = (float)tag.Loyalty;
                        break;
                    }
                }

                list.Add(data);
            }

            return list.ToArray();
        }

        private ResourceData[] CaptureResources(ref SystemState state)
        {
            var list = new List<ResourceData>(128);
            var entityManager = state.EntityManager;

            foreach (var (asteroid, sourceState, sourceConfig, resourceId, transform, entity) in
                SystemAPI.Query<RefRO<Asteroid>, RefRO<Space4XResourceSourceState>, RefRO<Space4XResourceSourceConfig>, RefRO<Space4XResourceTypeId>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                var data = new ResourceData
                {
                    asteroidId = asteroid.ValueRO.AsteroidId.ToString(),
                    resourceType = (byte)asteroid.ValueRO.ResourceType,
                    resourceAmount = asteroid.ValueRO.ResourceAmount,
                    maxResourceAmount = asteroid.ValueRO.MaxResourceAmount,
                    miningRate = asteroid.ValueRO.MiningRate,
                    unitsRemaining = sourceState.ValueRO.UnitsRemaining,
                    lastHarvestTick = sourceState.ValueRO.LastHarvestTick,
                    gatherRatePerWorker = sourceConfig.ValueRO.GatherRatePerWorker,
                    maxWorkers = sourceConfig.ValueRO.MaxSimultaneousWorkers,
                    respawnSeconds = sourceConfig.ValueRO.RespawnSeconds,
                    flags = sourceConfig.ValueRO.Flags,
                    resourceTypeId = resourceId.ValueRO.Value.ToString(),
                    position = ToVector3(transform.ValueRO.Position)
                };

                if (entityManager.HasComponent<Space4XAsteroidVolumeConfig>(entity))
                {
                    var volume = entityManager.GetComponentData<Space4XAsteroidVolumeConfig>(entity);
                    data.radius = volume.Radius;
                    data.coreRadiusRatio = volume.CoreRadiusRatio;
                    data.mantleRadiusRatio = volume.MantleRadiusRatio;
                    data.crustMaterialId = volume.CrustMaterialId;
                    data.mantleMaterialId = volume.MantleMaterialId;
                    data.coreMaterialId = volume.CoreMaterialId;
                    data.coreDepositId = volume.CoreDepositId;
                    data.coreOreGrade = volume.CoreOreGrade;
                    data.oreGradeExponent = volume.OreGradeExponent;
                    data.volumeSeed = volume.Seed;
                }

                if (entityManager.HasComponent<Space4XAsteroidCenter>(entity))
                {
                    data.center = ToVector3(entityManager.GetComponentData<Space4XAsteroidCenter>(entity).Position);
                }

                if (entityManager.HasComponent<HistoryTier>(entity))
                {
                    var history = entityManager.GetComponentData<HistoryTier>(entity);
                    data.historyTier = (byte)history.Tier;
                    data.historyOverrideStride = history.OverrideStrideSeconds;
                }

                if (entityManager.HasComponent<LastRecordedTick>(entity))
                {
                    data.lastRecordedTick = entityManager.GetComponentData<LastRecordedTick>(entity).Tick;
                }

                list.Add(data);
            }

            return list.ToArray();
        }

        private AnomalyData[] CaptureAnomalies(ref SystemState state)
        {
            var list = new List<AnomalyData>(16);

            foreach (var (anomaly, transform) in SystemAPI.Query<RefRO<Space4XAnomaly>, RefRO<LocalTransform>>())
            {
                list.Add(new AnomalyData
                {
                    anomalyId = anomaly.ValueRO.AnomalyId.ToString(),
                    classification = anomaly.ValueRO.Classification.ToString(),
                    severity = (byte)anomaly.ValueRO.Severity,
                    state = (byte)anomaly.ValueRO.State,
                    instability = anomaly.ValueRO.Instability,
                    sectorId = anomaly.ValueRO.SectorId,
                    position = ToVector3(transform.ValueRO.Position)
                });
            }

            return list.ToArray();
        }

        private MissionOfferData[] CaptureMissionOffers(ref SystemState state)
        {
            var list = new List<MissionOfferData>(32);
            var entityManager = state.EntityManager;

            foreach (var (offer, entity) in SystemAPI.Query<RefRO<Space4XMissionOffer>>().WithEntityAccess())
            {
                ResolveTargetData(entityManager, offer.ValueRO.TargetEntity, offer.ValueRO.TargetPosition,
                    out var kind, out var targetId, out var systemId, out var ringIndex, out var poiKind, out var targetPos);

                list.Add(new MissionOfferData
                {
                    offerId = offer.ValueRO.OfferId,
                    type = (byte)offer.ValueRO.Type,
                    status = (byte)offer.ValueRO.Status,
                    issuerFactionId = offer.ValueRO.IssuerFactionId,
                    targetKind = (byte)kind,
                    targetId = targetId,
                    targetSystemId = systemId,
                    targetRingIndex = ringIndex,
                    targetPoiKind = poiKind,
                    targetPosition = targetPos,
                    resourceTypeIndex = offer.ValueRO.ResourceTypeIndex,
                    units = offer.ValueRO.Units,
                    rewardCredits = offer.ValueRO.RewardCredits,
                    rewardStanding = offer.ValueRO.RewardStanding,
                    rewardLp = offer.ValueRO.RewardLp,
                    risk = offer.ValueRO.Risk,
                    priority = offer.ValueRO.Priority,
                    createdTick = offer.ValueRO.CreatedTick,
                    expiryTick = offer.ValueRO.ExpiryTick,
                    assignedTick = offer.ValueRO.AssignedTick,
                    completedTick = offer.ValueRO.CompletedTick
                });
            }

            return list.Count == 0 ? Array.Empty<MissionOfferData>() : list.ToArray();
        }

        private MissionAssignmentData[] CaptureMissionAssignments(ref SystemState state)
        {
            var list = new List<MissionAssignmentData>(32);
            var entityManager = state.EntityManager;

            foreach (var (assignment, entity) in SystemAPI.Query<RefRO<Space4XMissionAssignment>>().WithEntityAccess())
            {
                ResolveTargetData(entityManager, assignment.ValueRO.TargetEntity, assignment.ValueRO.TargetPosition,
                    out var targetKind, out var targetId, out var targetSystemId, out var targetRingIndex, out var targetPoiKind, out var targetPos);

                ResolveTargetData(entityManager, assignment.ValueRO.SourceEntity, assignment.ValueRO.SourcePosition,
                    out var sourceKind, out var sourceId, out var sourceSystemId, out var sourceRingIndex, out var sourcePoiKind, out var sourcePos);

                var assignedKind = (byte)0;
                var assignedId = string.Empty;
                if (TryResolveAssignmentEntityId(entityManager, entity, out var resolvedId, out var resolvedKind))
                {
                    assignedId = resolvedId;
                    assignedKind = resolvedKind;
                }

                list.Add(new MissionAssignmentData
                {
                    offerId = assignment.ValueRO.OfferId,
                    type = (byte)assignment.ValueRO.Type,
                    status = (byte)assignment.ValueRO.Status,
                    phase = (byte)assignment.ValueRO.Phase,
                    cargoState = (byte)assignment.ValueRO.CargoState,
                    issuerFactionId = assignment.ValueRO.IssuerFactionId,
                    resourceTypeIndex = assignment.ValueRO.ResourceTypeIndex,
                    units = assignment.ValueRO.Units,
                    cargoUnits = assignment.ValueRO.CargoUnits,
                    rewardCredits = assignment.ValueRO.RewardCredits,
                    rewardStanding = assignment.ValueRO.RewardStanding,
                    rewardLp = assignment.ValueRO.RewardLp,
                    startedTick = assignment.ValueRO.StartedTick,
                    dueTick = assignment.ValueRO.DueTick,
                    completedTick = assignment.ValueRO.CompletedTick,
                    autoComplete = assignment.ValueRO.AutoComplete,
                    targetKind = (byte)targetKind,
                    targetId = targetId,
                    targetSystemId = targetSystemId,
                    targetRingIndex = targetRingIndex,
                    targetPoiKind = targetPoiKind,
                    targetPosition = targetPos,
                    sourceKind = (byte)sourceKind,
                    sourceId = sourceId,
                    sourceSystemId = sourceSystemId,
                    sourceRingIndex = sourceRingIndex,
                    sourcePoiKind = sourcePoiKind,
                    sourcePosition = sourcePos,
                    destinationPosition = ToVector3(assignment.ValueRO.DestinationPosition),
                    assignedEntityId = assignedId,
                    assignedEntityKind = assignedKind
                });
            }

            return list.Count == 0 ? Array.Empty<MissionAssignmentData>() : list.ToArray();
        }

        private void ResolveTargetData(
            EntityManager entityManager,
            Entity targetEntity,
            float3 fallbackPosition,
            out MissionTargetKind kind,
            out string targetId,
            out ushort systemId,
            out byte ringIndex,
            out byte poiKind,
            out Vector3 position)
        {
            kind = MissionTargetKind.None;
            targetId = null;
            systemId = 0;
            ringIndex = 0;
            poiKind = 0;
            position = ToVector3(fallbackPosition);

            if (targetEntity == Entity.Null || !entityManager.Exists(targetEntity))
            {
                return;
            }

            if (entityManager.HasComponent<LocalTransform>(targetEntity))
            {
                position = ToVector3(entityManager.GetComponentData<LocalTransform>(targetEntity).Position);
            }

            if (entityManager.HasComponent<Space4XColony>(targetEntity))
            {
                var colony = entityManager.GetComponentData<Space4XColony>(targetEntity);
                kind = MissionTargetKind.Colony;
                targetId = colony.ColonyId.ToString();
                return;
            }

            if (entityManager.HasComponent<Asteroid>(targetEntity))
            {
                var asteroid = entityManager.GetComponentData<Asteroid>(targetEntity);
                kind = MissionTargetKind.Asteroid;
                targetId = asteroid.AsteroidId.ToString();
                return;
            }

            if (entityManager.HasComponent<Space4XAnomaly>(targetEntity))
            {
                var anomaly = entityManager.GetComponentData<Space4XAnomaly>(targetEntity);
                kind = MissionTargetKind.Anomaly;
                targetId = anomaly.AnomalyId.ToString();
                return;
            }

            if (entityManager.HasComponent<Space4XStarSystem>(targetEntity))
            {
                var system = entityManager.GetComponentData<Space4XStarSystem>(targetEntity);
                kind = MissionTargetKind.System;
                systemId = system.SystemId;
                return;
            }

            if (entityManager.HasComponent<Space4XPoi>(targetEntity))
            {
                var poi = entityManager.GetComponentData<Space4XPoi>(targetEntity);
                kind = MissionTargetKind.Poi;
                systemId = poi.SystemId;
                ringIndex = poi.RingIndex;
                poiKind = (byte)poi.Kind;
                return;
            }

            kind = MissionTargetKind.Unknown;
        }

        private static bool TryResolveAssignmentEntityId(EntityManager entityManager, Entity entity, out string id, out byte kind)
        {
            id = null;
            kind = 0;

            if (entityManager.HasComponent<Carrier>(entity))
            {
                id = entityManager.GetComponentData<Carrier>(entity).CarrierId.ToString();
                kind = 1;
                return true;
            }

            if (entityManager.HasComponent<MiningVessel>(entity))
            {
                id = entityManager.GetComponentData<MiningVessel>(entity).VesselId.ToString();
                kind = 2;
                return true;
            }

            return false;
        }

        private ushort ResolveFactionId(ComponentLookup<Space4XFaction> lookup, Entity entity, ushort fallback)
        {
            if (entity != Entity.Null && lookup.HasComponent(entity))
            {
                return lookup[entity].FactionId;
            }

            return fallback;
        }

        private LeaderData CaptureLeader(EntityManager entityManager, Entity factionEntity)
        {
            if (!entityManager.HasComponent<AuthorityBody>(factionEntity))
            {
                return null;
            }

            var body = entityManager.GetComponentData<AuthorityBody>(factionEntity);
            if (body.ExecutiveSeat == Entity.Null || !entityManager.HasComponent<AuthoritySeatOccupant>(body.ExecutiveSeat))
            {
                return null;
            }

            var occupant = entityManager.GetComponentData<AuthoritySeatOccupant>(body.ExecutiveSeat).OccupantEntity;
            if (occupant == Entity.Null || !entityManager.Exists(occupant))
            {
                return null;
            }

            var data = new LeaderData();

            if (entityManager.HasComponent<IndividualId>(occupant))
            {
                data.id = entityManager.GetComponentData<IndividualId>(occupant).Value;
            }
            else
            {
                data.id = occupant.Index;
            }

            if (entityManager.HasComponent<IndividualName>(occupant))
            {
                data.name = entityManager.GetComponentData<IndividualName>(occupant).Name.ToString();
            }

            if (entityManager.HasComponent<Space4XAlignmentTriplet>(occupant))
            {
                var alignment = entityManager.GetComponentData<Space4XAlignmentTriplet>(occupant);
                data.hasAlignment = 1;
                data.law = (float)alignment.Law;
                data.good = (float)alignment.Good;
                data.integrity = (float)alignment.Integrity;
            }

            if (entityManager.HasComponent<BehaviorDisposition>(occupant))
            {
                var disposition = entityManager.GetComponentData<BehaviorDisposition>(occupant);
                data.hasBehavior = 1;
                data.compliance = disposition.Compliance;
                data.caution = disposition.Caution;
                data.formationAdherence = disposition.FormationAdherence;
                data.riskTolerance = disposition.RiskTolerance;
                data.aggression = disposition.Aggression;
                data.patience = disposition.Patience;
            }

            if (entityManager.HasComponent<Space4XIndividualStats>(occupant))
            {
                var stats = entityManager.GetComponentData<Space4XIndividualStats>(occupant);
                data.hasStats = 1;
                data.command = (float)stats.Command;
                data.tactics = (float)stats.Tactics;
                data.logistics = (float)stats.Logistics;
                data.diplomacy = (float)stats.Diplomacy;
                data.engineering = (float)stats.Engineering;
                data.resolve = (float)stats.Resolve;
            }

            if (entityManager.HasComponent<PhysiqueFinesseWill>(occupant))
            {
                var physique = entityManager.GetComponentData<PhysiqueFinesseWill>(occupant);
                data.hasPhysique = 1;
                data.physique = (float)physique.Physique;
                data.finesse = (float)physique.Finesse;
                data.will = (float)physique.Will;
                data.physiqueInclination = physique.PhysiqueInclination;
                data.finesseInclination = physique.FinesseInclination;
                data.willInclination = physique.WillInclination;
                data.generalXp = physique.GeneralXP;
            }

            if (entityManager.HasComponent<PersonalityAxes>(occupant))
            {
                var personality = entityManager.GetComponentData<PersonalityAxes>(occupant);
                data.hasPersonality = 1;
                data.boldness = personality.Boldness;
                data.vengefulness = personality.Vengefulness;
                data.personalityRisk = personality.RiskTolerance;
                data.selflessness = personality.Selflessness;
                data.conviction = personality.Conviction;
            }

            if (entityManager.HasComponent<PreordainProfile>(occupant))
            {
                data.hasPreordain = 1;
                data.preordainTrack = (byte)entityManager.GetComponentData<PreordainProfile>(occupant).Track;
            }

            return data;
        }
        private bool ApplyLoadData(ref SystemState state, SimSaveData data)
        {
            var entityManager = state.EntityManager;
            var runtimeConfig = SystemAPI.TryGetSingleton(out Space4XSimServerConfig config) ? config : default;

            var taggedQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XSimServerTag>());
            entityManager.DestroyEntity(taggedQuery);

            var configEntity = entityManager.CreateEntity(typeof(Space4XSimServerConfig), typeof(Space4XSimServerTag));
            var restoredConfig = data.config != null ? data.config.ToConfig() : runtimeConfig;
            if (runtimeConfig.HttpPort != 0)
            {
                restoredConfig.HttpPort = runtimeConfig.HttpPort;
            }
            if (runtimeConfig.AutosaveSeconds > 0f)
            {
                restoredConfig.AutosaveSeconds = runtimeConfig.AutosaveSeconds;
            }
            entityManager.SetComponentData(configEntity, restoredConfig);

            entityManager.CreateEntity(typeof(Space4XSimServerGalaxyBootstrapped), typeof(Space4XSimServerTag));

            var factionMap = new Dictionary<ushort, Entity>();
            var pendingFactions = new List<PendingFaction>(data.factions?.Length ?? 0);

            if (data.factions != null)
            {
                foreach (var factionData in data.factions)
                {
                    var factionEntity = CreateFactionEntity(entityManager, factionData);
                    if (factionData != null)
                    {
                        factionMap[factionData.factionId] = factionEntity;
                        pendingFactions.Add(new PendingFaction
                        {
                            entity = factionEntity,
                            data = factionData
                        });
                    }
                }
            }

            foreach (var pending in pendingFactions)
            {
                if (pending.data?.diffusion != null)
                {
                    var diffusion = entityManager.GetComponentData<TechDiffusionState>(pending.entity);
                    diffusion.SourceEntity = ResolveFactionEntity(factionMap, pending.data.diffusion.sourceFactionId, pending.entity);
                    entityManager.SetComponentData(pending.entity, diffusion);
                }

                if (pending.data?.relations != null)
                {
                    var buffer = entityManager.GetBuffer<FactionRelationEntry>(pending.entity);
                    buffer.Clear();
                    foreach (var relationData in pending.data.relations)
                    {
                        var otherEntity = ResolveFactionEntity(factionMap, relationData.otherFactionId, Entity.Null);
                        var relation = relationData.ToRelation(otherEntity);
                        buffer.Add(new FactionRelationEntry { Relation = relation });
                    }
                }

                if (pending.data?.contacts != null && pending.data.contacts.Length > 0)
                {
                    DynamicBuffer<Space4XContactStanding> buffer;
                    if (entityManager.HasBuffer<Space4XContactStanding>(pending.entity))
                    {
                        buffer = entityManager.GetBuffer<Space4XContactStanding>(pending.entity);
                        buffer.Clear();
                    }
                    else
                    {
                        buffer = entityManager.AddBuffer<Space4XContactStanding>(pending.entity);
                    }

                    foreach (var contact in pending.data.contacts)
                    {
                        buffer.Add(new Space4XContactStanding
                        {
                            ContactFactionId = contact.contactFactionId,
                            Standing = (half)math.saturate(contact.standing),
                            LoyaltyPoints = contact.loyaltyPoints,
                            Tier = contact.tier
                        });
                    }
                }

                ApplyReverseEngineeringLoad(entityManager, pending.entity, pending.data);
            }

            if (data.systems != null)
            {
                foreach (var systemData in data.systems)
                {
                    var entity = entityManager.CreateEntity(
                        typeof(Space4XStarSystem),
                        typeof(LocalTransform),
                        typeof(SpatialIndexedTag),
                        typeof(Space4XSimServerTag));

                    entityManager.SetComponentData(entity, new Space4XStarSystem
                    {
                        SystemId = systemData.systemId,
                        OwnerFactionId = systemData.ownerFactionId,
                        RingIndex = systemData.ringIndex
                    });
                    entityManager.SetComponentData(entity, LocalTransform.FromPosition(ToFloat3(systemData.position)));

                    if (systemData.traits != null && systemData.traits.Length > 0)
                    {
                        var buffer = entityManager.AddBuffer<Space4XSystemTrait>(entity);
                        for (int i = 0; i < systemData.traits.Length; i++)
                        {
                            var trait = systemData.traits[i];
                            buffer.Add(new Space4XSystemTrait
                            {
                                Kind = (GalaxySystemTraitKind)trait.kind,
                                Intensity = (half)math.saturate(trait.intensity)
                            });
                        }
                    }
                }
            }

            if (data.colonies != null)
            {
                foreach (var colonyData in data.colonies)
                {
                    var entity = entityManager.CreateEntity(
                        typeof(Space4XColony),
                        typeof(LocalTransform),
                        typeof(SpatialIndexedTag),
                        typeof(Space4XSimServerTag));

                    entityManager.SetComponentData(entity, new Space4XColony
                    {
                        ColonyId = new FixedString64Bytes(colonyData.colonyId ?? string.Empty),
                        Population = colonyData.population,
                        StoredResources = colonyData.storedResources,
                        Status = (Space4XColonyStatus)colonyData.status,
                        SectorId = colonyData.sectorId
                    });
                    entityManager.SetComponentData(entity, LocalTransform.FromPosition(ToFloat3(colonyData.position)));

                    var affiliations = entityManager.AddBuffer<AffiliationTag>(entity);
                    if (colonyData.factionId != 0 && factionMap.TryGetValue(colonyData.factionId, out var factionEntity))
                    {
                        affiliations.Add(new AffiliationTag
                        {
                            Type = AffiliationType.Faction,
                            Target = factionEntity,
                            Loyalty = (half)math.saturate(colonyData.loyalty <= 0f ? 1f : colonyData.loyalty)
                        });
                    }
                }
            }

            if (data.resources != null)
            {
                foreach (var resourceData in data.resources)
                {
                    var entity = entityManager.CreateEntity(
                        typeof(LocalTransform),
                        typeof(SpatialIndexedTag),
                        typeof(Asteroid),
                        typeof(Space4XResourceSourceState),
                        typeof(Space4XResourceSourceConfig),
                        typeof(Space4XResourceTypeId),
                        typeof(RewindableTag),
                        typeof(LastRecordedTick),
                        typeof(HistoryTier),
                        typeof(Space4XAsteroidVolumeConfig),
                        typeof(Space4XAsteroidCenter),
                        typeof(Space4XSimServerTag));

                    entityManager.SetComponentData(entity, LocalTransform.FromPosition(ToFloat3(resourceData.position)));
                    entityManager.SetComponentData(entity, new Asteroid
                    {
                        AsteroidId = new FixedString64Bytes(resourceData.asteroidId ?? string.Empty),
                        ResourceType = (ResourceType)resourceData.resourceType,
                        ResourceAmount = resourceData.resourceAmount,
                        MaxResourceAmount = resourceData.maxResourceAmount,
                        MiningRate = resourceData.miningRate
                    });
                    entityManager.SetComponentData(entity, new Space4XResourceSourceState
                    {
                        UnitsRemaining = resourceData.unitsRemaining,
                        LastHarvestTick = resourceData.lastHarvestTick
                    });
                    entityManager.SetComponentData(entity, new Space4XResourceSourceConfig
                    {
                        GatherRatePerWorker = resourceData.gatherRatePerWorker,
                        MaxSimultaneousWorkers = resourceData.maxWorkers,
                        RespawnSeconds = resourceData.respawnSeconds,
                        Flags = resourceData.flags
                    });
                    entityManager.SetComponentData(entity, new Space4XResourceTypeId
                    {
                        Value = new FixedString64Bytes(resourceData.resourceTypeId ?? string.Empty)
                    });

                    var volume = new Space4XAsteroidVolumeConfig
                    {
                        Radius = resourceData.radius > 0f ? resourceData.radius : Space4XAsteroidVolumeConfig.Default.Radius,
                        CoreRadiusRatio = resourceData.coreRadiusRatio,
                        MantleRadiusRatio = resourceData.mantleRadiusRatio,
                        CrustMaterialId = resourceData.crustMaterialId,
                        MantleMaterialId = resourceData.mantleMaterialId,
                        CoreMaterialId = resourceData.coreMaterialId,
                        CoreDepositId = resourceData.coreDepositId,
                        CoreOreGrade = resourceData.coreOreGrade,
                        OreGradeExponent = resourceData.oreGradeExponent,
                        Seed = resourceData.volumeSeed
                    };
                    entityManager.SetComponentData(entity, volume);

                    var center = resourceData.center.sqrMagnitude > 0f ? resourceData.center : resourceData.position;
                    entityManager.SetComponentData(entity, new Space4XAsteroidCenter
                    {
                        Position = ToFloat3(center)
                    });

                    entityManager.SetComponentData(entity, new HistoryTier
                    {
                        Tier = (HistoryTier.TierType)resourceData.historyTier,
                        OverrideStrideSeconds = resourceData.historyOverrideStride
                    });
                    entityManager.SetComponentData(entity, new LastRecordedTick
                    {
                        Tick = resourceData.lastRecordedTick
                    });

                    if (!entityManager.HasBuffer<ResourceHistorySample>(entity))
                    {
                        entityManager.AddBuffer<ResourceHistorySample>(entity);
                    }
                    if (!entityManager.HasBuffer<Space4XMiningLatchReservation>(entity))
                    {
                        entityManager.AddBuffer<Space4XMiningLatchReservation>(entity);
                    }
                }
            }

            if (data.anomalies != null)
            {
                foreach (var anomalyData in data.anomalies)
                {
                    var entity = entityManager.CreateEntity(
                        typeof(Space4XAnomaly),
                        typeof(LocalTransform),
                        typeof(SpatialIndexedTag),
                        typeof(Space4XSimServerTag));

                    entityManager.SetComponentData(entity, new Space4XAnomaly
                    {
                        AnomalyId = new FixedString64Bytes(anomalyData.anomalyId ?? string.Empty),
                        Classification = new FixedString64Bytes(anomalyData.classification ?? string.Empty),
                        Severity = (Space4XAnomalySeverity)anomalyData.severity,
                        State = (Space4XAnomalyState)anomalyData.state,
                        Instability = anomalyData.instability,
                        SectorId = anomalyData.sectorId
                    });
                    entityManager.SetComponentData(entity, LocalTransform.FromPosition(ToFloat3(anomalyData.position)));
                }
            }

            var colonyById = BuildColonyIdMap(ref state);
            var asteroidById = BuildAsteroidIdMap(ref state);
            var anomalyById = BuildAnomalyIdMap(ref state);
            var systemById = BuildSystemIdMap(ref state);
            var poiByKey = BuildPoiKeyMap(ref state);
            var carrierById = BuildCarrierIdMap(ref state);
            var vesselById = BuildVesselIdMap(ref state);
            var offerEntities = new Dictionary<uint, Entity>();

            if (data.missionOffers != null)
            {
                foreach (var offerData in data.missionOffers)
                {
                    var targetEntity = ResolveTargetEntity(offerData.targetKind, offerData.targetId, offerData.targetSystemId, offerData.targetRingIndex,
                        offerData.targetPoiKind, colonyById, asteroidById, anomalyById, systemById, poiByKey);
                    var targetPosition = ResolveTargetPosition(entityManager, targetEntity, offerData.targetPosition);
                    var issuer = ResolveFactionEntity(factionMap, offerData.issuerFactionId, Entity.Null);

                    var offerEntity = entityManager.CreateEntity(typeof(Space4XMissionOffer));
                    entityManager.SetComponentData(offerEntity, new Space4XMissionOffer
                    {
                        OfferId = offerData.offerId,
                        Type = (Space4XMissionType)offerData.type,
                        Status = (Space4XMissionStatus)offerData.status,
                        Issuer = issuer,
                        IssuerFactionId = offerData.issuerFactionId,
                        TargetEntity = targetEntity,
                        TargetPosition = targetPosition,
                        ResourceTypeIndex = offerData.resourceTypeIndex,
                        Units = offerData.units,
                        RewardCredits = offerData.rewardCredits,
                        RewardStanding = offerData.rewardStanding,
                        RewardLp = offerData.rewardLp,
                        Risk = offerData.risk,
                        Priority = offerData.priority,
                        CreatedTick = offerData.createdTick,
                        ExpiryTick = offerData.expiryTick,
                        AssignedTick = offerData.assignedTick,
                        CompletedTick = offerData.completedTick,
                        AssignedEntity = Entity.Null
                    });

                    if (offerData.offerId != 0)
                    {
                        offerEntities[offerData.offerId] = offerEntity;
                    }
                }
            }

            if (data.missionAssignments != null)
            {
                foreach (var assignmentData in data.missionAssignments)
                {
                    var agent = ResolveAssignedEntity(assignmentData, carrierById, vesselById);
                    if (agent == Entity.Null)
                    {
                        continue;
                    }

                    var targetEntity = ResolveTargetEntity(assignmentData.targetKind, assignmentData.targetId, assignmentData.targetSystemId, assignmentData.targetRingIndex,
                        assignmentData.targetPoiKind, colonyById, asteroidById, anomalyById, systemById, poiByKey);
                    var sourceEntity = ResolveTargetEntity(assignmentData.sourceKind, assignmentData.sourceId, assignmentData.sourceSystemId, assignmentData.sourceRingIndex,
                        assignmentData.sourcePoiKind, colonyById, asteroidById, anomalyById, systemById, poiByKey);
                    var offerEntity = assignmentData.offerId != 0 && offerEntities.TryGetValue(assignmentData.offerId, out var offerRef)
                        ? offerRef
                        : Entity.Null;

                    var assignmentComponent = new Space4XMissionAssignment
                    {
                        OfferEntity = offerEntity,
                        OfferId = assignmentData.offerId,
                        Type = (Space4XMissionType)assignmentData.type,
                        Status = (Space4XMissionStatus)assignmentData.status,
                        TargetEntity = targetEntity,
                        TargetPosition = ToFloat3(assignmentData.targetPosition),
                        SourceEntity = sourceEntity,
                        SourcePosition = ToFloat3(assignmentData.sourcePosition),
                        DestinationPosition = ToFloat3(assignmentData.destinationPosition),
                        Phase = (Space4XMissionPhase)assignmentData.phase,
                        CargoState = (Space4XMissionCargoState)assignmentData.cargoState,
                        ResourceTypeIndex = assignmentData.resourceTypeIndex,
                        Units = assignmentData.units,
                        CargoUnits = assignmentData.cargoUnits,
                        RewardCredits = assignmentData.rewardCredits,
                        RewardStanding = assignmentData.rewardStanding,
                        RewardLp = assignmentData.rewardLp,
                        IssuerFactionId = assignmentData.issuerFactionId,
                        StartedTick = assignmentData.startedTick,
                        DueTick = assignmentData.dueTick,
                        CompletedTick = assignmentData.completedTick,
                        AutoComplete = assignmentData.autoComplete
                    };

                    if (entityManager.HasComponent<Space4XMissionAssignment>(agent))
                    {
                        entityManager.SetComponentData(agent, assignmentComponent);
                    }
                    else
                    {
                        entityManager.AddComponentData(agent, assignmentComponent);
                    }

                    if (entityManager.HasComponent<CaptainOrder>(agent))
                    {
                        var order = entityManager.GetComponentData<CaptainOrder>(agent);
                        order.Type = ResolveMissionOrderType((Space4XMissionType)assignmentData.type);
                        order.Status = CaptainOrderStatus.Received;
                        order.TargetEntity = assignmentComponent.TargetEntity;
                        order.TargetPosition = assignmentComponent.TargetPosition;
                        order.IssuedTick = assignmentComponent.StartedTick;
                        order.TimeoutTick = assignmentComponent.DueTick;
                        entityManager.SetComponentData(agent, order);
                    }
                }
            }

            ApplyTimeState(ref state, data);

            EnsureMissionBoardState(ref state, data);
            return true;
        }

        private Entity CreateFactionEntity(EntityManager entityManager, FactionData data)
        {
            var entity = entityManager.CreateEntity(
                typeof(AffiliationRelation),
                typeof(Space4XFaction),
                typeof(FactionResources),
                typeof(Space4XTerritoryControl),
                typeof(TechLevel),
                typeof(TechDiffusionState),
                typeof(Space4XFactionDirective),
                typeof(Space4XSimServerTag));

            var faction = new Space4XFaction
            {
                Type = (FactionType)data.type,
                Outlook = (FactionOutlook)data.outlook,
                FactionId = data.factionId,
                Aggression = (half)data.aggression,
                RiskTolerance = (half)data.riskTolerance,
                ExpansionDrive = (half)data.expansionDrive,
                TradeFocus = (half)data.tradeFocus,
                ResearchFocus = (half)data.researchFocus,
                MilitaryFocus = (half)data.militaryFocus
            };

            entityManager.SetComponentData(entity, faction);
            entityManager.SetComponentData(entity, new AffiliationRelation
            {
                AffiliationName = new FixedString64Bytes(string.IsNullOrWhiteSpace(data.name) ? $"Empire-{data.factionId:00}" : data.name)
            });

            if (data.resources != null)
            {
                entityManager.SetComponentData(entity, data.resources.ToComponent());
            }

            if (data.territory != null)
            {
                entityManager.SetComponentData(entity, data.territory.ToComponent());
            }

            if (data.tech != null)
            {
                entityManager.SetComponentData(entity, data.tech.ToComponent());
            }

            var diffusion = data.diffusion != null ? data.diffusion.ToComponent() : default;
            diffusion.SourceEntity = Entity.Null;
            entityManager.SetComponentData(entity, diffusion);

            if (data.directive != null)
            {
                entityManager.SetComponentData(entity, data.directive.ToComponent());
            }
            else
            {
                entityManager.SetComponentData(entity, new Space4XFactionDirective
                {
                    Security = (float)faction.MilitaryFocus,
                    Economy = (float)faction.TradeFocus,
                    Research = (float)faction.ResearchFocus,
                    Expansion = (float)faction.ExpansionDrive,
                    Diplomacy = 0.5f,
                    Production = (float)faction.TradeFocus,
                    Food = 0.5f,
                    Priority = 0.25f,
                    LastUpdatedTick = 0,
                    ExpiresAtTick = 0,
                    DirectiveId = new FixedString64Bytes("default")
                });
            }

            if (!entityManager.HasBuffer<FactionRelationEntry>(entity))
            {
                entityManager.AddBuffer<FactionRelationEntry>(entity);
            }

            EnsureFactionLeadership(entityManager, entity, data, faction);

            return entity;
        }

        private void EnsureFactionLeadership(EntityManager entityManager, Entity factionEntity, FactionData data, in Space4XFaction faction)
        {
            if (entityManager.HasComponent<AuthorityBody>(factionEntity))
            {
                return;
            }

            var directive = entityManager.GetComponentData<Space4XFactionDirective>(factionEntity);
            var seats = entityManager.AddBuffer<AuthoritySeatRef>(factionEntity);

            var leaderSeat = entityManager.CreateEntity(
                typeof(AuthoritySeat),
                typeof(AuthoritySeatOccupant),
                typeof(Space4XSimServerTag));

            var roleId = new FixedString64Bytes("faction.leader");
            entityManager.SetComponentData(leaderSeat, AuthoritySeatDefaults.CreateExecutive(factionEntity, roleId, AgencyDomain.Governance));
            entityManager.SetComponentData(leaderSeat, AuthoritySeatDefaults.Vacant(0u));

            var leader = entityManager.CreateEntity(typeof(SimIndividualTag), typeof(Space4XSimServerTag));
            ApplyLeaderSnapshot(entityManager, leader, data?.leader, faction, directive);

            var affiliations = entityManager.AddBuffer<AffiliationTag>(leader);
            affiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = factionEntity,
                Loyalty = (half)1f
            });

            entityManager.SetComponentData(leaderSeat, new AuthoritySeatOccupant
            {
                OccupantEntity = leader,
                AssignedTick = 0,
                LastChangedTick = 0,
                IsActing = 0
            });

            entityManager.AddComponentData(factionEntity, new AuthorityBody
            {
                Mode = AuthorityBodyMode.SingleExecutive,
                ExecutiveSeat = leaderSeat,
                CreatedTick = 0
            });

            seats.Add(new AuthoritySeatRef { SeatEntity = leaderSeat });
        }

        private void ApplyReverseEngineeringLoad(EntityManager entityManager, Entity factionEntity, FactionData data)
        {
            if (data == null)
            {
                return;
            }

            if (data.reverseEngineeringState != null)
            {
                if (entityManager.HasComponent<ReverseEngineeringState>(factionEntity))
                {
                    entityManager.SetComponentData(factionEntity, data.reverseEngineeringState.ToComponent());
                }
                else
                {
                    entityManager.AddComponentData(factionEntity, data.reverseEngineeringState.ToComponent());
                }
            }
            else if (!entityManager.HasComponent<ReverseEngineeringState>(factionEntity))
            {
                entityManager.AddComponentData(factionEntity, new ReverseEngineeringState
                {
                    NextTaskId = 1,
                    NextVariantId = 1
                });
            }

            var evidenceBuffer = entityManager.HasBuffer<ReverseEngineeringEvidence>(factionEntity)
                ? entityManager.GetBuffer<ReverseEngineeringEvidence>(factionEntity)
                : entityManager.AddBuffer<ReverseEngineeringEvidence>(factionEntity);
            evidenceBuffer.Clear();
            if (data.reverseEngineeringEvidence != null)
            {
                foreach (var entry in data.reverseEngineeringEvidence)
                {
                    evidenceBuffer.Add(entry.ToBuffer());
                }
            }

            var taskBuffer = entityManager.HasBuffer<ReverseEngineeringTask>(factionEntity)
                ? entityManager.GetBuffer<ReverseEngineeringTask>(factionEntity)
                : entityManager.AddBuffer<ReverseEngineeringTask>(factionEntity);
            taskBuffer.Clear();
            if (data.reverseEngineeringTasks != null)
            {
                foreach (var entry in data.reverseEngineeringTasks)
                {
                    taskBuffer.Add(entry.ToBuffer());
                }
            }

            var variantBuffer = entityManager.HasBuffer<ReverseEngineeringBlueprintVariant>(factionEntity)
                ? entityManager.GetBuffer<ReverseEngineeringBlueprintVariant>(factionEntity)
                : entityManager.AddBuffer<ReverseEngineeringBlueprintVariant>(factionEntity);
            variantBuffer.Clear();
            if (data.reverseEngineeringVariants != null)
            {
                foreach (var entry in data.reverseEngineeringVariants)
                {
                    variantBuffer.Add(entry.ToBuffer());
                }
            }

            var progressBuffer = entityManager.HasBuffer<ReverseEngineeringBlueprintProgress>(factionEntity)
                ? entityManager.GetBuffer<ReverseEngineeringBlueprintProgress>(factionEntity)
                : entityManager.AddBuffer<ReverseEngineeringBlueprintProgress>(factionEntity);
            progressBuffer.Clear();
            if (data.reverseEngineeringProgress != null)
            {
                foreach (var entry in data.reverseEngineeringProgress)
                {
                    progressBuffer.Add(entry.ToBuffer());
                }
            }
        }

        private void ApplyLeaderSnapshot(
            EntityManager entityManager,
            Entity leader,
            LeaderData data,
            in Space4XFaction faction,
            in Space4XFactionDirective directive)
        {
            var leaderId = data != null && data.id != 0 ? data.id : faction.FactionId * 1000 + 1;
            entityManager.AddComponentData(leader, new IndividualId { Value = leaderId });

            var name = data?.name;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Leader-{faction.FactionId:00}";
            }
            entityManager.AddComponentData(leader, new IndividualName { Name = new FixedString64Bytes(name) });

            if (data != null && data.hasAlignment != 0)
            {
                entityManager.AddComponentData(leader, Space4XAlignmentTriplet.FromFloats(data.law, data.good, data.integrity));
            }
            else
            {
                entityManager.AddComponentData(leader, Space4XAlignmentTriplet.FromFloats(0f, 0f, 0f));
            }

            if (data != null && data.hasStats != 0)
            {
                entityManager.AddComponentData(leader, new Space4XIndividualStats
                {
                    Command = (half)math.clamp(data.command, 0f, 100f),
                    Tactics = (half)math.clamp(data.tactics, 0f, 100f),
                    Logistics = (half)math.clamp(data.logistics, 0f, 100f),
                    Diplomacy = (half)math.clamp(data.diplomacy, 0f, 100f),
                    Engineering = (half)math.clamp(data.engineering, 0f, 100f),
                    Resolve = (half)math.clamp(data.resolve, 0f, 100f)
                });
            }
            else
            {
                entityManager.AddComponentData(leader, new Space4XIndividualStats
                {
                    Command = (half)65f,
                    Tactics = (half)60f,
                    Logistics = (half)60f,
                    Diplomacy = (half)55f,
                    Engineering = (half)50f,
                    Resolve = (half)60f
                });
            }

            if (data != null && data.hasPhysique != 0)
            {
                entityManager.AddComponentData(leader, new PhysiqueFinesseWill
                {
                    Physique = (half)math.clamp(data.physique, 0f, 100f),
                    Finesse = (half)math.clamp(data.finesse, 0f, 100f),
                    Will = (half)math.clamp(data.will, 0f, 100f),
                    PhysiqueInclination = data.physiqueInclination,
                    FinesseInclination = data.finesseInclination,
                    WillInclination = data.willInclination,
                    GeneralXP = data.generalXp
                });
            }
            else
            {
                entityManager.AddComponentData(leader, new PhysiqueFinesseWill
                {
                    Physique = (half)50f,
                    Finesse = (half)50f,
                    Will = (half)50f,
                    PhysiqueInclination = 5,
                    FinesseInclination = 5,
                    WillInclination = 5,
                    GeneralXP = 0f
                });
            }

            if (data != null && data.hasPersonality != 0)
            {
                entityManager.AddComponentData(leader, PersonalityAxes.FromValues(
                    data.boldness,
                    data.vengefulness,
                    data.personalityRisk,
                    data.selflessness,
                    data.conviction));
            }
            else
            {
                entityManager.AddComponentData(leader, PersonalityAxes.FromValues(0f, 0f, 0f, 0f, 0f));
            }

            if (data != null && data.hasPreordain != 0)
            {
                entityManager.AddComponentData(leader, new PreordainProfile
                {
                    Track = (PreordainTrack)data.preordainTrack
                });
            }

            var disposition = data != null && data.hasBehavior != 0
                ? BehaviorDisposition.FromValues(
                    data.compliance,
                    data.caution,
                    data.formationAdherence,
                    data.riskTolerance,
                    data.aggression,
                    data.patience)
                : Space4XSimServerProfileUtility.BuildLeaderDisposition(
                    directive.Security,
                    directive.Economy,
                    directive.Research,
                    directive.Expansion,
                    directive.Diplomacy,
                    math.saturate((float)faction.Aggression),
                    math.saturate((float)faction.RiskTolerance),
                    directive.Food);

            entityManager.AddComponentData(leader, disposition);
        }

        private Entity ResolveFactionEntity(Dictionary<ushort, Entity> map, ushort id, Entity fallback)
        {
            return id != 0 && map.TryGetValue(id, out var entity) ? entity : fallback;
        }

        private void ApplyTimeState(ref SystemState state, SimSaveData data)
        {
            var timeData = data.time ?? new SimTimeData();
            var fixedDelta = timeData.fixedDeltaTime > 0f ? timeData.fixedDeltaTime : 0.016f;
            var scale = ResolveTimeScale(timeData);

            if (SystemAPI.TryGetSingletonEntity<TimeState>(out var timeEntity))
            {
                var timeState = state.EntityManager.GetComponentData<TimeState>(timeEntity);
                timeState.Tick = timeData.tick;
                timeState.FixedDeltaTime = fixedDelta;
                timeState.WorldSeconds = timeData.worldSeconds > 0f ? timeData.worldSeconds : timeData.tick * fixedDelta;
                timeState.ElapsedTime = timeData.elapsedTime > 0f ? timeData.elapsedTime : timeState.WorldSeconds;
                timeState.CurrentSpeedMultiplier = scale;
                timeState.DeltaTime = fixedDelta * scale;
                timeState.DeltaSeconds = timeState.DeltaTime;
                timeState.IsPaused = timeData.isPaused || scale <= math.FLT_MIN_NORMAL;
                state.EntityManager.SetComponentData(timeEntity, timeState);
            }

            if (SystemAPI.TryGetSingletonEntity<TickTimeState>(out var tickEntity))
            {
                var tickState = state.EntityManager.GetComponentData<TickTimeState>(tickEntity);
                tickState.Tick = timeData.tick;
                tickState.FixedDeltaTime = fixedDelta;
                tickState.CurrentSpeedMultiplier = scale;
                tickState.TargetTick = timeData.tick;
                tickState.IsPaused = timeData.isPaused || scale <= math.FLT_MIN_NORMAL;
                tickState.IsPlaying = !tickState.IsPaused;
                tickState.WorldSeconds = timeData.worldSeconds > 0f ? timeData.worldSeconds : timeData.tick * fixedDelta;
                state.EntityManager.SetComponentData(tickEntity, tickState);
            }

            if (SystemAPI.TryGetSingletonEntity<SimulationScalars>(out var scalarsEntity))
            {
                var scalars = state.EntityManager.GetComponentData<SimulationScalars>(scalarsEntity);
                scalars.TimeScale = scale;
                state.EntityManager.SetComponentData(scalarsEntity, scalars);
            }

            if (SystemAPI.TryGetSingletonEntity<SimulationOverrides>(out var overridesEntity))
            {
                var overrides = state.EntityManager.GetComponentData<SimulationOverrides>(overridesEntity);
                overrides.OverrideTimeScale = true;
                overrides.TimeScaleOverride = scale;
                state.EntityManager.SetComponentData(overridesEntity, overrides);
            }

            if (SystemAPI.TryGetSingletonEntity<TimeScaleConfig>(out var configEntity))
            {
                var config = state.EntityManager.GetComponentData<TimeScaleConfig>(configEntity);
                config.DefaultScale = scale;
                state.EntityManager.SetComponentData(configEntity, config);
            }

            if (SystemAPI.TryGetSingletonEntity<TimeScaleScheduleState>(out var scheduleEntity))
            {
                var schedule = state.EntityManager.GetComponentData<TimeScaleScheduleState>(scheduleEntity);
                schedule.ResolvedScale = scale;
                schedule.IsPaused = timeData.isPaused || scale <= math.FLT_MIN_NORMAL;
                schedule.ActiveEntryId = 0;
                schedule.ActiveSource = TimeScaleSource.Default;
                state.EntityManager.SetComponentData(scheduleEntity, schedule);
            }
        }

        private void EnsureMissionBoardState(ref SystemState state, SimSaveData data)
        {
            var maxOfferId = 0u;
            if (data.missionOffers != null)
            {
                for (int i = 0; i < data.missionOffers.Length; i++)
                {
                    maxOfferId = math.max(maxOfferId, data.missionOffers[i].offerId);
                }
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XMissionBoardConfig>(out var configEntity))
            {
                configEntity = state.EntityManager.CreateEntity(typeof(Space4XMissionBoardConfig), typeof(Space4XMissionBoardState));
                state.EntityManager.SetComponentData(configEntity, Space4XMissionBoardConfig.Default);
                state.EntityManager.SetComponentData(configEntity, new Space4XMissionBoardState
                {
                    LastGenerationTick = data.time?.tick ?? 0u,
                    NextOfferId = maxOfferId + 1u
                });
                return;
            }

            if (!state.EntityManager.HasComponent<Space4XMissionBoardState>(configEntity))
            {
                state.EntityManager.AddComponentData(configEntity, new Space4XMissionBoardState
                {
                    LastGenerationTick = data.time?.tick ?? 0u,
                    NextOfferId = maxOfferId + 1u
                });
                return;
            }

            var boardState = state.EntityManager.GetComponentData<Space4XMissionBoardState>(configEntity);
            boardState.LastGenerationTick = data.time?.tick ?? boardState.LastGenerationTick;
            boardState.NextOfferId = math.max(boardState.NextOfferId, maxOfferId + 1u);
            state.EntityManager.SetComponentData(configEntity, boardState);
        }

        private Dictionary<string, Entity> BuildColonyIdMap(ref SystemState state)
        {
            var map = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>().WithEntityAccess())
            {
                map[colony.ValueRO.ColonyId.ToString()] = entity;
            }
            return map;
        }

        private Dictionary<string, Entity> BuildAsteroidIdMap(ref SystemState state)
        {
            var map = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (var (asteroid, entity) in SystemAPI.Query<RefRO<Asteroid>>().WithEntityAccess())
            {
                map[asteroid.ValueRO.AsteroidId.ToString()] = entity;
            }
            return map;
        }

        private Dictionary<string, Entity> BuildAnomalyIdMap(ref SystemState state)
        {
            var map = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (var (anomaly, entity) in SystemAPI.Query<RefRO<Space4XAnomaly>>().WithEntityAccess())
            {
                map[anomaly.ValueRO.AnomalyId.ToString()] = entity;
            }
            return map;
        }

        private Dictionary<ushort, Entity> BuildSystemIdMap(ref SystemState state)
        {
            var map = new Dictionary<ushort, Entity>();
            foreach (var (system, entity) in SystemAPI.Query<RefRO<Space4XStarSystem>>().WithEntityAccess())
            {
                map[system.ValueRO.SystemId] = entity;
            }
            return map;
        }

        private Dictionary<int, Entity> BuildPoiKeyMap(ref SystemState state)
        {
            var map = new Dictionary<int, Entity>();
            foreach (var (poi, entity) in SystemAPI.Query<RefRO<Space4XPoi>>().WithEntityAccess())
            {
                var key = BuildPoiKey(poi.ValueRO.SystemId, poi.ValueRO.RingIndex, (byte)poi.ValueRO.Kind);
                if (!map.ContainsKey(key))
                {
                    map[key] = entity;
                }
            }
            return map;
        }

        private Dictionary<string, Entity> BuildCarrierIdMap(ref SystemState state)
        {
            var map = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                map[carrier.ValueRO.CarrierId.ToString()] = entity;
            }
            return map;
        }

        private Dictionary<string, Entity> BuildVesselIdMap(ref SystemState state)
        {
            var map = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (var (vessel, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithEntityAccess())
            {
                map[vessel.ValueRO.VesselId.ToString()] = entity;
            }
            return map;
        }

        private static int BuildPoiKey(ushort systemId, byte ringIndex, byte poiKind)
        {
            return (systemId << 16) ^ (ringIndex << 8) ^ poiKind;
        }

        private static Entity ResolveTargetEntity(byte kindValue, string targetId, ushort systemId, byte ringIndex, byte poiKind,
            Dictionary<string, Entity> colonyMap,
            Dictionary<string, Entity> asteroidMap,
            Dictionary<string, Entity> anomalyMap,
            Dictionary<ushort, Entity> systemMap,
            Dictionary<int, Entity> poiMap)
        {
            var kind = (MissionTargetKind)kindValue;
            switch (kind)
            {
                case MissionTargetKind.Colony:
                    if (!string.IsNullOrWhiteSpace(targetId) && colonyMap.TryGetValue(targetId, out var colony))
                    {
                        return colony;
                    }
                    break;
                case MissionTargetKind.Asteroid:
                    if (!string.IsNullOrWhiteSpace(targetId) && asteroidMap.TryGetValue(targetId, out var asteroid))
                    {
                        return asteroid;
                    }
                    break;
                case MissionTargetKind.Anomaly:
                    if (!string.IsNullOrWhiteSpace(targetId) && anomalyMap.TryGetValue(targetId, out var anomaly))
                    {
                        return anomaly;
                    }
                    break;
                case MissionTargetKind.System:
                    if (systemId != 0 && systemMap.TryGetValue(systemId, out var system))
                    {
                        return system;
                    }
                    break;
                case MissionTargetKind.Poi:
                    var key = BuildPoiKey(systemId, ringIndex, poiKind);
                    if (poiMap.TryGetValue(key, out var poi))
                    {
                        return poi;
                    }
                    break;
            }

            return Entity.Null;
        }

        private float3 ResolveTargetPosition(EntityManager entityManager, Entity targetEntity, Vector3 fallback)
        {
            if (targetEntity != Entity.Null && entityManager.Exists(targetEntity) && entityManager.HasComponent<LocalTransform>(targetEntity))
            {
                return entityManager.GetComponentData<LocalTransform>(targetEntity).Position;
            }

            return ToFloat3(fallback);
        }

        private static Entity ResolveAssignedEntity(MissionAssignmentData assignmentData, Dictionary<string, Entity> carrierMap, Dictionary<string, Entity> vesselMap)
        {
            if (string.IsNullOrWhiteSpace(assignmentData.assignedEntityId))
            {
                return Entity.Null;
            }

            if (assignmentData.assignedEntityKind == 1 && carrierMap.TryGetValue(assignmentData.assignedEntityId, out var carrier))
            {
                return carrier;
            }

            if (assignmentData.assignedEntityKind == 2 && vesselMap.TryGetValue(assignmentData.assignedEntityId, out var vessel))
            {
                return vessel;
            }

            return Entity.Null;
        }

        private static CaptainOrderType ResolveMissionOrderType(Space4XMissionType type)
        {
            return type switch
            {
                Space4XMissionType.Scout => CaptainOrderType.Survey,
                Space4XMissionType.Mine => CaptainOrderType.Mine,
                Space4XMissionType.HaulDelivery => CaptainOrderType.Haul,
                Space4XMissionType.HaulProcure => CaptainOrderType.Haul,
                Space4XMissionType.Patrol => CaptainOrderType.Patrol,
                Space4XMissionType.Intercept => CaptainOrderType.Intercept,
                Space4XMissionType.BuildStation => CaptainOrderType.Construct,
                _ => CaptainOrderType.MoveTo
            };
        }

        private float ResolveTimeScale(SimTimeData timeData)
        {
            var scale = timeData.timeScale;
            if (scale <= math.FLT_MIN_NORMAL)
            {
                scale = timeData.currentSpeedMultiplier;
            }

            if (scale <= math.FLT_MIN_NORMAL)
            {
                scale = 1f;
            }

            return math.clamp(scale, 0.01f, 16f);
        }

        private Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }
        [Serializable]
        private sealed class SaveRequest
        {
            public string slot;
            public string name;
            public string file;
            public bool overwrite;

            public string ResolveSlot()
            {
                if (!string.IsNullOrWhiteSpace(slot))
                {
                    return slot;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }

                return file;
            }
        }

        [Serializable]
        private sealed class LoadRequest
        {
            public string slot;
            public string name;
            public string file;
            public bool latest;
            public bool newest;

            public string ResolveSlot()
            {
                if (!string.IsNullOrWhiteSpace(slot))
                {
                    return slot;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }

                return file;
            }

            public bool ResolveLatest()
            {
                return latest || newest;
            }
        }

        [Serializable]
        private sealed class SimSaveData
        {
            public int version;
            public string createdUtc;
            public SimTimeData time;
            public SimConfigData config;
            public FactionData[] factions;
            public SystemData[] systems;
            public ColonyData[] colonies;
            public ResourceData[] resources;
            public AnomalyData[] anomalies;
            public MissionOfferData[] missionOffers;
            public MissionAssignmentData[] missionAssignments;
        }

        [Serializable]
        private sealed class SimTimeData
        {
            public uint tick;
            public float worldSeconds;
            public float fixedDeltaTime;
            public float currentSpeedMultiplier;
            public float elapsedTime;
            public float deltaTime;
            public float deltaSeconds;
            public bool isPaused;
            public float timeScale;
        }

        [Serializable]
        private sealed class SimConfigData
        {
            public uint seed;
            public ushort factionCount;
            public ushort systemsPerFaction;
            public ushort resourcesPerSystem;
            public float startRadius;
            public float systemSpacing;
            public float resourceBaseUnits;
            public float resourceRichnessGradient;
            public float techDiffusionDurationSeconds;
            public float targetTicksPerSecond;
            public ushort httpPort;
            public float autosaveSeconds;
            public ushort traitMask;
            public ushort poiMask;
            public byte maxTraitsPerSystem;
            public byte maxPoisPerSystem;
            public float traitChanceBase;
            public float traitChancePerRing;
            public float traitChanceMax;
            public float poiChanceBase;
            public float poiChancePerRing;
            public float poiChanceMax;
            public float poiOffsetMin;
            public float poiOffsetMax;

            public static SimConfigData FromConfig(Space4XSimServerConfig config)
            {
                return new SimConfigData
                {
                    seed = config.Seed,
                    factionCount = config.FactionCount,
                    systemsPerFaction = config.SystemsPerFaction,
                    resourcesPerSystem = config.ResourcesPerSystem,
                    startRadius = config.StartRadius,
                    systemSpacing = config.SystemSpacing,
                    resourceBaseUnits = config.ResourceBaseUnits,
                    resourceRichnessGradient = config.ResourceRichnessGradient,
                    techDiffusionDurationSeconds = config.TechDiffusionDurationSeconds,
                    targetTicksPerSecond = config.TargetTicksPerSecond,
                    httpPort = config.HttpPort,
                    autosaveSeconds = config.AutosaveSeconds,
                    traitMask = (ushort)config.TraitMask,
                    poiMask = (ushort)config.PoiMask,
                    maxTraitsPerSystem = config.MaxTraitsPerSystem,
                    maxPoisPerSystem = config.MaxPoisPerSystem,
                    traitChanceBase = config.TraitChanceBase,
                    traitChancePerRing = config.TraitChancePerRing,
                    traitChanceMax = config.TraitChanceMax,
                    poiChanceBase = config.PoiChanceBase,
                    poiChancePerRing = config.PoiChancePerRing,
                    poiChanceMax = config.PoiChanceMax,
                    poiOffsetMin = config.PoiOffsetMin,
                    poiOffsetMax = config.PoiOffsetMax
                };
            }

            public Space4XSimServerConfig ToConfig()
            {
                var resolvedTraitMask = traitMask == 0 ? GalaxySystemTraitMask.All : (GalaxySystemTraitMask)traitMask;
                var resolvedPoiMask = poiMask == 0 ? GalaxyPoiMask.All : (GalaxyPoiMask)poiMask;

                var resolvedMaxTraits = maxTraitsPerSystem == 0 ? (byte)1 : maxTraitsPerSystem;
                var resolvedMaxPois = maxPoisPerSystem == 0 ? (byte)1 : maxPoisPerSystem;

                var resolvedTraitChanceBase = traitChanceBase <= 0f ? 0.35f : traitChanceBase;
                var resolvedTraitChancePerRing = traitChancePerRing <= 0f ? 0.1f : traitChancePerRing;
                var resolvedTraitChanceMax = traitChanceMax <= 0f ? 0.8f : traitChanceMax;

                var resolvedPoiChanceBase = poiChanceBase <= 0f ? 0.25f : poiChanceBase;
                var resolvedPoiChancePerRing = poiChancePerRing <= 0f ? 0.12f : poiChancePerRing;
                var resolvedPoiChanceMax = poiChanceMax <= 0f ? 0.75f : poiChanceMax;

                var resolvedPoiOffsetMin = poiOffsetMin <= 0f ? 450f : poiOffsetMin;
                var resolvedPoiOffsetMax = poiOffsetMax <= 0f ? 900f : poiOffsetMax;

                return new Space4XSimServerConfig
                {
                    Seed = seed,
                    FactionCount = factionCount,
                    SystemsPerFaction = systemsPerFaction,
                    ResourcesPerSystem = resourcesPerSystem,
                    StartRadius = startRadius,
                    SystemSpacing = systemSpacing,
                    ResourceBaseUnits = resourceBaseUnits,
                    ResourceRichnessGradient = resourceRichnessGradient,
                    TechDiffusionDurationSeconds = techDiffusionDurationSeconds,
                    TargetTicksPerSecond = targetTicksPerSecond,
                    HttpPort = httpPort,
                    AutosaveSeconds = autosaveSeconds,
                    TraitMask = resolvedTraitMask,
                    PoiMask = resolvedPoiMask,
                    MaxTraitsPerSystem = resolvedMaxTraits,
                    MaxPoisPerSystem = resolvedMaxPois,
                    TraitChanceBase = resolvedTraitChanceBase,
                    TraitChancePerRing = resolvedTraitChancePerRing,
                    TraitChanceMax = resolvedTraitChanceMax,
                    PoiChanceBase = resolvedPoiChanceBase,
                    PoiChancePerRing = resolvedPoiChancePerRing,
                    PoiChanceMax = resolvedPoiChanceMax,
                    PoiOffsetMin = resolvedPoiOffsetMin,
                    PoiOffsetMax = resolvedPoiOffsetMax
                };
            }
        }
        [Serializable]
        private sealed class FactionData
        {
            public ushort factionId;
            public byte type;
            public ushort outlook;
            public float aggression;
            public float riskTolerance;
            public float expansionDrive;
            public float tradeFocus;
            public float researchFocus;
            public float militaryFocus;
            public string name;
            public FactionResourcesData resources;
            public TerritoryData territory;
            public TechLevelData tech;
            public TechDiffusionData diffusion;
            public DirectiveData directive;
            public LeaderData leader;
            public RelationData[] relations;
            public ContactData[] contacts;
            public ReverseEngineeringStateData reverseEngineeringState;
            public ReverseEngineeringEvidenceData[] reverseEngineeringEvidence;
            public ReverseEngineeringTaskData[] reverseEngineeringTasks;
            public ReverseEngineeringVariantData[] reverseEngineeringVariants;
            public ReverseEngineeringProgressData[] reverseEngineeringProgress;
        }

        [Serializable]
        private sealed class LeaderData
        {
            public int id;
            public string name;
            public byte hasAlignment;
            public float law;
            public float good;
            public float integrity;
            public byte hasBehavior;
            public float compliance;
            public float caution;
            public float formationAdherence;
            public float riskTolerance;
            public float aggression;
            public float patience;
            public byte hasStats;
            public float command;
            public float tactics;
            public float logistics;
            public float diplomacy;
            public float engineering;
            public float resolve;
            public byte hasPhysique;
            public float physique;
            public float finesse;
            public float will;
            public byte physiqueInclination;
            public byte finesseInclination;
            public byte willInclination;
            public float generalXp;
            public byte hasPersonality;
            public float boldness;
            public float vengefulness;
            public float personalityRisk;
            public float selflessness;
            public float conviction;
            public byte hasPreordain;
            public byte preordainTrack;
        }

        [Serializable]
        private sealed class FactionResourcesData
        {
            public float credits;
            public float materials;
            public float energy;
            public float influence;
            public float research;
            public float incomeRate;
            public float expenseRate;

            public static FactionResourcesData From(FactionResources resources)
            {
                return new FactionResourcesData
                {
                    credits = resources.Credits,
                    materials = resources.Materials,
                    energy = resources.Energy,
                    influence = resources.Influence,
                    research = resources.Research,
                    incomeRate = resources.IncomeRate,
                    expenseRate = resources.ExpenseRate
                };
            }

            public FactionResources ToComponent()
            {
                return new FactionResources
                {
                    Credits = credits,
                    Materials = materials,
                    Energy = energy,
                    Influence = influence,
                    Research = research,
                    IncomeRate = incomeRate,
                    ExpenseRate = expenseRate
                };
            }
        }

        [Serializable]
        private sealed class TerritoryData
        {
            public ushort controlledSystems;
            public ushort colonyCount;
            public ushort outpostCount;
            public ushort contestedSectors;
            public float fleetStrength;
            public float economicOutput;
            public uint population;
            public float expansionRate;

            public static TerritoryData From(Space4XTerritoryControl territory)
            {
                return new TerritoryData
                {
                    controlledSystems = territory.ControlledSystems,
                    colonyCount = territory.ColonyCount,
                    outpostCount = territory.OutpostCount,
                    contestedSectors = territory.ContestedSectors,
                    fleetStrength = territory.FleetStrength,
                    economicOutput = territory.EconomicOutput,
                    population = territory.Population,
                    expansionRate = (float)territory.ExpansionRate
                };
            }

            public Space4XTerritoryControl ToComponent()
            {
                return new Space4XTerritoryControl
                {
                    ControlledSystems = controlledSystems,
                    ColonyCount = colonyCount,
                    OutpostCount = outpostCount,
                    ContestedSectors = contestedSectors,
                    FleetStrength = fleetStrength,
                    EconomicOutput = economicOutput,
                    Population = population,
                    ExpansionRate = (half)expansionRate
                };
            }
        }

        [Serializable]
        private sealed class TechLevelData
        {
            public byte miningTech;
            public byte combatTech;
            public byte haulingTech;
            public byte processingTech;
            public uint lastUpgradeTick;

            public static TechLevelData From(TechLevel level)
            {
                return new TechLevelData
                {
                    miningTech = level.MiningTech,
                    combatTech = level.CombatTech,
                    haulingTech = level.HaulingTech,
                    processingTech = level.ProcessingTech,
                    lastUpgradeTick = level.LastUpgradeTick
                };
            }

            public TechLevel ToComponent()
            {
                return new TechLevel
                {
                    MiningTech = miningTech,
                    CombatTech = combatTech,
                    HaulingTech = haulingTech,
                    ProcessingTech = processingTech,
                    LastUpgradeTick = lastUpgradeTick
                };
            }
        }

        [Serializable]
        private sealed class TechDiffusionData
        {
            public ushort sourceFactionId;
            public float diffusionProgressSeconds;
            public float diffusionDurationSeconds;
            public byte targetMiningTech;
            public byte targetCombatTech;
            public byte targetHaulingTech;
            public byte targetProcessingTech;
            public byte active;
            public uint diffusionStartTick;

            public static TechDiffusionData From(TechDiffusionState state, ushort sourceFactionId)
            {
                return new TechDiffusionData
                {
                    sourceFactionId = sourceFactionId,
                    diffusionProgressSeconds = state.DiffusionProgressSeconds,
                    diffusionDurationSeconds = state.DiffusionDurationSeconds,
                    targetMiningTech = state.TargetMiningTech,
                    targetCombatTech = state.TargetCombatTech,
                    targetHaulingTech = state.TargetHaulingTech,
                    targetProcessingTech = state.TargetProcessingTech,
                    active = state.Active,
                    diffusionStartTick = state.DiffusionStartTick
                };
            }

            public TechDiffusionState ToComponent()
            {
                return new TechDiffusionState
                {
                    DiffusionProgressSeconds = diffusionProgressSeconds,
                    DiffusionDurationSeconds = diffusionDurationSeconds,
                    TargetMiningTech = targetMiningTech,
                    TargetCombatTech = targetCombatTech,
                    TargetHaulingTech = targetHaulingTech,
                    TargetProcessingTech = targetProcessingTech,
                    Active = active,
                    DiffusionStartTick = diffusionStartTick
                };
            }
        }

        [Serializable]
        private sealed class DirectiveData
        {
            public float security;
            public float economy;
            public float research;
            public float expansion;
            public float diplomacy;
            public float production;
            public float food;
            public float priority;
            public uint lastUpdatedTick;
            public uint expiresAtTick;
            public string directiveId;

            public static DirectiveData From(Space4XFactionDirective directive)
            {
                return new DirectiveData
                {
                    security = directive.Security,
                    economy = directive.Economy,
                    research = directive.Research,
                    expansion = directive.Expansion,
                    diplomacy = directive.Diplomacy,
                    production = directive.Production,
                    food = directive.Food,
                    priority = directive.Priority,
                    lastUpdatedTick = directive.LastUpdatedTick,
                    expiresAtTick = directive.ExpiresAtTick,
                    directiveId = directive.DirectiveId.ToString()
                };
            }

            public Space4XFactionDirective ToComponent()
            {
                return new Space4XFactionDirective
                {
                    Security = security,
                    Economy = economy,
                    Research = research,
                    Expansion = expansion,
                    Diplomacy = diplomacy,
                    Production = production,
                    Food = food,
                    Priority = priority,
                    LastUpdatedTick = lastUpdatedTick,
                    ExpiresAtTick = expiresAtTick,
                    DirectiveId = new FixedString64Bytes(directiveId ?? string.Empty)
                };
            }
        }

        [Serializable]
        private sealed class RelationData
        {
            public ushort otherFactionId;
            public sbyte score;
            public float trust;
            public float fear;
            public float respect;
            public float tradeVolume;
            public uint recentCombats;
            public uint lastInteractionTick;

            public static RelationData From(FactionRelation relation, ushort otherId)
            {
                return new RelationData
                {
                    otherFactionId = otherId,
                    score = relation.Score,
                    trust = (float)relation.Trust,
                    fear = (float)relation.Fear,
                    respect = (float)relation.Respect,
                    tradeVolume = relation.TradeVolume,
                    recentCombats = relation.RecentCombats,
                    lastInteractionTick = relation.LastInteractionTick
                };
            }

            public FactionRelation ToRelation(Entity other)
            {
                return new FactionRelation
                {
                    OtherFaction = other,
                    OtherFactionId = otherFactionId,
                    Score = score,
                    Trust = (half)trust,
                    Fear = (half)fear,
                    Respect = (half)respect,
                    TradeVolume = tradeVolume,
                    RecentCombats = recentCombats,
                    LastInteractionTick = lastInteractionTick
                };
            }
        }

        [Serializable]
        private sealed class SystemData
        {
            public ushort systemId;
            public ushort ownerFactionId;
            public byte ringIndex;
            public Vector3 position;
            public SystemTraitData[] traits;
        }

        [Serializable]
        private sealed class SystemTraitData
        {
            public byte kind;
            public float intensity;
        }

        [Serializable]
        private sealed class ColonyData
        {
            public string colonyId;
            public float population;
            public float storedResources;
            public byte status;
            public int sectorId;
            public Vector3 position;
            public ushort factionId;
            public float loyalty;
        }

        [Serializable]
        private sealed class ResourceData
        {
            public string asteroidId;
            public byte resourceType;
            public float resourceAmount;
            public float maxResourceAmount;
            public float miningRate;
            public float unitsRemaining;
            public uint lastHarvestTick;
            public float gatherRatePerWorker;
            public ushort maxWorkers;
            public float respawnSeconds;
            public byte flags;
            public string resourceTypeId;
            public Vector3 position;
            public Vector3 center;
            public float radius;
            public float coreRadiusRatio;
            public float mantleRadiusRatio;
            public byte crustMaterialId;
            public byte mantleMaterialId;
            public byte coreMaterialId;
            public byte coreDepositId;
            public byte coreOreGrade;
            public float oreGradeExponent;
            public uint volumeSeed;
            public byte historyTier;
            public float historyOverrideStride;
            public uint lastRecordedTick;
        }

        [Serializable]
        private sealed class AnomalyData
        {
            public string anomalyId;
            public string classification;
            public byte severity;
            public byte state;
            public float instability;
            public int sectorId;
            public Vector3 position;
        }

        private enum MissionTargetKind : byte
        {
            None = 0,
            Colony = 1,
            Asteroid = 2,
            Anomaly = 3,
            System = 4,
            Poi = 5,
            Unknown = 250
        }

        [Serializable]
        private sealed class MissionOfferData
        {
            public uint offerId;
            public byte type;
            public byte status;
            public ushort issuerFactionId;
            public byte targetKind;
            public string targetId;
            public ushort targetSystemId;
            public byte targetRingIndex;
            public byte targetPoiKind;
            public Vector3 targetPosition;
            public ushort resourceTypeIndex;
            public float units;
            public float rewardCredits;
            public float rewardStanding;
            public float rewardLp;
            public float risk;
            public byte priority;
            public uint createdTick;
            public uint expiryTick;
            public uint assignedTick;
            public uint completedTick;
        }

        [Serializable]
        private sealed class MissionAssignmentData
        {
            public uint offerId;
            public byte type;
            public byte status;
            public byte phase;
            public byte cargoState;
            public ushort issuerFactionId;
            public ushort resourceTypeIndex;
            public float units;
            public float cargoUnits;
            public float rewardCredits;
            public float rewardStanding;
            public float rewardLp;
            public uint startedTick;
            public uint dueTick;
            public uint completedTick;
            public byte autoComplete;
            public byte targetKind;
            public string targetId;
            public ushort targetSystemId;
            public byte targetRingIndex;
            public byte targetPoiKind;
            public Vector3 targetPosition;
            public byte sourceKind;
            public string sourceId;
            public ushort sourceSystemId;
            public byte sourceRingIndex;
            public byte sourcePoiKind;
            public Vector3 sourcePosition;
            public Vector3 destinationPosition;
            public string assignedEntityId;
            public byte assignedEntityKind;
        }

        [Serializable]
        private sealed class ContactData
        {
            public ushort contactFactionId;
            public float standing;
            public float loyaltyPoints;
            public byte tier;
        }

        [Serializable]
        private sealed class ReverseEngineeringStateData
        {
            public uint nextTaskId;
            public uint nextVariantId;

            public static ReverseEngineeringStateData From(ReverseEngineeringState state)
            {
                return new ReverseEngineeringStateData
                {
                    nextTaskId = state.NextTaskId,
                    nextVariantId = state.NextVariantId
                };
            }

            public ReverseEngineeringState ToComponent()
            {
                return new ReverseEngineeringState
                {
                    NextTaskId = nextTaskId,
                    NextVariantId = nextVariantId
                };
            }
        }

        [Serializable]
        private sealed class ReverseEngineeringEvidenceData
        {
            public ushort blueprintId;
            public byte stage;
            public byte fidelity;
            public byte integrity;
            public byte coverageEfficiency;
            public byte coverageReliability;
            public byte coverageMass;
            public byte coveragePower;
            public byte coverageSignature;
            public byte coverageDurability;
            public uint evidenceSeed;
            public uint sourceTick;

            public static ReverseEngineeringEvidenceData From(ReverseEngineeringEvidence evidence)
            {
                return new ReverseEngineeringEvidenceData
                {
                    blueprintId = evidence.BlueprintId,
                    stage = evidence.Stage,
                    fidelity = evidence.Fidelity,
                    integrity = evidence.Integrity,
                    coverageEfficiency = evidence.CoverageEfficiency,
                    coverageReliability = evidence.CoverageReliability,
                    coverageMass = evidence.CoverageMass,
                    coveragePower = evidence.CoveragePower,
                    coverageSignature = evidence.CoverageSignature,
                    coverageDurability = evidence.CoverageDurability,
                    evidenceSeed = evidence.EvidenceSeed,
                    sourceTick = evidence.SourceTick
                };
            }

            public ReverseEngineeringEvidence ToBuffer()
            {
                return new ReverseEngineeringEvidence
                {
                    BlueprintId = blueprintId,
                    Stage = stage,
                    Fidelity = fidelity,
                    Integrity = integrity,
                    CoverageEfficiency = coverageEfficiency,
                    CoverageReliability = coverageReliability,
                    CoverageMass = coverageMass,
                    CoveragePower = coveragePower,
                    CoverageSignature = coverageSignature,
                    CoverageDurability = coverageDurability,
                    EvidenceSeed = evidenceSeed,
                    SourceTick = sourceTick
                };
            }
        }

        [Serializable]
        private sealed class ReverseEngineeringTaskData
        {
            public uint taskId;
            public byte type;
            public ushort blueprintId;
            public byte evidenceNeeded;
            public uint evidenceHash;
            public float durationSeconds;
            public float progress;
            public uint attemptIndex;
            public uint teamHash;

            public static ReverseEngineeringTaskData From(ReverseEngineeringTask task)
            {
                return new ReverseEngineeringTaskData
                {
                    taskId = task.TaskId,
                    type = (byte)task.Type,
                    blueprintId = task.BlueprintId,
                    evidenceNeeded = task.EvidenceNeeded,
                    evidenceHash = task.EvidenceHash,
                    durationSeconds = task.DurationSeconds,
                    progress = task.Progress,
                    attemptIndex = task.AttemptIndex,
                    teamHash = task.TeamHash
                };
            }

            public ReverseEngineeringTask ToBuffer()
            {
                return new ReverseEngineeringTask
                {
                    TaskId = taskId,
                    Type = (ReverseEngineeringTaskType)type,
                    BlueprintId = blueprintId,
                    EvidenceNeeded = evidenceNeeded,
                    EvidenceHash = evidenceHash,
                    DurationSeconds = durationSeconds,
                    Progress = progress,
                    AttemptIndex = attemptIndex,
                    TeamHash = teamHash
                };
            }
        }

        [Serializable]
        private sealed class ReverseEngineeringVariantData
        {
            public uint variantId;
            public ushort blueprintId;
            public byte quality;
            public byte remainingRuns;
            public float efficiencyScalar;
            public float reliabilityScalar;
            public float massScalar;
            public float powerScalar;
            public float signatureScalar;
            public float durabilityScalar;
            public uint evidenceHash;
            public uint seed;

            public static ReverseEngineeringVariantData From(ReverseEngineeringBlueprintVariant variant)
            {
                return new ReverseEngineeringVariantData
                {
                    variantId = variant.VariantId,
                    blueprintId = variant.BlueprintId,
                    quality = variant.Quality,
                    remainingRuns = variant.RemainingRuns,
                    efficiencyScalar = variant.EfficiencyScalar,
                    reliabilityScalar = variant.ReliabilityScalar,
                    massScalar = variant.MassScalar,
                    powerScalar = variant.PowerScalar,
                    signatureScalar = variant.SignatureScalar,
                    durabilityScalar = variant.DurabilityScalar,
                    evidenceHash = variant.EvidenceHash,
                    seed = variant.Seed
                };
            }

            public ReverseEngineeringBlueprintVariant ToBuffer()
            {
                return new ReverseEngineeringBlueprintVariant
                {
                    VariantId = variantId,
                    BlueprintId = blueprintId,
                    Quality = quality,
                    RemainingRuns = remainingRuns,
                    EfficiencyScalar = efficiencyScalar,
                    ReliabilityScalar = reliabilityScalar,
                    MassScalar = massScalar,
                    PowerScalar = powerScalar,
                    SignatureScalar = signatureScalar,
                    DurabilityScalar = durabilityScalar,
                    EvidenceHash = evidenceHash,
                    Seed = seed
                };
            }
        }

        [Serializable]
        private sealed class ReverseEngineeringProgressData
        {
            public ushort blueprintId;
            public uint attemptCount;

            public static ReverseEngineeringProgressData From(ReverseEngineeringBlueprintProgress progress)
            {
                return new ReverseEngineeringProgressData
                {
                    blueprintId = progress.BlueprintId,
                    attemptCount = progress.AttemptCount
                };
            }

            public ReverseEngineeringBlueprintProgress ToBuffer()
            {
                return new ReverseEngineeringBlueprintProgress
                {
                    BlueprintId = blueprintId,
                    AttemptCount = attemptCount
                };
            }
        }

        private sealed class PendingFaction
        {
            public Entity entity;
            public FactionData data;
        }
    }
}
