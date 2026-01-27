using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.Time.Branching;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Time.Branching
{
    /// <summary>
    /// System that manages timeline branches.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct TimelineBranchSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Update branch current ticks
            foreach (var (branch, entity) in 
                SystemAPI.Query<RefRW<TimelineBranch>>()
                    .WithEntityAccess())
            {
                if (branch.ValueRO.IsActive && !branch.ValueRO.IsFrozen)
                {
                    branch.ValueRW.CurrentTick = currentTick;
                    if (currentTick > branch.ValueRO.MaxTick)
                    {
                        branch.ValueRW.MaxTick = currentTick;
                    }
                }
            }

            // Check for branches that should be frozen
            foreach (var (branch, config, entity) in 
                SystemAPI.Query<RefRW<TimelineBranch>, RefRO<TimeSpineConfig>>()
                    .WithEntityAccess())
            {
                if (BranchingHelpers.ShouldFreezeBranch(branch.ValueRO, config.ValueRO))
                {
                    branch.ValueRW.IsFrozen = true;
                    branch.ValueRW.IsActive = false;
                }
            }
        }
    }

    /// <summary>
    /// System that processes what-if simulation requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TimelineBranchSystem))]
    [BurstCompile]
    public partial struct WhatIfSimulationSystem : ISystem
    {
        // Instance field for Burst-compatible FixedString pattern (initialized in OnCreate)
        private FixedString64Bytes _mainBranchName;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            // Initialize FixedString pattern (OnCreate is not Burst-compiled)
            _mainBranchName = new FixedString64Bytes("main");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process what-if requests
            foreach (var (request, modifications, entity) in 
                SystemAPI.Query<RefRO<WhatIfRequest>, DynamicBuffer<WhatIfModification>>()
                    .WithEntityAccess())
            {
                // Count current branches
                int branchCount = 0;
                foreach (var branch in SystemAPI.Query<RefRO<TimelineBranch>>())
                {
                    branchCount++;
                }

                // Get config (use default if not found)
                var config = BranchingHelpers.DefaultConfig;
                foreach (var configComp in SystemAPI.Query<RefRO<TimeSpineConfig>>())
                {
                    config = configComp.ValueRO;
                    break;
                }

                // Check if we can create a branch
                if (BranchingHelpers.CanCreateBranch(branchCount, config, false))
                {
                    // Create new branch
                    uint seed = (uint)(entity.Index ^ entity.Version ^ currentTick);
                    var branchId = BranchingHelpers.GenerateBranchId(request.ValueRO.StartTick, seed);
                    byte priority = BranchingHelpers.GetScenarioPriority(request.ValueRO.ScenarioName);

                    var newBranch = BranchingHelpers.CreateBranch(
                        branchId,
                        _mainBranchName,
                        request.ValueRO.StartTick,
                        priority);

                    // Create branch entity
                    var branchEntity = ecb.CreateEntity();
                    ecb.AddComponent(branchEntity, newBranch);

                    // Create result tracker
                    ecb.AddComponent(branchEntity, new WhatIfResult
                    {
                        ScenarioName = request.ValueRO.ScenarioName,
                        BranchId = branchId,
                        IsComplete = false
                    });

                    // Create snapshot
                    ecb.AddComponent(branchEntity, new BranchSnapshot
                    {
                        BranchId = branchId,
                        SnapshotTick = request.ValueRO.StartTick,
                        IsValid = true
                    });
                }

                // Remove processed request
                ecb.RemoveComponent<WhatIfRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System that handles branch merging.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WhatIfSimulationSystem))]
    [BurstCompile]
    public partial struct BranchMergeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process merge requests
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<BranchMergeRequest>>()
                    .WithEntityAccess())
            {
                // Find source and target branches
                Entity sourceEntity = Entity.Null;
                Entity targetEntity = Entity.Null;

                foreach (var (branch, branchEntity) in 
                    SystemAPI.Query<RefRO<TimelineBranch>>()
                        .WithEntityAccess())
                {
                    if (branch.ValueRO.BranchId.Equals(request.ValueRO.SourceBranchId))
                    {
                        sourceEntity = branchEntity;
                    }
                    if (branch.ValueRO.BranchId.Equals(request.ValueRO.TargetBranchId))
                    {
                        targetEntity = branchEntity;
                    }
                }

                if (sourceEntity != Entity.Null && targetEntity != Entity.Null)
                {
                    // Mark source for merge
                    foreach (var (branch, branchEntity) in 
                        SystemAPI.Query<RefRW<TimelineBranch>>()
                            .WithEntityAccess())
                    {
                        if (branchEntity == sourceEntity)
                        {
                            branch.ValueRW.IsMarkedForMerge = true;
                            branch.ValueRW.IsActive = false;
                            break;
                        }
                    }

                    // TODO: Actual merge logic would copy entity states
                    // from source branch to target branch
                }

                // Remove processed request
                ecb.RemoveComponent<BranchMergeRequest>(entity);
            }

            // Process comparison requests
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<ComparisonRequest>>()
                    .WithEntityAccess())
            {
                // Create comparison result
                var comparison = new BranchComparison
                {
                    BranchAId = request.ValueRO.BranchAId,
                    BranchBId = request.ValueRO.BranchBId,
                    ComparedAtTick = currentTick
                };

                // TODO: Calculate actual differences between branches
                // This would involve comparing entity states

                ecb.AddComponent(entity, comparison);
                ecb.RemoveComponent<ComparisonRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

