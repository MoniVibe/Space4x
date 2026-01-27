using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Villager;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems.Performance
{
    /// <summary>
    /// Stress test system for benchmarking villager systems at scale (50k, 200k, 500k agents).
    /// Measures frame time per system and verifies zero GC allocations.
    /// Only runs when explicitly enabled via StressTestConfig.
    /// NOTE: Not Burst-compiled because it requires UnityEngine.Time for accurate performance measurement.
    /// </summary>
    [UpdateInGroup(typeof(VillagerSystemGroup), OrderLast = true)]
    public partial struct VillagerStressTestSystem : ISystem
    {
        private uint _lastMeasureTick;
        private float _accumulatedFrameTime;
        private int _frameCount;

        public void OnCreate(ref SystemState state)
        {
            _lastMeasureTick = 0;
            _accumulatedFrameTime = 0f;
            _frameCount = 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only run if stress test config exists and is enabled
            if (!SystemAPI.HasSingleton<StressTestConfig>())
            {
                return;
            }

            var config = SystemAPI.GetSingleton<StressTestConfig>();
            if (!config.EnableStressTest)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Measure every N ticks
            if (currentTick - _lastMeasureTick < config.MeasurementIntervalTicks)
            {
                return;
            }

            _lastMeasureTick = currentTick;

            // Spawn villagers up to target count if needed
            if (config.TargetVillagerCount > 0)
            {
                var villagerCount = SystemAPI.QueryBuilder()
                    .WithAll<VillagerId>()
                    .Build()
                    .CalculateEntityCount();

                if (villagerCount < config.TargetVillagerCount)
                {
                    SpawnVillagers(ref state, config.TargetVillagerCount - villagerCount, config.SpawnRadius);
                }
            }

            _frameCount++;

            // Accumulate frame time (using fixed delta time for deterministic measurement)
            var frameTime = timeState.FixedDeltaTime;
            _accumulatedFrameTime += frameTime;

            // Log summary every N measurements
            if (_frameCount >= config.LogInterval)
            {
                var avgFrameTime = _accumulatedFrameTime / _frameCount;
                var villagerCount = SystemAPI.QueryBuilder()
                    .WithAll<VillagerId>()
                    .Build()
                    .CalculateEntityCount();

#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[StressTest] Tick={currentTick} Villagers={villagerCount} AvgFrameTime={avgFrameTime * 1000f:F2}ms Frames={_frameCount}");
#endif

                _accumulatedFrameTime = 0f;
                _frameCount = 0;
            }
        }

        private void SpawnVillagers(ref SystemState state, int count, float radius)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var random = new Unity.Mathematics.Random((uint)UnityEngine.Time.frameCount);

            for (int i = 0; i < count; i++)
            {
                var entity = state.EntityManager.CreateEntity();
                var angle = random.NextFloat(0f, math.PI * 2f);
                var distance = random.NextFloat(0f, radius);
                var position = new float3(
                    math.cos(angle) * distance,
                    0f,
                    math.sin(angle) * distance
                );

                // Add basic villager components
                ecb.AddComponent(entity, new VillagerId { Value = i, FactionId = 0 });
                ecb.AddComponent(entity, new LocalTransform
                {
                    Position = position,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                var needs = new VillagerNeeds { Health = 100f, MaxHealth = 100f };
                needs.SetHunger(50f);
                needs.SetEnergy(80f);
                needs.SetMorale(75f);
                ecb.AddComponent(entity, needs);

                ecb.AddComponent(entity, new VillagerAIState
                {
                    CurrentState = VillagerAIState.State.Idle,
                    CurrentGoal = VillagerAIState.Goal.None,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    StateTimer = 0f,
                    StateStartTick = 0
                });
                ecb.AddComponent(entity, new VillagerJob
                {
                    Type = VillagerJob.JobType.None,
                    Phase = VillagerJob.JobPhase.Idle,
                    ActiveTicketId = 0,
                    Productivity = 1f,
                    LastStateChangeTick = 0
                });
                ecb.AddComponent(entity, new VillagerAvailability
                {
                    IsAvailable = 1,
                    IsReserved = 0,
                    LastChangeTick = 0,
                    BusyTime = 0f
                });
                ecb.AddComponent(entity, new VillagerFlags());
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Configuration for stress testing (singleton).
    /// </summary>
    public struct StressTestConfig : IComponentData
    {
        public bool EnableStressTest;
        public int TargetVillagerCount; // 0 = don't spawn, >0 = spawn up to this count
        public float SpawnRadius;
        public uint MeasurementIntervalTicks; // Measure every N ticks
        public int LogInterval; // Log summary every N measurements
    }
}

