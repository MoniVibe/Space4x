using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Updates spatial sensor caches for agents at configurable intervals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct SpatialSensorUpdateSystem : ISystem
    {
        private ComponentLookup<SpatialGridState> _gridStateLookup;
        private BufferLookup<SpatialGridEntry> _gridEntryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _gridStateLookup = state.GetComponentLookup<SpatialGridState>(isReadOnly: true);
            _gridEntryLookup = state.GetBufferLookup<SpatialGridEntry>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.HasSingleton<SpatialGridConfig>())
            {
                return;
            }

            var gridConfigEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();

            _gridStateLookup.Update(ref state);
            _gridEntryLookup.Update(ref state);

            var job = new UpdateSpatialSensorJob
            {
                CurrentTick = timeState.Tick,
                GridConfigEntity = gridConfigEntity,
                GridStateLookup = _gridStateLookup,
                GridEntryLookup = _gridEntryLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateSpatialSensorJob : IJobEntity
        {
            public uint CurrentTick;
            public Entity GridConfigEntity;
            [ReadOnly]
            public ComponentLookup<SpatialGridState> GridStateLookup;
            [ReadOnly]
            public BufferLookup<SpatialGridEntry> GridEntryLookup;

            public void Execute(
                ref SpatialSensor sensor,
                ref DynamicBuffer<SpatialSensorEntity> sensorEntities,
                in LocalTransform transform,
                in FlowFieldAgentTag agentTag)
            {
                // Check if update is needed
                if (sensor.UpdateIntervalTicks > 0)
                {
                    var ticksSinceUpdate = CurrentTick - sensor.LastUpdateTick;
                    if (ticksSinceUpdate < (uint)sensor.UpdateIntervalTicks)
                    {
                        return;
                    }
                }

                sensorEntities.Clear();
                sensor.NeighborCount = 0;
                sensor.ThreatCount = 0;
                sensor.ResourceCount = 0;

                // Check spatial grid data availability
                if (GridConfigEntity == Entity.Null ||
                    !GridStateLookup.HasComponent(GridConfigEntity) ||
                    !GridEntryLookup.HasBuffer(GridConfigEntity))
                {
                    sensor.LastUpdateTick = CurrentTick;
                    return;
                }

                var gridEntries = GridEntryLookup[GridConfigEntity];
                var agentPos = transform.Position;
                var radiusSq = sensor.SensorRadius * sensor.SensorRadius;

                // Query nearby entities from spatial grid
                for (int i = 0; i < gridEntries.Length; i++)
                {
                    var entry = gridEntries[i];
                    var distSq = math.distancesq(agentPos, entry.Position);
                    if (distSq <= radiusSq)
                    {
                        // Determine entity type (simplified - would need component lookups for real categorization)
                        byte entityType = 1; // Default to neighbor

                        sensorEntities.Add(new SpatialSensorEntity
                        {
                            Entity = entry.Entity,
                            Position = entry.Position,
                            EntityType = entityType,
                            Distance = math.sqrt(distSq)
                        });

                        switch (entityType)
                        {
                            case 1: // Neighbor
                                sensor.NeighborCount++;
                                break;
                            case 2: // Threat
                                sensor.ThreatCount++;
                                break;
                            case 3: // Resource
                                sensor.ResourceCount++;
                                break;
                        }
                    }
                }

                sensor.LastUpdateTick = CurrentTick;
            }
        }
    }
}

