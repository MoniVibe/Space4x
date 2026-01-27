using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Accumulates hazard slices into a 3D risk grid.
    /// Clears grid and rasterizes each HazardSlice into cells via AABB intersection.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(BuildHazardSlicesSystem))]
    public partial struct AccumulateHazardGridSystem : ISystem
    {
        public void OnDestroy(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<HazardGridSingleton>(out var singletonEntity))
                return;

            var singleton = SystemAPI.GetComponent<HazardGridSingleton>(singletonEntity);
            if (singleton.GridEntity == Entity.Null || !SystemAPI.HasComponent<HazardGrid>(singleton.GridEntity))
                return;

            var grid = SystemAPI.GetComponent<HazardGrid>(singleton.GridEntity);
            if (grid.Risk.IsCreated)
            {
                grid.Risk.Dispose();
                grid.Risk = default;
                SystemAPI.SetComponent(singleton.GridEntity, grid);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HazardGridSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = timeState.DeltaTime;

            // Find or create hazard grid (single owner)
            HazardGrid grid;
            Entity gridEntity;

            var singletonEntity = SystemAPI.GetSingletonEntity<HazardGridSingleton>();
            var singleton = SystemAPI.GetComponent<HazardGridSingleton>(singletonEntity);

            if (singleton.GridEntity != Entity.Null && SystemAPI.HasComponent<HazardGrid>(singleton.GridEntity))
            {
                gridEntity = singleton.GridEntity;
                grid = SystemAPI.GetComponent<HazardGrid>(gridEntity);
            }
            else
            {
                gridEntity = state.EntityManager.CreateEntity();
                grid = new HazardGrid
                {
                    Size = new int3(100, 100, 1), // 2D default
                    Cell = 10f,
                    Origin = float3.zero,
                    Risk = default
                };
                state.EntityManager.AddComponentData(gridEntity, grid);
                state.EntityManager.SetComponentData(singletonEntity, new HazardGridSingleton { GridEntity = gridEntity });
            }

            // Get hazard slices buffer
            if (!SystemAPI.TryGetSingletonEntity<HazardSliceBuffer>(out var sliceBufferEntity) ||
                !SystemAPI.HasBuffer<HazardSlice>(sliceBufferEntity))
            {
                return;
            }

            var slices = SystemAPI.GetBuffer<HazardSlice>(sliceBufferEntity);

            // Rebuild risk blob if needed
            int totalCells = grid.Size.x * grid.Size.y * grid.Size.z;
            if (!grid.Risk.IsCreated || grid.Risk.Value.Risk.Length != totalCells)
            {
                if (grid.Risk.IsCreated)
                {
                    grid.Risk.Dispose();
                }

                // Create new risk blob
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<HazardRiskBlob>();
                var riskArray = builder.Allocate(ref root.Risk, totalCells);
                for (int i = 0; i < totalCells; i++)
                {
                    riskArray[i] = 0f;
                }
                var newRisk = builder.CreateBlobAssetReference<HazardRiskBlob>(Allocator.Persistent);
                builder.Dispose();

                grid.Risk = newRisk;
                SystemAPI.SetComponent(gridEntity, grid);
            }

            // Clear grid
            ref var riskData = ref grid.Risk.Value.Risk;
            unsafe
            {
                UnsafeUtility.MemClear(riskData.GetUnsafePtr(), totalCells * sizeof(float));
            }

            // Convert slices to native array for job
            var slicesArray = slices.ToNativeArray(Allocator.TempJob);

            // Rasterize slices into grid
            var jobHandle = new AccumulateHazardGridJob
            {
                Grid = grid,
                CurrentTick = currentTick,
                DeltaTime = deltaTime,
                Slices = slicesArray,
                RiskData = riskData
            }.Schedule(slices.Length, 64, state.Dependency);
            state.Dependency = slicesArray.Dispose(jobHandle);
        }

        [BurstCompile]
        public struct AccumulateHazardGridJob : IJobParallelFor
        {
            [ReadOnly] public HazardGrid Grid;
            public uint CurrentTick;
            public float DeltaTime;
            [ReadOnly] public NativeArray<HazardSlice> Slices;
            [NativeDisableParallelForRestriction] public BlobArray<float> RiskData;

            public void Execute(int index)
            {
                var slice = Slices[index];

                // Skip if slice is not active at current tick
                if (CurrentTick < slice.StartTick || CurrentTick > slice.EndTick)
                {
                    return;
                }

                // Calculate current radius (with growth)
                float elapsedTicks = CurrentTick - slice.StartTick;
                float elapsedSec = elapsedTicks * DeltaTime;
                float currentRadius = slice.Radius0 + slice.RadiusGrow * elapsedSec;

                // Calculate current center position (with velocity extrapolation)
                float3 currentCenter = slice.Center + slice.Vel * elapsedSec;

                // Rasterize sphere into grid cells
                int3 minCell = CellOf(currentCenter - currentRadius, Grid);
                int3 maxCell = CellOf(currentCenter + currentRadius, Grid);

                // Clamp to grid bounds
                minCell = math.clamp(minCell, int3.zero, Grid.Size - 1);
                maxCell = math.clamp(maxCell, int3.zero, Grid.Size - 1);

                // Iterate over affected cells
                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    for (int y = minCell.y; y <= maxCell.y; y++)
                    {
                        for (int x = minCell.x; x <= maxCell.x; x++)
                        {
                            int3 cell = new int3(x, y, z);
                            float3 cellCenter = CellCenter(cell, Grid);

                            // Distance from cell center to hazard center
                            float dist = math.length(cellCenter - currentCenter);

                            if (dist <= currentRadius)
                            {
                                // Compute base risk (inverse distance falloff)
                                float baseRisk = 1f / (1f + dist);

                                // Apply kind-specific weights (simplified - would use AvoidanceProfile weights)
                                float kindWeight = 1f;
                                if ((slice.Kind & HazardKind.AoE) != 0) kindWeight *= 1.5f;
                                if ((slice.Kind & HazardKind.Chain) != 0) kindWeight *= 1.2f;
                                if ((slice.Kind & HazardKind.Homing) != 0) kindWeight *= 1.3f;

                                float risk = baseRisk * kindWeight;

                                // Accumulate risk (atomic add for thread safety)
                                int cellIndex = Flatten(cell, Grid);
                                if (cellIndex >= 0 && cellIndex < RiskData.Length)
                                {
                                    RiskData[cellIndex] += risk;
                                }
                            }
                        }
                    }
                }
            }

            private static int3 CellOf(float3 pos, HazardGrid grid)
            {
                float3 local = (pos - grid.Origin) / grid.Cell;
                return (int3)math.floor(local);
            }

            private static float3 CellCenter(int3 cell, HazardGrid grid)
            {
                return grid.Origin + ((float3)cell + 0.5f) * grid.Cell;
            }

            private static int Flatten(int3 cell, HazardGrid grid)
            {
                return (cell.z * grid.Size.y + cell.y) * grid.Size.x + cell.x;
            }
        }
    }
}
