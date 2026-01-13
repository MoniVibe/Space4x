using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Ensures strike craft scaffolding components exist without per-tick structural churn.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XStrikeCraftBootstrapSystem : ISystem
    {
        private const uint BootstrapCadenceTicks = 60;
        private uint _lastBootstrapTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<StrikeCraftState>();
            _lastBootstrapTick = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (_lastBootstrapTick != 0 && timeState.Tick - _lastBootstrapTick < BootstrapCadenceTicks)
            {
                return;
            }

            _lastBootstrapTick = timeState.Tick;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            using var missingEngagement = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftState>()
                .WithNone<Space4XEngagement>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingEngagement.Length; i++)
            {
                ecb.AddComponent(missingEngagement[i], new Space4XEngagement());
            }

            using var missingSupply = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftState>()
                .WithNone<SupplyStatus>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingSupply.Length; i++)
            {
                ecb.AddComponent(missingSupply[i], SupplyStatus.DefaultStrikeCraft);
            }

            using var missingDogfightSteering = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftDogfightTag>()
                .WithNone<StrikeCraftDogfightSteering>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingDogfightSteering.Length; i++)
            {
                ecb.AddComponent(missingDogfightSteering[i], new StrikeCraftDogfightSteering());
            }

            using var missingDogfightMetrics = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftDogfightTag>()
                .WithNone<StrikeCraftDogfightMetrics>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingDogfightMetrics.Length; i++)
            {
                ecb.AddComponent(missingDogfightMetrics[i], new StrikeCraftDogfightMetrics());
            }

            using var missingDogfightSamples = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftDogfightTag>()
                .WithNone<StrikeCraftDogfightSample>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            for (int i = 0; i < missingDogfightSamples.Length; i++)
            {
                ecb.AddBuffer<StrikeCraftDogfightSample>(missingDogfightSamples[i]);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
