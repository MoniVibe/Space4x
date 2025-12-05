using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using PureDOTS.Systems.Environment;
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
    /// Manages biodeck climate grids and evaluates species comfort for crew on ships/stations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ClimateControlSystem))]
    public partial struct BioDeckSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<BioDeckModule>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();

            var job = new BioDeckComfortJob
            {
                CurrentTick = timeState.Tick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct BioDeckComfortJob : IJobEntity
        {
            public uint CurrentTick;

            public void Execute(
                in BioDeckModule module,
                in DynamicBuffer<BioDeckCell> cells,
                in LocalTransform transform)
            {
                // Biodeck cells are managed by climate control sources within the module
                // This job can evaluate comfort for crew members based on their positions
                // For now, it's a placeholder that can be extended when crew systems are integrated
            }
        }
    }

    /// <summary>
    /// Creates climate control sources for biodeck modules based on their cell configurations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct BioDeckClimateControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BioDeckModule>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);

            foreach (var (moduleEntity, module, transform) in SystemAPI.Query<Entity, RefRO<BioDeckModule>, RefRO<LocalTransform>>())
            {
                if (!SystemAPI.HasBuffer<BioDeckCell>(moduleEntity))
                {
                    continue;
                }

                var cells = SystemAPI.GetBuffer<BioDeckCell>(moduleEntity);
                var worldPos = transform.ValueRO.Position;

                // Create climate control sources for each biodeck cell that has a target climate
                // This allows biodeck modules to have multiple climate zones
                for (int i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i];
                    var cellCoords = new int2(i % module.ValueRO.GridResolution.x, i / module.ValueRO.GridResolution.x);
                    var cellWorldPos = worldPos + module.ValueRO.LocalOrigin + 
                        new float3(cellCoords.x * module.ValueRO.CellSize, 0f, cellCoords.y * module.ValueRO.CellSize);

                    // Create a small climate control source for this cell
                    var sourceEntity = ecb.CreateEntity();
                    ecb.AddComponent(sourceEntity, new ClimateControlSource
                    {
                        Kind = ClimateControlKind.Structure,
                        Center = cellWorldPos,
                        Radius = module.ValueRO.CellSize * 0.5f,
                        TargetClimate = cell.Climate,
                        Strength = 0.2f // Strong influence within biodeck
                    });
                    ecb.AddComponent(sourceEntity, LocalTransform.FromPositionRotationScale(
                        cellWorldPos, quaternion.identity, 1f));
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

