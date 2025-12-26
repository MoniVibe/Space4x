using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.StrikeCraft
{
    /// <summary>
    /// Applies hangar/engineering work quality to strike craft based on carrier department stats.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDepartmentSystem))]
    [UpdateBefore(typeof(Space4X.Systems.AI.Space4XStrikeCraftBehaviorSystem))]
    public partial struct StrikeCraftMaintenanceQualitySystem : ISystem
    {
        private BufferLookup<DepartmentStatsBuffer> _departmentStatsLookup;
        private ComponentLookup<StrikeCraftMaintenanceQuality> _qualityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<StrikeCraftProfile>();
            _departmentStatsLookup = state.GetBufferLookup<DepartmentStatsBuffer>(true);
            _qualityLookup = state.GetComponentLookup<StrikeCraftMaintenanceQuality>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _departmentStatsLookup.Update(ref state);
            _qualityLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (profile, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithEntityAccess())
            {
                var quality = 0.75f;
                var carrier = profile.ValueRO.Carrier;
                if (carrier != Entity.Null && _departmentStatsLookup.HasBuffer(carrier))
                {
                    var statsBuffer = _departmentStatsLookup[carrier];
                    var flightQuality = -1f;
                    var engineeringQuality = -1f;

                    for (var i = 0; i < statsBuffer.Length; i++)
                    {
                        var stats = statsBuffer[i].Stats;
                        if (stats.Type == DepartmentType.Flight)
                        {
                            flightQuality = ComputeDepartmentQuality(stats);
                        }
                        else if (stats.Type == DepartmentType.Engineering)
                        {
                            engineeringQuality = ComputeDepartmentQuality(stats);
                        }
                    }

                    if (flightQuality >= 0f && engineeringQuality >= 0f)
                    {
                        quality = (flightQuality + engineeringQuality) * 0.5f;
                    }
                    else if (flightQuality >= 0f)
                    {
                        quality = flightQuality;
                    }
                    else if (engineeringQuality >= 0f)
                    {
                        quality = engineeringQuality;
                    }
                }

                if (!_qualityLookup.HasComponent(entity))
                {
                    ecb.AddComponent(entity, new StrikeCraftMaintenanceQuality { Value = quality });
                }
                else
                {
                    var current = _qualityLookup[entity];
                    current.Value = quality;
                    _qualityLookup[entity] = current;
                }
            }
        }

        private static float ComputeDepartmentQuality(in DepartmentStats stats)
        {
            var cohesion = math.clamp((float)stats.Cohesion, 0f, 1f);
            var skill = math.clamp((float)stats.SkillLevel, 0f, 1f);
            var efficiency = math.clamp((float)stats.Efficiency, 0.5f, 1.5f);
            var normalizedEfficiency = math.saturate((efficiency - 0.5f) / 1.0f);
            return math.saturate((cohesion + skill + normalizedEfficiency) / 3f);
        }
    }
}
