using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Stamps SpatialGridResidency.Version with the current SpatialGridState.Version after grid rebuilds.
    /// This ensures registries can validate spatial continuity using residency version data.
    /// </summary>
    /// <remarks>
    /// Runs after <see cref="SpatialGridBuildSystem"/> and before registry sync systems.
    /// This fixes the issue where SpatialGridResidency.Version remained zero after grid rebuilds,
    /// preventing registry continuity validation from working correctly.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(SpatialGridBuildSystem))]
    [UpdateBefore(typeof(RegistrySpatialSyncSystem))]
    public partial struct SpatialResidencyVersionSystem : ISystem
    {
        private uint _lastProcessedVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialGridState>();
            state.RequireForUpdate<RewindState>();
            _lastProcessedVersion = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var gridState = SystemAPI.GetSingleton<SpatialGridState>();
            var currentVersion = gridState.Version;

            // Skip if grid hasn't been updated
            if (currentVersion == _lastProcessedVersion)
            {
                return;
            }

            _lastProcessedVersion = currentVersion;

            // Stamp all SpatialGridResidency components with the current grid version
            var job = new StampResidencyVersionJob
            {
                GridVersion = currentVersion
            };

            var handle = job.ScheduleParallel(state.Dependency);
            handle.Complete();
            state.Dependency = default;
        }

        [BurstCompile]
        private partial struct StampResidencyVersionJob : IJobEntity
        {
            public uint GridVersion;

            public void Execute(ref SpatialGridResidency residency)
            {
                residency.Version = GridVersion;
            }
        }
    }
}

