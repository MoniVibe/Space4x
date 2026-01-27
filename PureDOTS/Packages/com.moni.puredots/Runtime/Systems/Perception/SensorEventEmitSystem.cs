using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Emits sensor events per cell during detection phase.
    /// Runs alongside PerceptionUpdateSystem but writes lightweight events instead of updating every agent.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup))]
    [UpdateAfter(typeof(SensorCellColoringSystem))]
    public partial struct SensorEventEmitSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SensorSignature> _signatureLookup;
        private ComponentLookup<SensorCellIndex> _cellIndexLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<Detectable> _detectableLookup;
        private ComponentLookup<FactionId> _factionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
            state.RequireForUpdate<SimulationFeatureFlags>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _signatureLookup = state.GetComponentLookup<SensorSignature>(true);
            _cellIndexLookup = state.GetComponentLookup<SensorCellIndex>(true);
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);
            _detectableLookup = state.GetComponentLookup<Detectable>(true);
            _factionLookup = state.GetComponentLookup<FactionId>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.SensorCommsScalingPrototype) == 0)
            {
                return;
            }

            if (!SystemAPI.HasSingleton<SpatialGridConfig>() || !SystemAPI.HasSingleton<SpatialGridState>())
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var gridState = SystemAPI.GetSingleton<SpatialGridState>();
            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();

            // Get current color being processed
            if (!SystemAPI.HasComponent<SensorCellColoringState>(gridEntity))
            {
                return;
            }

            var coloringState = SystemAPI.GetComponentRO<SensorCellColoringState>(gridEntity).ValueRO;
            var currentColor = coloringState.CurrentColor;

            // Ensure event buffer exists on grid entity
            DynamicBuffer<SensorCellEvent> eventsBuffer;
            if (!SystemAPI.HasBuffer<SensorCellEvent>(gridEntity))
            {
                state.EntityManager.AddBuffer<SensorCellEvent>(gridEntity);
            }
            eventsBuffer = SystemAPI.GetBuffer<SensorCellEvent>(gridEntity);
            eventsBuffer.Clear();

            _transformLookup.Update(ref state);
            _signatureLookup.Update(ref state);
            _cellIndexLookup.Update(ref state);
            _residencyLookup.Update(ref state);
            _detectableLookup.Update(ref state);
            _factionLookup.Update(ref state);

            // Get spatial grid data
            if (!SystemAPI.HasBuffer<SpatialGridCellRange>(gridEntity) || !SystemAPI.HasBuffer<SpatialGridEntry>(gridEntity))
            {
                return;
            }

            var gridRanges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
            var gridEntries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);

            // Collect events in a native list first, then add to buffer
            var eventList = new NativeList<SensorCellEvent>(1024, Allocator.Temp);
            var job = new EmitSensorEventsJob
            {
                GridConfig = gridConfig,
                GridRanges = gridRanges,
                GridEntries = gridEntries,
                CurrentColor = currentColor,
                CurrentTick = timeState.Tick,
                TransformLookup = _transformLookup,
                SignatureLookup = _signatureLookup,
                CellIndexLookup = _cellIndexLookup,
                ResidencyLookup = _residencyLookup,
                DetectableLookup = _detectableLookup,
                FactionLookup = _factionLookup,
                Events = eventList
            };

            state.Dependency = job.Schedule(state.Dependency);
            state.Dependency.Complete();

            // Copy events to buffer
            eventsBuffer.ResizeUninitialized(eventList.Length);
            for (int i = 0; i < eventList.Length; i++)
            {
                eventsBuffer[i] = eventList[i];
            }

            eventList.Dispose();
        }

        [BurstCompile]
        private struct EmitSensorEventsJob : IJob
        {
            public SpatialGridConfig GridConfig;
            [ReadOnly] public DynamicBuffer<SpatialGridCellRange> GridRanges;
            [ReadOnly] public DynamicBuffer<SpatialGridEntry> GridEntries;
            public byte CurrentColor;
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<SensorSignature> SignatureLookup;
            [ReadOnly] public ComponentLookup<SensorCellIndex> CellIndexLookup;
            [ReadOnly] public ComponentLookup<SpatialGridResidency> ResidencyLookup;
            [ReadOnly] public ComponentLookup<Detectable> DetectableLookup;
            [ReadOnly] public ComponentLookup<FactionId> FactionLookup;
            public NativeList<SensorCellEvent> Events;

            public void Execute()
            {

                // Iterate through spatial grid entries to find sensors
                for (int cellIdx = 0; cellIdx < GridRanges.Length; cellIdx++)
                {
                    var cellRange = GridRanges[cellIdx];
                    if (cellRange.Count == 0)
                    {
                        continue;
                    }

                    // Check if this cell matches current color
                    SpatialHash.Unflatten(cellIdx, GridConfig, out var cellCoords);
                    var mortonKey = SpatialHash.MortonKey(cellCoords, GridConfig.HashSeed);
                    var cellColor = (byte)(mortonKey & 0x3);
                    if (cellColor != CurrentColor)
                    {
                        continue;
                    }

                    // Process entities in this cell
                    for (int entryIdx = cellRange.StartIndex; entryIdx < cellRange.StartIndex + cellRange.Count; entryIdx++)
                    {
                        var entry = GridEntries[entryIdx];
                        var sensorEntity = entry.Entity;

                        // Check if entity has sensor capability
                        if (!CellIndexLookup.HasComponent(sensorEntity))
                        {
                            continue;
                        }

                        var cellIndex = CellIndexLookup[sensorEntity];
                        if (cellIndex.Color != CurrentColor)
                        {
                            continue;
                        }

                        if (!TransformLookup.HasComponent(sensorEntity))
                        {
                            continue;
                        }

                        var sensorTransform = TransformLookup[sensorEntity];
                        var sensorPos = sensorTransform.Position;

                        // Find nearby entities using spatial query
                        var candidates = new NativeList<Entity>(64, Allocator.Temp);
                        SpatialQueryHelper.GetEntitiesWithinRadius(
                            ref sensorPos,
                            100f, // TODO: Get from SenseCapability
                            GridConfig,
                            GridRanges,
                            GridEntries,
                            ref candidates);

                        // Emit events for detected entities
                        for (int i = 0; i < candidates.Length; i++)
                        {
                            var targetEntity = candidates[i];
                            if (targetEntity == sensorEntity)
                            {
                                continue;
                            }

                            if (!TransformLookup.HasComponent(targetEntity))
                            {
                                continue;
                            }

                            var targetTransform = TransformLookup[targetEntity];
                            var targetPos = targetTransform.Position;
                            var distance = math.distance(sensorPos, targetPos);

                            // Get target signature and threat
                            var threatLevel = (byte)0;
                            if (DetectableLookup.HasComponent(targetEntity))
                            {
                                threatLevel = DetectableLookup[targetEntity].ThreatLevel;
                            }

                            var signature = SignatureLookup.HasComponent(targetEntity)
                                ? SignatureLookup[targetEntity]
                                : SensorSignature.Default;

                            // Determine detected channels (simplified - use EM as default)
                            var detectedChannels = PerceptionChannel.EM;
                            if (signature.VisualSignature > 0.1f)
                            {
                                detectedChannels |= PerceptionChannel.Vision;
                            }

                            // Get faction ID
                            var factionId = 0;
                            if (FactionLookup.HasComponent(targetEntity))
                            {
                                factionId = FactionLookup[targetEntity].Value;
                            }

                            // Get target cell ID
                            var targetCellId = cellIdx;
                            if (ResidencyLookup.HasComponent(targetEntity))
                            {
                                targetCellId = ResidencyLookup[targetEntity].CellId;
                            }

                            Events.Add(new SensorCellEvent
                            {
                                CellId = targetCellId,
                                DetectedEntity = targetEntity,
                                SensorEntity = sensorEntity,
                                DetectedChannels = detectedChannels,
                                Confidence = 1f, // TODO: Calculate from signature/acuity
                                Distance = distance,
                                ThreatLevel = threatLevel,
                                FactionId = factionId,
                                EmittedTick = CurrentTick,
                                ChangeVersion = CurrentTick
                            });
                        }

                        candidates.Dispose();
                    }
                }
            }
        }
    }
}
