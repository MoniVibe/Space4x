#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Runtime.Core;
using Space4X.Presentation;
using Unity.Entities;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using UTime = UnityEngine.Time;

namespace Space4X.Diagnostics
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XAsteroidChunkMeshSystem))]
    public partial struct Space4XAsteroidChunkMeshTelemetryLogSystem : ISystem
    {
        private const double LogIntervalSeconds = 0.75;
        private double _nextLogTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XAsteroidChunkMeshRebuildQueue>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                state.Enabled = false;
                return;
            }

            var now = UTime.realtimeSinceStartupAsDouble;
            if (now < _nextLogTime)
            {
                return;
            }

            _nextLogTime = now + LogIntervalSeconds;

            var queueEntity = SystemAPI.GetSingletonEntity<Space4XAsteroidChunkMeshRebuildQueue>();
            if (!SystemAPI.HasComponent<Space4XAsteroidChunkMeshFrameStats>(queueEntity))
            {
                return;
            }

            var stats = SystemAPI.GetComponent<Space4XAsteroidChunkMeshFrameStats>(queueEntity);
            if (stats.ChunksBuiltThisFrame == 0 && stats.QueueLength == 0)
            {
                return;
            }

            UDebug.Log(
                $"[Space4XAsteroidChunkMeshTelemetry] Queue={stats.QueueLength} Built={stats.ChunksBuiltThisFrame} " +
                $"BuildMs={stats.TotalBuildMsThisFrame:0.00} Verts={stats.TotalVertsThisFrame} " +
                $"Indices={stats.TotalIndicesThisFrame} LastMs={stats.LastChunkBuildMs:0.00} " +
                $"BudgetMs={stats.BudgetMs:0.00} MinChunks={stats.MinChunksPerFrame} Skipped={stats.SkippedDueToBudget}");
        }
    }
}
#endif
