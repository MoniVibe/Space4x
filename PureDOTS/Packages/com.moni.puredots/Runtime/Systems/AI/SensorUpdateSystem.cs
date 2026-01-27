using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Deception;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// Updates sensor data for all entities with SensorConfig.
    /// Populates DetectedEntity buffer based on sensor range and capabilities.
    /// Game-agnostic: works for any entity type with sensors.
    /// </summary>
    /// <remarks>
    /// Legacy N² sensor pipeline. Prefer PerceptionUpdateSystem → AISensorUpdateSystem for scalable sims and
    /// only enable this system in small demo scenes.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SensorUpdateSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Detectable> _detectableLookup;
        private BufferLookup<EntityRelation> _relationLookup;
        private ComponentLookup<FactionId> _factionLookup;
        private ComponentLookup<DisguiseIdentity> _disguiseLookup;
        private BufferLookup<DisguiseDiscovery> _discoveryLookup;
        private ComponentLookup<VillagerId> _villagerLookup;
        private ComponentLookup<VillageId> _villageLookup;
        private ComponentLookup<BandId> _bandLookup;
        private ComponentLookup<ArmyId> _armyLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SimulationFeatureFlags>();
            
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _detectableLookup = state.GetComponentLookup<Detectable>(true);
            _relationLookup = state.GetBufferLookup<EntityRelation>(true);
            _factionLookup = state.GetComponentLookup<FactionId>(true);
            _disguiseLookup = state.GetComponentLookup<DisguiseIdentity>(true);
            _discoveryLookup = state.GetBufferLookup<DisguiseDiscovery>(true);
            _villagerLookup = state.GetComponentLookup<VillagerId>(true);
            _villageLookup = state.GetComponentLookup<VillageId>(true);
            _bandLookup = state.GetComponentLookup<BandId>(true);
            _armyLookup = state.GetComponentLookup<ArmyId>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.LegacySensorSystemEnabled) == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _detectableLookup.Update(ref state);
            _relationLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _disguiseLookup.Update(ref state);
            _discoveryLookup.Update(ref state);
            _villagerLookup.Update(ref state);
            _villageLookup.Update(ref state);
            _bandLookup.Update(ref state);
            _armyLookup.Update(ref state);

            var relationshipCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<FactionRelationships>>())
            {
                relationshipCount++;
            }

            var factionRelationships = new NativeList<FactionRelationships>(relationshipCount, Allocator.TempJob);
            foreach (var relationship in SystemAPI.Query<RefRO<FactionRelationships>>())
            {
                factionRelationships.Add(relationship.ValueRO);
            }

            // Collect all detectable entities
            var detectableCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<Detectable>>())
            {
                detectableCount++;
            }

            var detectables = new NativeList<DetectableData>(detectableCount, Allocator.TempJob);
            
            foreach (var (detectable, transform, entity) in SystemAPI.Query<RefRO<Detectable>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                detectables.Add(new DetectableData
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    Forward = math.forward(transform.ValueRO.Rotation),
                    Visibility = detectable.ValueRO.Visibility,
                    Audibility = detectable.ValueRO.Audibility,
                    ThreatLevel = detectable.ValueRO.ThreatLevel,
                    Category = detectable.ValueRO.Category
                });
            }

            // Update each sensor
            foreach (var (config, sensorState, transform, detectedBuffer, entity) in 
                SystemAPI.Query<RefRO<SensorConfig>, RefRW<SensorState>, RefRO<LocalTransform>, DynamicBuffer<DetectedEntity>>()
                .WithEntityAccess())
            {
                // Check update interval
                var ticksSinceUpdate = timeState.Tick - sensorState.ValueRO.LastUpdateTick;
                var secondsSinceUpdate = ticksSinceUpdate * timeState.FixedDeltaTime;
                
                if (secondsSinceUpdate < config.ValueRO.UpdateInterval)
                {
                    continue;
                }

                // Clear old detections
                detectedBuffer.Clear();

                var sensorPos = transform.ValueRO.Position;
                var sensorForward = math.forward(transform.ValueRO.Rotation);
                var rangeSq = config.ValueRO.Range * config.ValueRO.Range;
                var fovCos = math.cos(math.radians(config.ValueRO.FieldOfView * 0.5f));

                var highestThreat = (byte)0;
                var highestThreatEntity = Entity.Null;
                var nearestEntity = Entity.Null;
                var nearestDistSq = float.MaxValue;
                var detectionCount = 0;

                for (int i = 0; i < detectables.Length && detectionCount < config.ValueRO.MaxTrackedTargets; i++)
                {
                    var target = detectables[i];
                    
                    // Skip self
                    if (target.Entity == entity)
                    {
                        continue;
                    }

                    var toTarget = target.Position - sensorPos;
                    var distSq = math.lengthsq(toTarget);
                    
                    // Range check
                    if (distSq > rangeSq)
                    {
                        continue;
                    }

                    var distance = math.sqrt(distSq);
                    var direction = distance > 0.001f ? toTarget / distance : float3.zero;

                    // FOV check (for sight-based detection)
                    var inFov = true;
                    if (config.ValueRO.FieldOfView < 360f)
                    {
                        var dot = math.dot(sensorForward, direction);
                        inFov = dot >= fovCos;
                    }

                    // Determine detection type and confidence
                    var detectionType = DetectionType.Proximity;
                    var confidence = 1f;

                    if ((config.ValueRO.DetectionMask & DetectionMask.Sight) != 0 && inFov)
                    {
                        detectionType = DetectionType.Sight;
                        confidence = target.Visibility * (1f - distance / config.ValueRO.Range);
                    }
                    else if ((config.ValueRO.DetectionMask & DetectionMask.Sound) != 0)
                    {
                        detectionType = DetectionType.Sound;
                        confidence = target.Audibility * (1f - distance / config.ValueRO.Range);
                    }
                    else if ((config.ValueRO.DetectionMask & DetectionMask.Proximity) != 0)
                    {
                        detectionType = DetectionType.Proximity;
                        confidence = 1f - distance / config.ValueRO.Range;
                    }
                    else
                    {
                        continue; // No valid detection method
                    }

                    // Add detection
                    var relation = PerceptionRelationResolver.Resolve(
                        entity,
                        target.Entity,
                        target.Category,
                        _relationLookup,
                        _factionLookup,
                        _disguiseLookup,
                        _discoveryLookup,
                        _villagerLookup,
                        _villageLookup,
                        _bandLookup,
                        _armyLookup,
                        factionRelationships.AsArray());

                    detectedBuffer.Add(new DetectedEntity
                    {
                        Target = target.Entity,
                        Distance = distance,
                        Direction = direction,
                        DetectionType = detectionType,
                        Confidence = math.saturate(confidence),
                        DetectedAtTick = timeState.Tick,
                        ThreatLevel = target.ThreatLevel,
                        Relationship = relation.Score,
                        RelationKind = relation.Kind,
                        RelationFlags = relation.Flags
                    });

                    detectionCount++;

                    // Track highest threat
                    if (target.ThreatLevel > highestThreat)
                    {
                        highestThreat = target.ThreatLevel;
                        highestThreatEntity = target.Entity;
                    }

                    // Track nearest
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestEntity = target.Entity;
                    }
                }

                // Update sensor state
                sensorState.ValueRW.LastUpdateTick = timeState.Tick;
                sensorState.ValueRW.CurrentDetectionCount = (byte)detectionCount;
                sensorState.ValueRW.HighestThreat = highestThreat;
                sensorState.ValueRW.HighestThreatEntity = highestThreatEntity;
                sensorState.ValueRW.NearestEntity = nearestEntity;
                sensorState.ValueRW.NearestDistance = math.sqrt(nearestDistSq);
            }

            detectables.Dispose();
            factionRelationships.Dispose();
        }

        private struct DetectableData
        {
            public Entity Entity;
            public float3 Position;
            public float3 Forward;
            public float Visibility;
            public float Audibility;
            public byte ThreatLevel;
            public DetectableCategory Category;
        }
    }
}
