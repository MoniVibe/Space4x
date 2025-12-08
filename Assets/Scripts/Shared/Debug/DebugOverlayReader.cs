using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Space4X.Shared.Debug
{
    /// <summary>
    /// MonoBehaviour that reads DOTS singleton stats and displays them in UI text fields.
    /// Queries PureDOTS singletons for tick, sim time, and entity counts.
    /// </summary>
    public sealed class DebugOverlayReader : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Text field for displaying current tick")]
        public Text tickText;

        [Tooltip("Text field for displaying simulation time")]
        public Text simTimeText;

        [Tooltip("Text field for displaying carrier count")]
        public Text carrierCountText;

        [Tooltip("Text field for displaying mining vessel count")]
        public Text miningVesselCountText;

        [Tooltip("Text field for displaying asteroid count")]
        public Text asteroidCountText;

        private World _world;
        private EntityQuery _timeStateQuery;
        private EntityQuery _worldMetricsQuery;

        private void Start()
        {
            // Find the default world
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world != null && _world.IsCreated)
            {
                var entityManager = _world.EntityManager;
                _timeStateQuery = entityManager.CreateEntityQuery(typeof(TimeState));
                _worldMetricsQuery = entityManager.CreateEntityQuery(typeof(WorldMetrics));
            }
        }

        private void Update()
        {
            if (_world == null || !_world.IsCreated)
            {
                _world = World.DefaultGameObjectInjectionWorld;
                if (_world != null && _world.IsCreated)
                {
                    var entityManager = _world.EntityManager;
                    _timeStateQuery = entityManager.CreateEntityQuery(typeof(TimeState));
                    _worldMetricsQuery = entityManager.CreateEntityQuery(typeof(WorldMetrics));
                }
                return;
            }

            var entityManager = _world.EntityManager;

            // Update tick and sim time from TimeState
            if (_timeStateQuery != null && _timeStateQuery.CalculateEntityCount() > 0)
            {
                var timeState = _timeStateQuery.GetSingleton<TimeState>();
                
                if (tickText != null)
                {
                    tickText.text = $"Tick: {timeState.CurrentTick}";
                }

                if (simTimeText != null)
                {
                    simTimeText.text = $"Sim Time: {timeState.SimulationTimeSeconds:F2}s";
                }
            }
            else
            {
                if (tickText != null) tickText.text = "Tick: N/A";
                if (simTimeText != null) simTimeText.text = "Sim Time: N/A";
            }

            // Update entity counts from WorldMetrics
            if (_worldMetricsQuery != null && _worldMetricsQuery.CalculateEntityCount() > 0)
            {
                var metrics = _worldMetricsQuery.GetSingleton<WorldMetrics>();

                if (carrierCountText != null)
                {
                    carrierCountText.text = $"Carriers: {metrics.CarrierCount}";
                }

                if (miningVesselCountText != null)
                {
                    miningVesselCountText.text = $"Mining Vessels: {metrics.MiningVesselCount}";
                }

                if (asteroidCountText != null)
                {
                    asteroidCountText.text = $"Asteroids: {metrics.AsteroidCount}";
                }
            }
            else
            {
                if (carrierCountText != null) carrierCountText.text = "Carriers: N/A";
                if (miningVesselCountText != null) miningVesselCountText.text = "Mining Vessels: N/A";
                if (asteroidCountText != null) asteroidCountText.text = "Asteroids: N/A";
            }
        }
    }
}


