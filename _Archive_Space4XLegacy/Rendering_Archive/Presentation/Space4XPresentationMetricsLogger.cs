using System.IO;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// MonoBehaviour that logs presentation metrics to CSV file for offline analysis.
    /// </summary>
    public class Space4XPresentationMetricsLogger : MonoBehaviour
    {
        [Header("Logging Settings")]
        [Tooltip("Enable metrics logging")]
        public bool EnableLogging = false;

        [Tooltip("Log file path (relative to project root)")]
        public string LogFilePath = "Logs/PresentationMetrics.csv";

        [Tooltip("Log interval in frames")]
        public int LogIntervalFrames = 60;

        private World _world;
        private EntityQuery _metricsQuery;
        private int _frameCount;
        private StreamWriter _logWriter;
        private bool _headerWritten;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                enabled = false;
                return;
            }

            _metricsQuery = _world.EntityManager.CreateEntityQuery(typeof(PresentationMetrics));
        }

        private void OnEnable()
        {
            if (EnableLogging && !string.IsNullOrEmpty(LogFilePath))
            {
                try
                {
                    string fullPath = Path.Combine(Application.dataPath, "..", LogFilePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    _logWriter = new StreamWriter(fullPath, append: true);
                    _headerWritten = false;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Space4XPresentationMetricsLogger] Failed to open log file: {e.Message}");
                    EnableLogging = false;
                }
            }
        }

        private void OnDisable()
        {
            if (_logWriter != null)
            {
                _logWriter.Close();
                _logWriter = null;
            }
        }

        private void Update()
        {
            if (!EnableLogging || _world == null || !_world.IsCreated)
            {
                return;
            }

            _frameCount++;

            if (_frameCount % LogIntervalFrames != 0)
            {
                return;
            }

            if (_metricsQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var metrics = _metricsQuery.GetSingleton<PresentationMetrics>();

            // Write CSV header on first write
            if (!_headerWritten)
            {
                _logWriter.WriteLine("Frame,TotalEntities,VisibleEntities,FullDetail,ReducedDetail,Impostor,Hidden,Carriers,Crafts,Asteroids,RenderDensity,FrameTimeMs,FleetImpostors,RealFleets");
                _headerWritten = true;
            }

            // Write metrics row
            _logWriter.WriteLine($"{_frameCount},{metrics.TotalPresentationEntities},{metrics.VisibleEntities}," +
                                $"{metrics.FullDetailCount},{metrics.ReducedDetailCount},{metrics.ImpostorCount},{metrics.HiddenCount}," +
                                $"{metrics.VisibleCarriers},{metrics.VisibleCrafts},{metrics.VisibleAsteroids}," +
                                $"{metrics.CurrentRenderDensity:F3},{metrics.PresentationFrameTimeMs:F2}," +
                                $"{metrics.FleetImpostorCount},{metrics.RealFleetCount}");
            _logWriter.Flush();
        }
    }
}

