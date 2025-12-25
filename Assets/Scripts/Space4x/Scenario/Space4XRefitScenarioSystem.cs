using System;
using System.Collections.Generic;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4x.Scenario
{
    /// <summary>
    /// Loads and executes the refit scenario from JSON, spawning hulls/modules and processing timed actions.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ModuleCatalogBootstrapSystem))]
    public partial class Space4XRefitScenarioSystem : SystemBase
    {
        private bool _hasLoaded;
        private RefitScenarioJson _scenarioData;
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
            if (!scenarioIdString.Contains("refit", StringComparison.OrdinalIgnoreCase))
            {
                Enabled = false;
                return;
            }

            var scenarioPath = FindScenarioPath(scenarioIdString);
            if (string.IsNullOrEmpty(scenarioPath) || !File.Exists(scenarioPath))
            {
                Debug.LogWarning($"[Space4XRefitScenario] Scenario file not found: {scenarioPath}");
                Enabled = false;
                return;
            }

            var jsonText = File.ReadAllText(scenarioPath);
            _scenarioData = JsonUtility.FromJson<RefitScenarioJson>(jsonText);
            if (_scenarioData == null || _scenarioData.spawn == null)
            {
                Debug.LogError("[Space4XRefitScenario] Failed to parse scenario JSON");
                Enabled = false;
                return;
            }

            _spawnedEntities = new Dictionary<string, Entity>();
            SpawnEntities();
            ScheduleActions();

            _hasLoaded = true;
            Enabled = false;
        }

        private string FindScenarioPath(string scenarioId)
        {
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
                if (spawn.kind == "Hull")
                {
                    SpawnHull(spawn);
                }
                else if (spawn.kind == "Station")
                {
                    SpawnStation(spawn);
                }
            }
        }

        private void SpawnHull(SpawnDefinition spawn)
        {
            var position = spawn.position != null && spawn.position.Length >= 2
                ? new float3(spawn.position[0], spawn.position.Length > 2 ? spawn.position[2] : 0f, spawn.position[1])
                : float3.zero;

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);

            var hullId = new FixedString64Bytes(spawn.hullId ?? "lcv-sparrow");
            EntityManager.AddComponentData(entity, new CarrierHullId { HullId = hullId });
            EntityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes($"ship-{_spawnedEntities.Count}"),
                AffiliationEntity = Entity.Null,
                Speed = 3f,
                PatrolCenter = position,
                PatrolRadius = 50f
            });

            EntityManager.AddComponentData(entity, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero,
                WaitTime = 0f,
                WaitTimer = 0f
            });

            EntityManager.AddComponentData(entity, new MovementCommand
            {
                TargetPosition = position,
                ArrivalThreshold = 1f
            });

            EntityManager.AddComponentData(entity, new ModuleRefitFacility
            {
                RefitRatePerSecond = 1f,
                SupportsFieldRefit = 1
            });

            EntityManager.AddComponentData(entity, new FieldRepairCapability
            {
                RepairRatePerSecond = 10f,
                CriticalRepairRate = 5f,
                CanRepairCritical = 1
            });

            var slotsBuffer = EntityManager.AddBuffer<CarrierModuleSlot>(entity);
            InitializeSlotsFromHull(hullId, slotsBuffer);

            if (spawn.loadout != null)
            {
                InstallLoadout(entity, slotsBuffer, spawn.loadout);
            }

            var entityKey = $"ship[{_spawnedEntities.Count}]";
            _spawnedEntities[entityKey] = entity;
        }

        private void InitializeSlotsFromHull(FixedString64Bytes hullId, DynamicBuffer<CarrierModuleSlot> slots)
        {
            if (!ModuleCatalogUtility.TryGetHullSpec(EntityManager, hullId, out var catalogRef, out var hullIndex))
            {
                Debug.LogWarning($"[Space4XRefitScenario] Hull spec not found: {hullId}");
                return;
            }

            ref var hullSpec = ref catalogRef.Value.Hulls[hullIndex];
            ref var slotArray = ref hullSpec.Slots;
            for (int i = 0; i < slotArray.Length; i++)
            {
                var slot = slotArray[i];
                slots.Add(new CarrierModuleSlot
                {
                    SlotIndex = i,
                    SlotSize = ConvertMountSize(slot.Size),
                    CurrentModule = Entity.Null,
                    TargetModule = Entity.Null,
                    RefitProgress = 0f,
                    State = ModuleSlotState.Empty
                });
            }
        }

        private ModuleSlotSize ConvertMountSize(MountSize size)
        {
            return size switch
            {
                MountSize.S => ModuleSlotSize.Small,
                MountSize.M => ModuleSlotSize.Medium,
                MountSize.L => ModuleSlotSize.Large,
                _ => ModuleSlotSize.Medium
            };
        }

        private void InstallLoadout(Entity carrier, DynamicBuffer<CarrierModuleSlot> slots, List<LoadoutEntry> loadout)
        {
            foreach (var entry in loadout)
            {
                if (entry.slotIndex < 0 || entry.slotIndex >= slots.Length)
                {
                    continue;
                }

                var moduleEntity = CreateModuleEntity(entry.moduleId);
                if (moduleEntity == Entity.Null)
                {
                    continue;
                }

                var slot = slots[entry.slotIndex];
                slot.CurrentModule = moduleEntity;
                slot.State = ModuleSlotState.Active;
                slots[entry.slotIndex] = slot;
            }
        }

        private Entity CreateModuleEntity(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId))
            {
                return Entity.Null;
            }

            var moduleIdFixed = new FixedString64Bytes(moduleId);
            if (!ModuleCatalogUtility.TryGetModuleSpec(EntityManager, moduleIdFixed, out var spec))
            {
                Debug.LogWarning($"[Space4XRefitScenario] Module spec not found: {moduleId}");
                return Entity.Null;
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new ModuleTypeId { Value = moduleIdFixed });
            EntityManager.AddComponentData(entity, new ModuleSlotRequirement
            {
                SlotSize = ConvertMountSize(spec.RequiredSize)
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
            EntityManager.AddComponentData(entity, new ModuleHealth
            {
                MaxHealth = maxHealth,
                CurrentHealth = maxHealth * spec.DefaultEfficiency,
                MaxFieldRepairHealth = maxHealth * 0.8f,
                DegradationPerSecond = 0f,
                RepairPriority = 128,
                Failed = 0
            });

            return entity;
        }

        private void SpawnStation(SpawnDefinition spawn)
        {
            var position = spawn.position != null && spawn.position.Length >= 2
                ? new float3(spawn.position[0], spawn.position.Length > 2 ? spawn.position[2] : 0f, spawn.position[1])
                : float3.zero;

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);

            if (spawn.components != null)
            {
                if (spawn.components.RefitFacilityTag)
                {
                    EntityManager.AddComponent<RefitFacilityTag>(entity);
                }

                if (spawn.components.FacilityZone != null)
                {
                    EntityManager.AddComponentData(entity, new FacilityZone
                    {
                        RadiusMeters = spawn.components.FacilityZone.radiusMeters
                    });
                }
            }

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void ScheduleActions()
        {
            if (_scenarioData.actions == null)
            {
                return;
            }

            var actionEntity = EntityManager.CreateEntity();
            var actionsBuffer = EntityManager.AddBuffer<ScenarioActionEntry>(actionEntity);
            
            foreach (var action in _scenarioData.actions)
            {
                var targetStr = action.target;
                if (action.targets != null && action.targets.Length > 0)
                {
                    targetStr = string.Join(",", action.targets);
                }

                actionsBuffer.Add(new ScenarioActionEntry
                {
                    TimeSeconds = action.time_s,
                    ActionType = new FixedString64Bytes(action.type ?? string.Empty),
                    Target = new FixedString128Bytes(targetStr ?? string.Empty),
                    TargetEntityId = new FixedString64Bytes(action.targetEntity ?? string.Empty),
                    FloatValue = action.to,
                    Mode = new FixedString64Bytes(action.mode ?? string.Empty),
                    SlotIndex = action.swap != null ? action.swap.slotIndex : -1,
                    NewModuleId = new FixedString64Bytes(action.swap != null ? (action.swap.newModuleId ?? string.Empty) : string.Empty)
                });
            }

            EntityManager.AddComponentData(actionEntity, new ScenarioActionScheduler
            {
                LastProcessedTime = -1f
            });
        }
    }

    public struct ScenarioActionEntry : IBufferElementData
    {
        public float TimeSeconds;
        public FixedString64Bytes ActionType;
        public FixedString128Bytes Target;
        public FixedString64Bytes TargetEntityId;
        public float FloatValue;
        public FixedString64Bytes Mode;
        public int SlotIndex;
        public FixedString64Bytes NewModuleId;
    }

    public struct ScenarioActionScheduler : IComponentData
    {
        public float LastProcessedTime;
    }
}

