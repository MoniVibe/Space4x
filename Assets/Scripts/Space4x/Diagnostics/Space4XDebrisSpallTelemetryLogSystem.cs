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
    [UpdateAfter(typeof(Space4XDebrisSpallSystem))]
    public partial struct Space4XDebrisSpallTelemetryLogSystem : ISystem
    {
        private const double LogIntervalSeconds = 0.75;
        private double _nextLogTime;
        private float _accumulatedSeconds;
        private int _accumulatedSpawnCount;
        private float _lastSpawnRate;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XDebrisSpallFrameStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                state.Enabled = false;
                return;
            }

            var deltaTime = UTime.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            var stats = SystemAPI.GetSingleton<Space4XDebrisSpallFrameStats>();
            _accumulatedSeconds += deltaTime;
            _accumulatedSpawnCount += stats.DebrisSpawnedThisFrame;
            if (_accumulatedSeconds >= 1f)
            {
                _lastSpawnRate = _accumulatedSpawnCount / _accumulatedSeconds;
                _accumulatedSeconds = 0f;
                _accumulatedSpawnCount = 0;
            }

            var now = UTime.realtimeSinceStartupAsDouble;
            if (now < _nextLogTime)
            {
                return;
            }

            _nextLogTime = now + LogIntervalSeconds;

            if (stats.DebrisSpawnedThisFrame == 0 && stats.DebrisSpawnEventsThisFrame == 0 && stats.DebrisSuppressedByBudget == 0)
            {
                return;
            }

            UDebug.Log($"[Space4XDebrisSpallTelemetry] Spawned={stats.DebrisSpawnedThisFrame} Events={stats.DebrisSpawnEventsThisFrame} " +
                      $"Suppressed={stats.DebrisSuppressedByBudget} RatePerSec={_lastSpawnRate:0.0}");
        }
    }
}
#endif
