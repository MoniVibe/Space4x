using System;
using System.Reflection;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Villager;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems.Telemetry
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(TelemetryExportSystem))]
    public partial struct SimPhaseTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricPrefixTickTotal = "simphase.tickTotalMs";
        private static readonly FixedString64Bytes MetricPhaseScenario = "simphase.phase.scenarioApply.ms";
        private static readonly FixedString64Bytes MetricPhaseMovement = "simphase.phase.movement.ms";
        private static readonly FixedString64Bytes MetricPhasePhysics = "simphase.phase.physics.ms";
        private static readonly FixedString64Bytes MetricPhaseSensors = "simphase.phase.sensors.ms";
        private static readonly FixedString64Bytes MetricPhaseComms = "simphase.phase.comms.ms";
        private static readonly FixedString64Bytes MetricPhaseKnowledge = "simphase.phase.knowledge.ms";
        private static readonly FixedString64Bytes MetricPhaseEconomy = "simphase.phase.economy.ms";
        private static readonly FixedString64Bytes MetricPhasePresentation = "simphase.phase.presentation.ms";
        private static readonly FixedString64Bytes MetricCommsMessages = "simphase.queue.commsMessages";
        private static readonly FixedString64Bytes MetricCommsInbox = "simphase.queue.commsInboxEntries";
        private static readonly FixedString64Bytes MetricCommsOutbox = "simphase.queue.commsOutboxEntries";
        private static readonly FixedString64Bytes MetricAckEvents = "simphase.queue.ackEvents";
        private static readonly FixedString64Bytes MetricDetectedEntities = "simphase.queue.detectedEntities";
        private static readonly FixedString64Bytes MetricInterruptsPending = "simphase.queue.interruptsPending";
        private static readonly FixedString64Bytes MetricCarriers = "simphase.entities.carriers";
        private static readonly FixedString64Bytes MetricVessels = "simphase.entities.vessels";
        private static readonly FixedString64Bytes MetricVillagers = "simphase.entities.villagers";
        private static readonly FixedString64Bytes MetricProjectiles = "simphase.entities.projectiles";
        private static readonly FixedString64Bytes MetricEntities = "simphase.entities.total";
        private static readonly FixedString64Bytes MetricChunks = "simphase.chunks.total";
        private static readonly FixedString64Bytes MetricArchetypes = "simphase.archetypes.total";
        private static readonly FixedString64Bytes MetricChunksPerArchetype = "simphase.chunks.perArchetype";
        private static readonly FixedString64Bytes MetricWorstTickKey = "simphase.regression.worstTick";
        private static readonly FixedString64Bytes MetricWorstTickDuration = "simphase.regression.worstTickDurationMs";
        private static readonly FixedString64Bytes MetricWorstTickPhase = "simphase.regression.worstTickPhase";
        private static readonly FixedString64Bytes MetricRenderDrawCommand = "simphase.render.drawCommands";
        private static readonly FixedString64Bytes MetricRenderInstances = "simphase.render.instances";
        private static readonly FixedString64Bytes MetricRenderInstancesPerCommand = "simphase.render.instancesPerCommand";

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimPhaseProfilerState>();
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryExportConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<TelemetryExportConfig>() ||
                !SystemAPI.HasSingleton<TelemetryStream>() ||
                !SystemAPI.TryGetSingletonEntity<SimPhaseProfilerState>(out var profilerEntity))
            {
                return;
            }

            var config = SystemAPI.GetSingleton<TelemetryExportConfig>();
            if (config.Enabled == 0 || (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            var metrics = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            var profiler = state.EntityManager.GetComponentData<SimPhaseProfilerState>(profilerEntity);
            var dominantPhase = profiler.GetDominantPhase();
            var tick = ResolveTick();
            if (profiler.TickTotalMs > 0f)
            {
                profiler.UpdateWorstRecords(tick, dominantPhase);
            }

            metrics.AddMetric(MetricPrefixTickTotal, profiler.TickTotalMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhaseScenario, profiler.ScenarioApplyMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhaseMovement, profiler.MovementMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhasePhysics, profiler.PhysicsMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhaseSensors, profiler.SensorsMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhaseComms, profiler.CommsMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhaseKnowledge, profiler.KnowledgeMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhaseEconomy, profiler.EconomyMs, TelemetryMetricUnit.DurationMilliseconds);
            metrics.AddMetric(MetricPhasePresentation, profiler.PresentationBridgeMs, TelemetryMetricUnit.DurationMilliseconds);

            var queueStats = CollectQueueStats(ref state);
            metrics.AddMetric(MetricCommsMessages, queueStats.CommsMessages);
            metrics.AddMetric(MetricCommsInbox, queueStats.CommsInboxEntries);
            metrics.AddMetric(MetricCommsOutbox, queueStats.CommsOutboxEntries);
            metrics.AddMetric(MetricAckEvents, queueStats.AckEvents);
            metrics.AddMetric(MetricDetectedEntities, queueStats.DetectedEntities);
            metrics.AddMetric(MetricInterruptsPending, queueStats.InterruptsPending);

            var shipCounts = CollectShipCounts(ref state);
            metrics.AddMetric(MetricCarriers, shipCounts.Carriers);
            metrics.AddMetric(MetricVessels, shipCounts.Vessels);
            metrics.AddMetric(MetricVillagers, shipCounts.Villagers);
            metrics.AddMetric(MetricProjectiles, shipCounts.Projectiles);

            var worldStats = CollectWorldStats(ref state);
            metrics.AddMetric(MetricEntities, worldStats.EntityCount);
            metrics.AddMetric(MetricChunks, worldStats.ChunkCount);
            metrics.AddMetric(MetricArchetypes, worldStats.ArchetypeCount);
            metrics.AddMetric(MetricChunksPerArchetype, (float)worldStats.ChunksPerArchetype, TelemetryMetricUnit.Ratio);

            EmitWorstTickMetrics(metrics, profiler);

#if UNITY_EDITOR
            EmitRenderingMetrics(metrics);
#endif

            state.EntityManager.SetComponentData(profilerEntity, profiler);
        }

        private static void EmitWorstTickMetrics(DynamicBuffer<TelemetryMetric> metrics, in SimPhaseProfilerState profiler)
        {
            EmitWorst(metrics, profiler.WorstTick0, 0);
            EmitWorst(metrics, profiler.WorstTick1, 1);
            EmitWorst(metrics, profiler.WorstTick2, 2);

            static void EmitWorst(DynamicBuffer<TelemetryMetric> buffer, in SimPhaseWorstTickRecord record, int rank)
            {
                if (record.Tick == 0 || record.DurationMs == 0f)
                {
                    return;
                }

                var tickKey = new FixedString64Bytes();
                tickKey.Append(MetricWorstTickKey);
                tickKey.Append('.');
                tickKey.Append(rank);
                buffer.AddMetric(tickKey, record.Tick);

                var durationKey = new FixedString64Bytes();
                durationKey.Append(MetricWorstTickDuration);
                durationKey.Append('.');
                durationKey.Append(rank);
                buffer.AddMetric(durationKey, record.DurationMs, TelemetryMetricUnit.DurationMilliseconds);

                var phaseKey = new FixedString64Bytes();
                phaseKey.Append(MetricWorstTickPhase);
                phaseKey.Append('.');
                phaseKey.Append(rank);
                buffer.AddMetric(phaseKey, record.DominantPhase);
            }
        }

        private QueueStats CollectQueueStats(ref SystemState state)
        {
            var stats = new QueueStats();

            if (SystemAPI.TryGetSingletonEntity<CommsMessageStreamTag>(out var stream))
            {
                var buffer = state.EntityManager.GetBuffer<CommsMessage>(stream);
                stats.CommsMessages = buffer.Length;
            }

            foreach (var inbox in SystemAPI.Query<DynamicBuffer<CommsInboxEntry>>())
            {
                stats.CommsInboxEntries += inbox.Length;
            }

            foreach (var outbox in SystemAPI.Query<DynamicBuffer<CommsOutboxEntry>>())
            {
                stats.CommsOutboxEntries += outbox.Length;
            }

            foreach (var ack in SystemAPI.Query<DynamicBuffer<AIAckEvent>>())
            {
                stats.AckEvents += ack.Length;
            }

            foreach (var detected in SystemAPI.Query<DynamicBuffer<DetectedEntity>>())
            {
                stats.DetectedEntities += detected.Length;
            }

            foreach (var interrupts in SystemAPI.Query<DynamicBuffer<Interrupt>>())
            {
                stats.InterruptsPending += interrupts.Length;
            }

            return stats;
        }

        private ShipCounts CollectShipCounts(ref SystemState state)
        {
            var counts = new ShipCounts();

            foreach (var ship in SystemAPI.Query<RefRO<ShipAggregate>>())
            {
                if (ship.ValueRO.Role == ShipRole.Carrier)
                {
                    counts.Carriers++;
                }
                else
                {
                    counts.Vessels++;
                }
            }

            foreach (var _ in SystemAPI.Query<RefRO<VillagerId>>())
            {
                counts.Villagers++;
            }

            var projectileQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProjectileActive>());
            counts.Projectiles = projectileQuery.CalculateEntityCount();

            return counts;
        }

        private static WorldStats CollectWorldStats(ref SystemState state)
        {
            var stats = new WorldStats();
            stats.EntityCount = state.EntityManager.UniversalQuery.CalculateEntityCount();
            stats.ChunkCount = state.EntityManager.UniversalQuery.CalculateChunkCountWithoutFiltering();

            var archetypes = new NativeList<EntityArchetype>(Allocator.Temp);
            state.EntityManager.GetAllArchetypes(archetypes);
            stats.ArchetypeCount = archetypes.Length;
            archetypes.Dispose();
            stats.ChunksPerArchetype = stats.ArchetypeCount > 0
                ? (double)stats.ChunkCount / stats.ArchetypeCount
                : 0d;
            return stats;
        }

        private uint ResolveTick()
        {
            if (SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick) && scenarioTick.Tick > 0)
            {
                return scenarioTick.Tick;
            }

            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickState))
            {
                var tick = tickState.Tick;
                if (SystemAPI.TryGetSingleton<TimeState>(out var timeState) && timeState.Tick > tick)
                {
                    tick = timeState.Tick;
                }

                return tick;
            }

            if (SystemAPI.TryGetSingleton<TimeState>(out var legacyTime))
            {
                return legacyTime.Tick;
            }

            return 0u;
        }

        private static void EmitRenderingMetrics(DynamicBuffer<TelemetryMetric> metrics)
        {
            var drawerType = Type.GetType("Unity.Rendering.EntitiesGraphicsStatsDrawer, Unity.Rendering");
            if (drawerType == null)
            {
                return;
            }

            var drawers = UnityEngine.Resources.FindObjectsOfTypeAll(drawerType);
            if (drawers == null || drawers.Length == 0)
            {
                return;
            }

            var totalDraws = 0f;
            var totalInstances = 0f;
            var totalPerCommand = 0f;
            var count = 0f;

            foreach (var drawer in drawers)
            {
                if (drawer == null)
                {
                    continue;
                }

                totalDraws += TryReadValue(drawer, drawerType, "drawCommandCount", "DrawCommandCount", "m_DrawCommandCount");
                totalInstances += TryReadValue(drawer, drawerType, "renderedInstanceCount", "RenderedInstanceCount", "m_RenderedInstanceCount");
                totalPerCommand += TryReadValue(drawer, drawerType, "instancesPerDrawCommand", "InstancesPerDrawCommand", "m_InstancesPerDrawCommand");
                count += 1f;
            }

            if (count == 0f)
            {
                return;
            }

            metrics.AddMetric(MetricRenderDrawCommand, totalDraws / count);
            metrics.AddMetric(MetricRenderInstances, totalInstances / count);
            metrics.AddMetric(MetricRenderInstancesPerCommand, totalPerCommand / count);
        }

        private static float TryReadValue(object target, Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && TryConvertNumeric(prop.GetValue(target), out var value))
                {
                    return value;
                }

                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && TryConvertNumeric(field.GetValue(target), out value))
                {
                    return value;
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (TryMatchName(field.Name, "draw", "render", "command", "instance") && TryConvertNumeric(field.GetValue(target), out var value))
                {
                    return value;
                }
            }

            return 0f;
        }

        private static bool TryMatchName(string fieldName, params string[] fragments)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return false;
            }

            foreach (var fragment in fragments)
            {
                if (!fieldName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryConvertNumeric(object value, out float result)
        {
            if (value is int i)
            {
                result = i;
                return true;
            }
            if (value is float f)
            {
                result = f;
                return true;
            }
            if (value is double d)
            {
                result = (float)d;
                return true;
            }
            result = 0f;
            return false;
        }

        private struct QueueStats
        {
            public int CommsMessages;
            public int CommsInboxEntries;
            public int CommsOutboxEntries;
            public int AckEvents;
            public int DetectedEntities;
            public int InterruptsPending;
        }

        private struct ShipCounts
        {
            public int Carriers;
            public int Vessels;
            public int Villagers;
            public int Projectiles;
        }

        private struct WorldStats
        {
            public int EntityCount;
            public int ChunkCount;
            public int ArchetypeCount;
            public double ChunksPerArchetype;
        }
    }
}
