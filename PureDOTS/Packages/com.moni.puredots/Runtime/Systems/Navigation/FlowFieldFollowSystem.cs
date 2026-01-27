using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Villager;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Makes agents follow flow field directions, blending with steering for smooth movement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HotPathSystemGroup))]
    // Removed invalid UpdateAfter: VillagerTargetingSystem lives in VillagerSystemGroup.
    public partial struct FlowFieldFollowSystem : ISystem
    {
        private ComponentLookup<FlowFieldConfig> _configLookup;
        private BufferLookup<FlowFieldLayer> _layerLookup;
        private BufferLookup<FlowFieldCellData> _cellDataLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _configLookup = state.GetComponentLookup<FlowFieldConfig>(isReadOnly: true);
            _layerLookup = state.GetBufferLookup<FlowFieldLayer>(isReadOnly: true);
            _cellDataLookup = state.GetBufferLookup<FlowFieldCellData>(isReadOnly: true);
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

            if (!SystemAPI.HasSingleton<FlowFieldConfig>())
            {
                return;
            }

            _configLookup.Update(ref state);
            _layerLookup.Update(ref state);
            _cellDataLookup.Update(ref state);

            var configEntity = SystemAPI.GetSingletonEntity<FlowFieldConfig>();
            var config = _configLookup[configEntity];

            if (!_layerLookup.HasBuffer(configEntity) || !_cellDataLookup.HasBuffer(configEntity))
            {
                return;
            }

            var layers = _layerLookup[configEntity];
            var cellData = _cellDataLookup[configEntity];

            var job = new FollowFlowFieldJob
            {
                Config = config,
                Layers = layers.AsNativeArray(),
                CellData = cellData.AsNativeArray(),
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct FollowFlowFieldJob : IJobEntity
        {
            [ReadOnly]
            public FlowFieldConfig Config;

            [ReadOnly]
            public NativeArray<FlowFieldLayer> Layers;

            [ReadOnly]
            public NativeArray<FlowFieldCellData> CellData;

            public uint CurrentTick;
            public float DeltaTime;

            public void Execute(
                ref FlowFieldState flowState,
                ref VillagerMovement movement,
                ref LocalTransform transform,
                in FlowFieldAgentTag agentTag,
                [ChunkIndexInQuery] int chunkIndex)
            {
                if (Layers.Length == 0)
                {
                    return;
                }

                // Find layer index
                int layerIdx = -1;
                for (int i = 0; i < Layers.Length; i++)
                {
                    if (Layers[i].LayerId == flowState.CurrentLayerId)
                    {
                        layerIdx = i;
                        break;
                    }
                }

                if (layerIdx < 0)
                {
                    return;
                }

                var gridSize = Config.GridSize;
                var cellCount = Config.CellCount;
                var worldPos = new float2(transform.Position.xz);
                var cellCoords = WorldToCell(worldPos, Config);

                if (cellCoords.x < 0 || cellCoords.x >= gridSize.x ||
                    cellCoords.y < 0 || cellCoords.y >= gridSize.y)
                {
                    return;
                }

                var cellIdx = CellCoordsToIndex(cellCoords, gridSize);
                var dataIdx = layerIdx * cellCount + cellIdx;

                if (dataIdx < 0 || dataIdx >= CellData.Length)
                {
                    return;
                }

                var cellInfo = CellData[dataIdx];
                if (cellInfo.LayerId != flowState.CurrentLayerId)
                {
                    return;
                }

                // Update cached direction
                if (math.lengthsq(cellInfo.Direction) > 0.01f)
                {
                    flowState.CachedDirection = cellInfo.Direction;
                    flowState.CurrentCellIndex = cellIdx;
                    flowState.LastUpdateTick = CurrentTick;
                }

                // Blend flow direction with steering if available
                var flowDir = new float3(flowState.CachedDirection.x, 0f, flowState.CachedDirection.y);
                var targetDir = flowDir;

                // Apply speed scalar
                var speed = movement.BaseSpeed * flowState.SpeedScalar;
                movement.DesiredVelocity = math.normalize(targetDir) * speed;
                movement.CurrentSpeed = speed;
                movement.IsMoving = math.lengthsq(movement.DesiredVelocity) > 0.01f ? (byte)1 : (byte)0;
            }

            [BurstCompile]
            private int2 WorldToCell(float2 worldPos, FlowFieldConfig config)
            {
                var localPos = worldPos - config.WorldBoundsMin;
                return new int2(
                    (int)math.floor(localPos.x / config.CellSize),
                    (int)math.floor(localPos.y / config.CellSize)
                );
            }

            [BurstCompile]
            private int CellCoordsToIndex(int2 coords, int2 gridSize)
            {
                return coords.y * gridSize.x + coords.x;
            }
        }
    }
}
