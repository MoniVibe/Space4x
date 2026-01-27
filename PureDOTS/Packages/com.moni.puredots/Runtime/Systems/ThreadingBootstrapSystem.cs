using PureDOTS.Runtime.Config;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Configures job worker count and Burst compilation options during bootstrap.
    /// Runs once at startup to apply threading settings from runtime config.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct ThreadingBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            ConfigureThreading(ref state);
            state.Enabled = false; // Run once
        }

        public void OnUpdate(ref SystemState state)
        {
            // No-op; configuration happens in OnCreate
        }

        private void ConfigureThreading(ref SystemState state)
        {
            // Get threading config (if available)
            int workerCount = 0;
            bool burstSync = true;
            
            if (SystemAPI.TryGetSingleton<ThreadingSettingsConfig>(out var threadingConfig))
            {
                workerCount = threadingConfig.OverrideWorkerCount;
                burstSync = threadingConfig.BurstCompileSynchronously;
            }
            else
            {
                // Use defaults if no config
                workerCount = 0; // Unity default
                burstSync = true; // Development default
            }

            // Configure worker count
            if (workerCount > 0)
            {
                var logicalCores = SystemInfo.processorCount;
                var clampedCount = math.min(workerCount, logicalCores);
                JobsUtility.JobWorkerCount = clampedCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.Log($"[ThreadingBootstrap] Worker count set to {clampedCount} (requested: {workerCount}, logical cores: {logicalCores})");
#endif
            }
            else
            {
                // Use Unity's default
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var defaultCount = JobsUtility.JobWorkerCount;
                UnityEngine.Debug.Log($"[ThreadingBootstrap] Using Unity default worker count: {defaultCount}");
#endif
            }

            // Configure Burst compilation
            #if UNITY_BURST
            BurstCompilerOptions.CompileSynchronously = burstSync;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (burstSync)
            {
                Debug.Log("[ThreadingBootstrap] Burst synchronous compilation enabled (development mode)");
            }
#endif
            #endif
        }
    }
}

