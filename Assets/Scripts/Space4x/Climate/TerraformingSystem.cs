using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Space4X.Climate;
using Unity.Burst;
using Unity.Collections;
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
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct SectorClimateSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SectorClimateProfile>();
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            _entityLookup.Update(ref state);
            var sectorQuery = SystemAPI.QueryBuilder()
                .WithAll<SectorClimateProfile, LocalTransform>()
                .Build();
            if (sectorQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<ClimateControlSource>()
                .Build();

            var sectors = sectorQuery.ToComponentDataArray<SectorClimateProfile>(Allocator.TempJob);
            var sectorTransforms = sectorQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var sources = sourceQuery.ToComponentDataArray<ClimateControlSource>(Allocator.TempJob);
            var sourceEntities = sourceQuery.ToEntityArray(Allocator.TempJob);

            var updates = new NativeList<ClimateUpdateCommand>(math.max(1, sectors.Length), Allocator.TempJob);
            var creates = new NativeList<ClimateCreateCommand>(math.max(1, sectors.Length), Allocator.TempJob);

            var job = new BuildClimateCommandsJob
            {
                Sectors = sectors,
                SectorTransforms = sectorTransforms,
                Sources = sources,
                SourceEntities = sourceEntities,
                Updates = updates.AsParallelWriter(),
                Creates = creates.AsParallelWriter(),
                Strength = 0.05f
            };

            state.Dependency = job.Schedule(sectors.Length, 32, state.Dependency);
            state.Dependency.Complete();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < updates.Length; i++)
            {
                var update = updates[i];
                if (!_entityLookup.Exists(update.Entity))
                {
                    continue;
                }

                ecb.SetComponent(update.Entity, new ClimateControlSource
                {
                    Kind = ClimateControlKind.Structure,
                    Center = update.Center,
                    Radius = update.Radius,
                    TargetClimate = update.TargetClimate,
                    Strength = update.Strength
                });
            }

            for (int i = 0; i < creates.Length; i++)
            {
                var create = creates[i];
                var newEntity = ecb.CreateEntity();
                ecb.AddComponent(newEntity, new ClimateControlSource
                {
                    Kind = ClimateControlKind.Structure,
                    Center = create.Center,
                    Radius = create.Radius,
                    TargetClimate = create.TargetClimate,
                    Strength = create.Strength
                });
                ecb.AddComponent(newEntity, LocalTransform.FromPositionRotationScale(
                    create.Center, quaternion.identity, 1f));
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            updates.Dispose();
            creates.Dispose();
            sectors.Dispose();
            sectorTransforms.Dispose();
            sources.Dispose();
            sourceEntities.Dispose();
        }

        private struct ClimateUpdateCommand
        {
            public Entity Entity;
            public float3 Center;
            public float Radius;
            public ClimateVector TargetClimate;
            public float Strength;
        }

        private struct ClimateCreateCommand
        {
            public float3 Center;
            public float Radius;
            public ClimateVector TargetClimate;
            public float Strength;
        }

        [BurstCompile]
        private struct BuildClimateCommandsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorClimateProfile> Sectors;
            [ReadOnly] public NativeArray<LocalTransform> SectorTransforms;
            [ReadOnly] public NativeArray<ClimateControlSource> Sources;
            [ReadOnly] public NativeArray<Entity> SourceEntities;
            public NativeList<ClimateUpdateCommand>.ParallelWriter Updates;
            public NativeList<ClimateCreateCommand>.ParallelWriter Creates;
            public float Strength;

            public void Execute(int index)
            {
                var sector = Sectors[index];
                var center = SectorTransforms[index].Position;
                var threshold = sector.InfluenceRadius * 0.1f;

                var found = -1;
                for (int i = 0; i < Sources.Length; i++)
                {
                    if (math.distance(Sources[i].Center, center) < threshold)
                    {
                        found = i;
                        break;
                    }
                }

                if (found >= 0)
                {
                    Updates.AddNoResize(new ClimateUpdateCommand
                    {
                        Entity = SourceEntities[found],
                        Center = center,
                        Radius = sector.InfluenceRadius,
                        TargetClimate = sector.TargetClimate,
                        Strength = Strength
                    });
                }
                else
                {
                    Creates.AddNoResize(new ClimateCreateCommand
                    {
                        Center = center,
                        Radius = sector.InfluenceRadius,
                        TargetClimate = sector.TargetClimate,
                        Strength = Strength
                    });
                }
            }
        }
    }
}
