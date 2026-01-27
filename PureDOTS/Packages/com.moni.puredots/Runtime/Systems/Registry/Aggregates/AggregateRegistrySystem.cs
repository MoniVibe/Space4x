using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.Registry.Aggregates;
using PureDOTS.Runtime.Components;
using RegistryAggregateMember = PureDOTS.Runtime.Registry.Aggregates.AggregateMember;

namespace PureDOTS.Runtime.Systems.Registry.Aggregates
{
    /// <summary>
    /// System that maintains aggregate registry entries.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct AggregateRegistrySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Update aggregate statistics
            foreach (var (aggregate, members, entity) in 
                SystemAPI.Query<RefRW<AggregateRegistryEntry>, DynamicBuffer<RegistryAggregateMember>>()
                    .WithEntityAccess())
            {
                AggregateHelpers.CalculateAggregateStats(ref aggregate.ValueRW, in members, currentTick);
            }
        }
    }

    /// <summary>
    /// System that handles compression requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AggregateRegistrySystem))]
    [BurstCompile]
    public partial struct CompressionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process compression requests
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<CompressionRequest>>()
                    .WithEntityAccess())
            {
                // Find the target aggregate
                foreach (var (aggregate, aggregateEntity) in 
                    SystemAPI.Query<RefRW<AggregateRegistryEntry>>()
                        .WithEntityAccess())
                {
                    if (aggregateEntity == request.ValueRO.GroupEntity)
                    {
                        aggregate.ValueRW.IsCompressed = true;
                        aggregate.ValueRW.CompressionLevel = request.ValueRO.TargetCompressionLevel;
                        break;
                    }
                }

                // Remove request
                ecb.RemoveComponent<CompressionRequest>(entity);
            }

            // Process decompression requests
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<DecompressionRequest>>()
                    .WithEntityAccess())
            {
                foreach (var (aggregate, aggregateEntity) in 
                    SystemAPI.Query<RefRW<AggregateRegistryEntry>>()
                        .WithEntityAccess())
                {
                    if (aggregateEntity == request.ValueRO.GroupEntity)
                    {
                        aggregate.ValueRW.IsCompressed = false;
                        aggregate.ValueRW.CompressionLevel = 0;
                        break;
                    }
                }

                ecb.RemoveComponent<DecompressionRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System that runs background simulation for compressed groups.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CompressionSystem))]
    [BurstCompile]
    public partial struct BackgroundSimSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            float deltaTime = timeState.DeltaTime;

            // Run background simulation for compressed aggregates
            foreach (var (aggregate, production, bgState, config, entity) in 
                SystemAPI.Query<RefRW<AggregateRegistryEntry>, RefRO<AggregateProduction>, RefRW<BackgroundSimState>, RefRO<CompressionConfig>>()
                    .WithEntityAccess())
            {
                if (!aggregate.ValueRO.IsCompressed)
                    continue;

                if (bgState.ValueRO.IsPaused)
                    continue;

                // Check if should update
                uint interval = AggregateHelpers.GetUpdateInterval(aggregate.ValueRO.CompressionLevel, config.ValueRO);
                if (currentTick - bgState.ValueRO.LastSimTick < interval)
                    continue;

                // Simulate production
                float simDeltaTime = (currentTick - bgState.ValueRO.LastSimTick) * deltaTime;
                AggregateHelpers.SimulateProduction(ref aggregate.ValueRW, production.ValueRO, simDeltaTime);

                bgState.ValueRW.LastSimTick = currentTick;
            }
        }
    }

    /// <summary>
    /// System that generates pseudo-history for compressed periods.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BackgroundSimSystem))]
    [BurstCompile]
    public partial struct PseudoHistorySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Generate history for decompressing aggregates
            foreach (var (aggregate, production, history, bgState, entity) in 
                SystemAPI.Query<RefRO<AggregateRegistryEntry>, RefRO<AggregateProduction>, DynamicBuffer<PseudoHistoryEntry>, RefRO<BackgroundSimState>>()
                    .WithEntityAccess())
            {
                var historyBuffer = history;

                // Only generate when transitioning from compressed to uncompressed
                if (aggregate.ValueRO.IsCompressed)
                    continue;

                // Check if we have a gap in history
                uint lastHistoryTick = 0;
                if (historyBuffer.Length > 0)
                {
                    lastHistoryTick = historyBuffer[historyBuffer.Length - 1].Tick;
                }

                if (currentTick - lastHistoryTick > 120) // More than 2 seconds gap
                {
                    uint seed = (uint)(entity.Index ^ entity.Version ^ currentTick);
                    AggregateHelpers.GeneratePseudoHistory(
                        ref historyBuffer,
                        aggregate.ValueRO,
                        production.ValueRO,
                        lastHistoryTick,
                        currentTick,
                        seed);
                }
            }
        }
    }
}

