#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using PureDOTS.Rendering;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityTime = UnityEngine.Time;
using PureDOTSTimeState = PureDOTS.Runtime.Components.TimeState;
using PureDOTSTickTimeState = PureDOTS.Runtime.Components.TickTimeState;
using PureDOTSRewindState = PureDOTS.Runtime.Components.RewindState;

namespace Space4X.Diagnostics
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Logs render/presentation counts after presentation systems have run.
    /// Keeps running until MaterialMeshInfo appears or a timeout is hit.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(EntitiesGraphicsSystem))]
    public partial struct Space4XSmokePresentationCountsSystem : ISystem
    {
        private bool _loggedInitial;
        private bool _loggedFinal;
        private double _initialRealTimeSeconds;
        private EntityQuery _semanticQuery;
        private EntityQuery _variantQuery;
        private EntityQuery _meshPresenterQuery;
        private EntityQuery _materialMeshQuery;
        private EntityQuery _renderBoundsQuery;
        private EntityQuery _renderFilterQuery;
        private EntityQuery _materialMeshWithFilterQuery;
        private EntityQuery _missingRenderFilterQuery;
        private EntityQuery _localTransformQuery;
        private EntityQuery _localToWorldQuery;
        private EntityQuery _timeStateQuery;
        private EntityQuery _tickTimeStateQuery;
        private EntityQuery _rewindStateQuery;
        private EntityQuery _carrierQuery;
        private EntityQuery _miningVesselQuery;
        private EntityQuery _asteroidQuery;
        private EntityQuery _carrierSampleQuery;
        private EntityQuery _miningSampleQuery;
        private EntityQuery _asteroidSampleQuery;

        public void OnCreate(ref SystemState state)
        {
            _semanticQuery = state.GetEntityQuery(ComponentType.ReadOnly<RenderSemanticKey>());
            _variantQuery = state.GetEntityQuery(ComponentType.ReadOnly<RenderVariantKey>());
            _meshPresenterQuery = state.GetEntityQuery(ComponentType.ReadOnly<MeshPresenter>());
            _materialMeshQuery = state.GetEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>());
            _renderBoundsQuery = state.GetEntityQuery(ComponentType.ReadOnly<RenderBounds>());
            _renderFilterQuery = state.GetEntityQuery(ComponentType.ReadOnly<RenderFilterSettings>());
            _materialMeshWithFilterQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MaterialMeshInfo>(),
                    ComponentType.ReadOnly<RenderFilterSettings>()
                }
            });
            _missingRenderFilterQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MaterialMeshInfo>() },
                None = new[] { ComponentType.ReadOnly<RenderFilterSettings>() }
            });
            _localTransformQuery = state.GetEntityQuery(ComponentType.ReadOnly<LocalTransform>());
            _localToWorldQuery = state.GetEntityQuery(ComponentType.ReadOnly<LocalToWorld>());
            _timeStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<PureDOTSTimeState>());
            _tickTimeStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<PureDOTSTickTimeState>());
            _rewindStateQuery = state.GetEntityQuery(ComponentType.ReadOnly<PureDOTSRewindState>());
            _carrierQuery = state.GetEntityQuery(ComponentType.ReadOnly<Carrier>());
            _miningVesselQuery = state.GetEntityQuery(ComponentType.ReadOnly<MiningVessel>());
            _asteroidQuery = state.GetEntityQuery(ComponentType.ReadOnly<Asteroid>());
            _carrierSampleQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Carrier>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });
            _miningSampleQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MiningVessel>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });
            _asteroidSampleQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Asteroid>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<MaterialMeshInfo>()
                }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var worldName = state.WorldUnmanaged.Name.ToString();
            if (!string.Equals(worldName, "Game World", StringComparison.Ordinal))
            {
                return;
            }

            var semanticCount = _semanticQuery.CalculateEntityCount();
            var variantCount = _variantQuery.CalculateEntityCount();
            var meshPresenterCount = _meshPresenterQuery.CalculateEntityCount();
            var materialMeshCount = _materialMeshQuery.CalculateEntityCount();
            var renderBoundsCount = _renderBoundsQuery.CalculateEntityCount();
            var renderFilterCount = _renderFilterQuery.CalculateEntityCount();
            var materialMeshWithFilterCount = _materialMeshWithFilterQuery.CalculateEntityCount();
            var missingRenderFilterCount = _missingRenderFilterQuery.CalculateEntityCount();
            var localTransformCount = _localTransformQuery.CalculateEntityCount();
            var localToWorldCount = _localToWorldQuery.CalculateEntityCount();
            var carrierCount = _carrierQuery.CalculateEntityCount();
            var miningCount = _miningVesselQuery.CalculateEntityCount();
            var asteroidCount = _asteroidQuery.CalculateEntityCount();
            var sectionEntityAvailable = TryCountComponent(state.EntityManager, "Unity.Scenes.ResolvedSectionEntity, Unity.Scenes", out var sectionEntityCount);
            var fallbackCarrierCount = CountFallbackCarriers(_carrierQuery);
            var fallbackMinerCount = CountFallbackMiners(_miningVesselQuery);
            
            // Check for sim vs presentation mismatches
            if (fallbackCarrierCount > 0 || fallbackMinerCount > 0)
            {
                Debug.LogError($"[Space4XSmokePresentationCounts] PARITY VIOLATION: Fallback entities in presentation (Carriers={fallbackCarrierCount} Miners={fallbackMinerCount}). These should not exist - presentation must reflect headless progress only.");
            }

            var hasGameplayEntities = carrierCount > 0 || miningCount > 0 || asteroidCount > 0;
            if (hasGameplayEntities && !_loggedInitial)
            {
                _loggedInitial = true;
                _initialRealTimeSeconds = UnityTime.realtimeSinceStartupAsDouble;

                var timeInfo = BuildTimeInfo(ref state);
                Debug.Log(
                    $"[Space4XSmokePresentationCounts] Phase=Initial World='{worldName}' RenderSemanticKey={semanticCount} RenderVariantKey={variantCount} MeshPresenter={meshPresenterCount} MaterialMeshInfo={materialMeshCount} RenderBounds={renderBoundsCount} RenderFilterSettings={renderFilterCount} MaterialMeshInfoWithRenderFilterSettings={materialMeshWithFilterCount} MaterialMeshInfoMissingRenderFilterSettings={missingRenderFilterCount} LocalTransform={localTransformCount} LocalToWorld={localToWorldCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")} FallbackCarrier={fallbackCarrierCount} FallbackMiningVessel={fallbackMinerCount}{timeInfo}");
                LogSample(ref state, _carrierSampleQuery, "Carrier", timeInfo);
                LogSample(ref state, _miningSampleQuery, "MiningVessel", timeInfo);
                LogSample(ref state, _asteroidSampleQuery, "Asteroid", timeInfo);
            }

            if (_loggedInitial && !_loggedFinal)
            {
                var elapsedRealSeconds = UnityTime.realtimeSinceStartupAsDouble;
                if (elapsedRealSeconds >= _initialRealTimeSeconds + 5.0)
                {
                    _loggedFinal = true;
                    state.Enabled = false;
                    var timeInfo = BuildTimeInfo(ref state);
                    Debug.Log(
                        $"[Space4XSmokePresentationCounts] Phase=Final World='{worldName}' RenderSemanticKey={semanticCount} RenderVariantKey={variantCount} MeshPresenter={meshPresenterCount} MaterialMeshInfo={materialMeshCount} RenderBounds={renderBoundsCount} RenderFilterSettings={renderFilterCount} MaterialMeshInfoWithRenderFilterSettings={materialMeshWithFilterCount} MaterialMeshInfoMissingRenderFilterSettings={missingRenderFilterCount} LocalTransform={localTransformCount} LocalToWorld={localToWorldCount} Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount} ResolvedSectionEntity={(sectionEntityAvailable ? sectionEntityCount.ToString() : "unavailable")} FallbackCarrier={fallbackCarrierCount} FallbackMiningVessel={fallbackMinerCount}{timeInfo}");
                    LogSample(ref state, _carrierSampleQuery, "Carrier", timeInfo);
                    LogSample(ref state, _miningSampleQuery, "MiningVessel", timeInfo);
                    LogSample(ref state, _asteroidSampleQuery, "Asteroid", timeInfo);
                }
            }
        }

        private string BuildTimeInfo(ref SystemState state)
        {
            var timeStateCount = _timeStateQuery.CalculateEntityCount();
            var tickTimeStateCount = _tickTimeStateQuery.CalculateEntityCount();
            var rewindStateCount = _rewindStateQuery.CalculateEntityCount();

            var info = $" TimeStateCount={timeStateCount} TickTimeStateCount={tickTimeStateCount} RewindStateCount={rewindStateCount}";

            if (timeStateCount == 1 && _timeStateQuery.TryGetSingleton(out PureDOTSTimeState timeState))
            {
                info += $" TimeTick={timeState.Tick} TimeWorldSeconds={timeState.WorldSeconds} TimePaused={timeState.IsPaused}";
            }
            else
            {
                info += timeStateCount == 0 ? " TimeState=missing" : " TimeState=non-singleton";
            }

            if (tickTimeStateCount == 1 && _tickTimeStateQuery.TryGetSingleton(out PureDOTSTickTimeState tickTime))
            {
                info += $" Tick={tickTime.Tick} TargetTick={tickTime.TargetTick} WorldSeconds={tickTime.WorldSeconds} IsPlaying={tickTime.IsPlaying} IsPaused={tickTime.IsPaused} Speed={tickTime.CurrentSpeedMultiplier}";
            }
            else
            {
                info += tickTimeStateCount == 0 ? " TickTimeState=missing" : " TickTimeState=non-singleton";
            }

            if (rewindStateCount == 1 && _rewindStateQuery.TryGetSingleton(out PureDOTSRewindState rewind))
            {
                info += $" RewindMode={rewind.Mode}";
            }
            else
            {
                info += rewindStateCount == 0 ? " RewindState=missing" : " RewindState=non-singleton";
            }

            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            if (simulationGroup != null)
            {
                info += $" SimulationEnabled={simulationGroup.Enabled}";
            }

            var fixedGroup = state.World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedGroup != null)
            {
                info += $" FixedStepEnabled={fixedGroup.Enabled}";
            }

            return info;
        }

        private static int CountFallbackCarriers(EntityQuery query)
        {
            using var carriers = query.ToComponentDataArray<Carrier>(Allocator.Temp);
            var fallbackId = new FixedString64Bytes("FALLBACK-CARRIER");
            var count = 0;
            for (int i = 0; i < carriers.Length; i++)
            {
                if (carriers[i].CarrierId.Equals(fallbackId))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountFallbackMiners(EntityQuery query)
        {
            using var miners = query.ToComponentDataArray<MiningVessel>(Allocator.Temp);
            var fallbackId = new FixedString64Bytes("FALLBACK-MINER");
            var count = 0;
            for (int i = 0; i < miners.Length; i++)
            {
                if (miners[i].VesselId.Equals(fallbackId))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryCountComponent(EntityManager em, string assemblyQualifiedName, out int count)
        {
            var componentType = Type.GetType(assemblyQualifiedName);
            if (componentType == null)
            {
                count = 0;
                return false;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly(componentType));
            count = query.CalculateEntityCount();
            return true;
        }

        private static void LogSample(ref SystemState state, EntityQuery query, string label, string timeInfo)
        {
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0)
            {
                Debug.Log($"[Space4XSmokePresentationCounts] Sample='{label}' Entity=none");
                return;
            }

            var entity = entities[0];
            var em = state.EntityManager;
            var transform = em.GetComponentData<LocalTransform>(entity);
            var semanticKey = em.HasComponent<RenderSemanticKey>(entity)
                ? em.GetComponentData<RenderSemanticKey>(entity).Value
                : (ushort)0;
            var variantKey = em.HasComponent<RenderVariantKey>(entity)
                ? em.GetComponentData<RenderVariantKey>(entity).Value
                : -1;
            var hasLocalToWorld = em.HasComponent<LocalToWorld>(entity);
            var localToWorldPos = hasLocalToWorld
                ? em.GetComponentData<LocalToWorld>(entity).Position
                : default;
            var hasMeshPresenter = em.HasComponent<MeshPresenter>(entity);
            var meshPresenter = hasMeshPresenter
                ? em.GetComponentData<MeshPresenter>(entity)
                : default;
            var meshPresenterEnabled = hasMeshPresenter && em.IsComponentEnabled<MeshPresenter>(entity);
            var materialMesh = em.GetComponentData<MaterialMeshInfo>(entity);
            var materialIndex = materialMesh.Material < 0
                ? MaterialMeshInfo.StaticIndexToArrayIndex(materialMesh.Material)
                : materialMesh.Material;
            var meshIndex = materialMesh.Mesh < 0
                ? MaterialMeshInfo.StaticIndexToArrayIndex(materialMesh.Mesh)
                : materialMesh.Mesh;
            var materialMeshIndexRange = materialMesh.HasMaterialMeshIndexRange
                ? materialMesh.MaterialMeshIndexRange.ToString()
                : "n/a";

            var tint = em.HasComponent<RenderTint>(entity)
                ? em.GetComponentData<RenderTint>(entity).Value
                : default;
            var hasTint = em.HasComponent<RenderTint>(entity);

            Debug.Log(
                $"[Space4XSmokePresentationCounts] Sample='{label}' Entity={entity} Pos={transform.Position} Scale={transform.Scale} LocalToWorld={(hasLocalToWorld ? localToWorldPos.ToString() : "missing")} RenderSemanticKey={semanticKey} RenderVariantKey={variantKey} MeshPresenterDefIndex={(hasMeshPresenter ? meshPresenter.DefIndex.ToString() : "none")} MeshPresenterEnabled={(hasMeshPresenter ? meshPresenterEnabled.ToString() : "n/a")} Material={materialMesh.Material} Mesh={materialMesh.Mesh} SubMesh={materialMesh.SubMesh} HasMaterialMeshIndexRange={materialMesh.HasMaterialMeshIndexRange} MaterialMeshIndexRange={materialMeshIndexRange} MaterialIndex={materialIndex} MeshIndex={meshIndex} RenderTint={(hasTint ? tint.ToString() : "none")}{timeInfo}");
        }
    }
}
#endif
