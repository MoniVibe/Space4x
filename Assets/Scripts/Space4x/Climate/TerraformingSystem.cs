using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Space4X.Climate;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Climate.Systems
{
    /// <summary>
    /// Advances terraforming projects, gradually adjusting planetary climate.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct TerraformingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TerraformingProject>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var deltaSeconds = timeState.FixedDeltaTime;

            var job = new TerraformingJob
            {
                DeltaSeconds = deltaSeconds
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct TerraformingJob : IJobEntity
        {
            public float DeltaSeconds;

            public void Execute(
                ref TerraformingProject project,
                in Entity entity)
            {
                if (project.Planet == Entity.Null)
                {
                    return;
                }

                // Advance progress
                project.Progress = math.clamp(
                    project.Progress + project.TerraformingRate * DeltaSeconds,
                    0f, 1f);

                // When progress reaches 1.0, the planet's global climate should be updated
                // This would be handled by a separate system that reads TerraformingProject
                // and applies the target climate to the planet's climate control sources
            }
        }
    }

    /// <summary>
    /// Creates climate control sources for sector climate profiles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct SectorClimateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SectorClimateProfile>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);

            foreach (var (sector, transform, sectorEntity) in SystemAPI.Query<RefRO<SectorClimateProfile>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                // Find or create climate control source for this sector
                Entity? existingSource = null;
                foreach (var (source, sourceEntity) in SystemAPI.Query<RefRO<ClimateControlSource>>()
                             .WithEntityAccess())
                {
                    if (math.distance(source.ValueRO.Center, transform.ValueRO.Position) < sector.ValueRO.InfluenceRadius * 0.1f)
                    {
                        existingSource = sourceEntity;
                        break;
                    }
                }

                if (existingSource.HasValue)
                {
                    // Update existing source
                    var source = SystemAPI.GetComponent<ClimateControlSource>(existingSource.Value);
                    source.TargetClimate = sector.ValueRO.TargetClimate;
                    source.Radius = sector.ValueRO.InfluenceRadius;
                    source.Strength = 0.05f; // Slow, gradual sector climate change
                    source.Center = transform.ValueRO.Position;
                    ecb.SetComponent(existingSource.Value, source);
                }
                else
                {
                    // Create new source
                    var newEntity = ecb.CreateEntity();
                    ecb.AddComponent(newEntity, new ClimateControlSource
                    {
                        Kind = ClimateControlKind.Structure,
                        Center = transform.ValueRO.Position,
                        Radius = sector.ValueRO.InfluenceRadius,
                        TargetClimate = sector.ValueRO.TargetClimate,
                        Strength = 0.05f
                    });
                    ecb.AddComponent(newEntity, LocalTransform.FromPositionRotationScale(
                        transform.ValueRO.Position, quaternion.identity, 1f));
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

