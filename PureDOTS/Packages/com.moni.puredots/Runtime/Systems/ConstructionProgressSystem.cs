using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Villager;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Processes construction progress: handles material delivery and completion.
    /// </summary>
    [UpdateInGroup(typeof(ConstructionSystemGroup))]
    [UpdateAfter(typeof(ConstructionRegistrySystem))]
    public partial struct ConstructionProgressSystem : ISystem
    {
        private ComponentLookup<ResourceTypeIndex> _resourceCatalogLookup;
        private ComponentLookup<ConstructionSiteProgress> _progressLookup;
        private ComponentLookup<ConstructionSiteFlags> _flagsLookup;
        private ComponentLookup<ConstructionCompletionPrefab> _completionPrefabLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<ConstructionCostElement> _costBufferLookup;
        private BufferLookup<ConstructionDeliveredElement> _deliveredBufferLookup;
        private ComponentLookup<ConstructionSitePhaseSettings> _phaseSettingsLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ResourceTypeIndex>();

            _resourceCatalogLookup = state.GetComponentLookup<ResourceTypeIndex>(true);
            _progressLookup = state.GetComponentLookup<ConstructionSiteProgress>(false);
            _flagsLookup = state.GetComponentLookup<ConstructionSiteFlags>(false);
            _completionPrefabLookup = state.GetComponentLookup<ConstructionCompletionPrefab>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _costBufferLookup = state.GetBufferLookup<ConstructionCostElement>(true);
            _deliveredBufferLookup = state.GetBufferLookup<ConstructionDeliveredElement>(false);
            _phaseSettingsLookup = state.GetComponentLookup<ConstructionSitePhaseSettings>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var canEmitIncidents = SystemAPI.TryGetSingletonEntity<IncidentLearningEventBuffer>(out var incidentEntity);
            DynamicBuffer<IncidentLearningEvent> incidentEvents = default;
            if (canEmitIncidents)
            {
                incidentEvents = state.EntityManager.GetBuffer<IncidentLearningEvent>(incidentEntity);
            }

            _resourceCatalogLookup.Update(ref state);
            _progressLookup.Update(ref state);
            _flagsLookup.Update(ref state);
            _completionPrefabLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _costBufferLookup.Update(ref state);
            _deliveredBufferLookup.Update(ref state);
            _phaseSettingsLookup.Update(ref state);

            var catalog = SystemAPI.GetSingleton<ResourceTypeIndex>().Catalog;
            if (!catalog.IsCreated)
            {
                return;
            }

            // Process deposit commands (materials delivered to construction sites)
            foreach (var (deposits, siteEntity) in SystemAPI.Query<DynamicBuffer<ConstructionDepositCommand>>().WithEntityAccess())
            {
                if (!_progressLookup.HasComponent(siteEntity) ||
                    !_costBufferLookup.HasBuffer(siteEntity) ||
                    !_deliveredBufferLookup.HasBuffer(siteEntity))
                {
                    deposits.Clear();
                    continue;
                }

                var progress = _progressLookup[siteEntity];
                var costBuffer = _costBufferLookup[siteEntity];
                var deliveredBuffer = _deliveredBufferLookup[siteEntity];

                // Process each deposit command
                for (int i = deposits.Length - 1; i >= 0; i--)
                {
                    var deposit = deposits[i];
                    bool processed = false;

                    // Find matching cost element
                    for (int j = 0; j < costBuffer.Length; j++)
                    {
                        var cost = costBuffer[j];
                        if (cost.ResourceTypeId.Equals(deposit.ResourceTypeId))
                        {
                            // Find or create delivered element
                            int deliveredIndex = -1;
                            for (int k = 0; k < deliveredBuffer.Length; k++)
                            {
                                if (deliveredBuffer[k].ResourceTypeId.Equals(deposit.ResourceTypeId))
                                {
                                    deliveredIndex = k;
                                    break;
                                }
                            }

                            if (deliveredIndex < 0)
                            {
                                deliveredBuffer.Add(new ConstructionDeliveredElement
                                {
                                    ResourceTypeId = deposit.ResourceTypeId,
                                    UnitsDelivered = 0f
                                });
                                deliveredIndex = deliveredBuffer.Length - 1;
                            }

                            var delivered = deliveredBuffer[deliveredIndex];
                            var newDelivered = math.min(delivered.UnitsDelivered + deposit.Amount, cost.UnitsRequired);
                            delivered.UnitsDelivered = newDelivered;
                            deliveredBuffer[deliveredIndex] = delivered;

                            // Update progress based on material completion
                            float materialProgress = 0f;
                            float totalRequired = 0f;
                            float totalDelivered = 0f;

                            for (int k = 0; k < costBuffer.Length; k++)
                            {
                                totalRequired += costBuffer[k].UnitsRequired;
                                var delIdx = -1;
                                for (int l = 0; l < deliveredBuffer.Length; l++)
                                {
                                    if (deliveredBuffer[l].ResourceTypeId.Equals(costBuffer[k].ResourceTypeId))
                                    {
                                        delIdx = l;
                                        break;
                                    }
                                }
                                if (delIdx >= 0)
                                {
                                    totalDelivered += deliveredBuffer[delIdx].UnitsDelivered;
                                }
                            }

                            if (totalRequired > 0f)
                            {
                                materialProgress = totalDelivered / totalRequired;
                            }

                            // Progress is a combination of materials delivered and work done
                            // For now, materials contribute 50% of progress
                            var materialContribution = materialProgress * 0.5f;
                            var workContribution = math.clamp(progress.CurrentProgress / progress.RequiredProgress, 0f, 0.5f);
                            progress.CurrentProgress = (materialContribution + workContribution) * progress.RequiredProgress;
                            _progressLookup[siteEntity] = progress;

                            processed = true;
                            break;
                        }
                    }

                    if (processed)
                    {
                        deposits.RemoveAt(i);
                    }
                }
            }

            // Process progress commands (work done on construction sites)
            foreach (var (progressCommands, siteEntity) in SystemAPI.Query<DynamicBuffer<ConstructionProgressCommand>>().WithEntityAccess())
            {
                if (!_progressLookup.HasComponent(siteEntity))
                {
                    progressCommands.Clear();
                    continue;
                }

                var progress = _progressLookup[siteEntity];

                for (int i = progressCommands.Length - 1; i >= 0; i--)
                {
                    var cmd = progressCommands[i];
                    if (cmd.Delta < 0f && canEmitIncidents)
                    {
                        var builder = Entity.Null;
                        foreach (var (job, ticket, candidate) in SystemAPI
                                     .Query<RefRO<VillagerJob>, RefRO<VillagerJobTicket>>()
                                     .WithAll<IncidentLearningAgent>()
                                     .WithEntityAccess())
                        {
                            if (job.ValueRO.Type == VillagerJob.JobType.Builder &&
                                ticket.ValueRO.ResourceEntity == siteEntity)
                            {
                                builder = candidate;
                                break;
                            }
                        }

                        if (builder != Entity.Null)
                        {
                            var severity = ResolveIncidentSeverity(cmd.Delta, progress.RequiredProgress);
                            var category = severity >= 0.5f
                                ? IncidentLearningCategories.ConstructionCollapse
                                : IncidentLearningCategories.ConstructionIncident;
                            var position = _transformLookup.HasComponent(siteEntity)
                                ? _transformLookup[siteEntity].Position
                                : float3.zero;

                            incidentEvents.Add(new IncidentLearningEvent
                            {
                                Target = builder,
                                Source = siteEntity,
                                Position = position,
                                CategoryId = category,
                                Severity = severity,
                                Kind = IncidentLearningKind.Failure,
                                Tick = timeState.Tick
                            });
                        }
                    }

                    progress.CurrentProgress = math.min(
                        progress.CurrentProgress + cmd.Delta,
                        progress.RequiredProgress);
                    progressCommands.RemoveAt(i);
                }

                _progressLookup[siteEntity] = progress;

                var normalizedProgress = progress.RequiredProgress > 0f
                    ? math.saturate(progress.CurrentProgress / progress.RequiredProgress)
                    : 0f;

                if (_phaseSettingsLookup.HasComponent(siteEntity) && _flagsLookup.HasComponent(siteEntity))
                {
                    var settings = _phaseSettingsLookup[siteEntity];
                    if (settings.PartialUseThreshold01 > 0f)
                    {
                        var flags = _flagsLookup[siteEntity];
                        if (normalizedProgress >= settings.PartialUseThreshold01)
                        {
                            flags.Value |= ConstructionSiteFlags.PartiallyUsable;
                        }
                        else
                        {
                            flags.Value &= unchecked((byte)~ConstructionSiteFlags.PartiallyUsable);
                        }
                        _flagsLookup[siteEntity] = flags;
                    }
                }

                // Check for completion
                if (progress.CurrentProgress >= progress.RequiredProgress)
                {
                    // Mark as completed
                    if (_flagsLookup.HasComponent(siteEntity))
                    {
                        var flags = _flagsLookup[siteEntity];
                        flags.Value |= ConstructionSiteFlags.Completed;
                        _flagsLookup[siteEntity] = flags;
                    }

                    // Handle completion prefab spawn
                    if (_completionPrefabLookup.HasComponent(siteEntity))
                    {
                        var completionPrefab = _completionPrefabLookup[siteEntity];
                        if (completionPrefab.Prefab != Entity.Null)
                        {
                            // Spawn completion entity
                            var completedEntity = state.EntityManager.Instantiate(completionPrefab.Prefab);

                            // Copy transform from construction site
                            if (_transformLookup.HasComponent(siteEntity) &&
                                _transformLookup.HasComponent(completedEntity))
                            {
                                var siteTransform = _transformLookup[siteEntity];
                                state.EntityManager.SetComponentData(completedEntity, siteTransform);
                            }

                            // Destroy construction site if requested
                            if (completionPrefab.DestroySiteEntity)
                            {
                                state.EntityManager.DestroyEntity(siteEntity);
                            }
                        }
                    }
                }
            }

            // Process incident commands (construction mishaps, collapses, tool failures)
            foreach (var (incidentCommands, siteEntity) in SystemAPI.Query<DynamicBuffer<ConstructionIncidentCommand>>().WithEntityAccess())
            {
                if (incidentCommands.Length == 0)
                {
                    continue;
                }

                if (!canEmitIncidents)
                {
                    incidentCommands.Clear();
                    continue;
                }

                for (int i = incidentCommands.Length - 1; i >= 0; i--)
                {
                    var cmd = incidentCommands[i];
                    if (cmd.Target == Entity.Null || cmd.CategoryId.IsEmpty)
                    {
                        incidentCommands.RemoveAt(i);
                        continue;
                    }

                    var source = cmd.Source != Entity.Null ? cmd.Source : siteEntity;
                    var position = _transformLookup.HasComponent(source)
                        ? _transformLookup[source].Position
                        : (_transformLookup.HasComponent(siteEntity) ? _transformLookup[siteEntity].Position : float3.zero);

                    incidentEvents.Add(new IncidentLearningEvent
                    {
                        Target = cmd.Target,
                        Source = source,
                        Position = position,
                        CategoryId = cmd.CategoryId,
                        Severity = math.saturate(cmd.Severity),
                        Kind = cmd.Kind,
                        Tick = timeState.Tick
                    });

                    incidentCommands.RemoveAt(i);
                }
            }
        }

        private static float ResolveIncidentSeverity(float delta, float requiredProgress)
        {
            if (delta >= 0f)
            {
                return 0f;
            }

            var scale = math.max(requiredProgress, 1e-2f);
            return math.saturate(math.abs(delta) / scale);
        }
    }
}
