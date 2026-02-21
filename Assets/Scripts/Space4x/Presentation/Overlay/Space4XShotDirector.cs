using System;
using System.IO;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Presentation.Overlay
{
    /// <summary>
    /// Minimal shot director stub. Reads beats JSON and logs beat labels as time crosses them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XShotDirector : MonoBehaviour
    {
        private const string BeatsPathEnv = "SPACE4X_BEATS_PATH";

        [Serializable]
        private sealed class BeatsDocument
        {
            public string scenarioId;
            public BeatEntry[] beats;
        }

        [Serializable]
        private sealed class BeatEntry
        {
            public float time_s;
            public string label;
        }

        private BeatEntry[] _beats = Array.Empty<BeatEntry>();
        private string _scenarioId = string.Empty;
        private int _nextBeatIndex;
        private float _lastObservedTime = -1f;

        private void Awake()
        {
            LoadFromEnvironment();
        }

        private void Update()
        {
            if (_beats.Length == 0)
            {
                return;
            }

            if (!TryGetWorldTimeSeconds(out var worldSeconds))
            {
                return;
            }

            if (_lastObservedTime >= 0f && worldSeconds < _lastObservedTime - 0.001f)
            {
                _nextBeatIndex = FindFirstBeatAfter(worldSeconds);
            }

            while (_nextBeatIndex < _beats.Length && worldSeconds >= _beats[_nextBeatIndex].time_s)
            {
                var beat = _beats[_nextBeatIndex];
                var label = string.IsNullOrWhiteSpace(beat.label) ? "(unnamed)" : beat.label;
                if (!string.IsNullOrWhiteSpace(_scenarioId))
                {
                    UnityEngine.Debug.Log($"[Space4XShotDirector] scenario='{_scenarioId}' beat='{label}' time_s={beat.time_s:0.###}");
                }
                else
                {
                    UnityEngine.Debug.Log($"[Space4XShotDirector] beat='{label}' time_s={beat.time_s:0.###}");
                }

                _nextBeatIndex++;
            }

            _lastObservedTime = worldSeconds;
        }

        private void LoadFromEnvironment()
        {
            var beatsPath = System.Environment.GetEnvironmentVariable(BeatsPathEnv);
            if (string.IsNullOrWhiteSpace(beatsPath))
            {
                return;
            }

            var resolvedPath = ResolvePath(beatsPath);
            if (!File.Exists(resolvedPath))
            {
                UnityEngine.Debug.LogWarning($"[Space4XShotDirector] beats file not found path='{resolvedPath}'");
                return;
            }

            try
            {
                var json = File.ReadAllText(resolvedPath);
                var doc = JsonUtility.FromJson<BeatsDocument>(json);
                if (doc == null || doc.beats == null || doc.beats.Length == 0)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XShotDirector] beats file empty path='{resolvedPath}'");
                    return;
                }

                _beats = doc.beats;
                Array.Sort(_beats, static (a, b) => a.time_s.CompareTo(b.time_s));
                _scenarioId = doc.scenarioId ?? string.Empty;
                _nextBeatIndex = 0;
                _lastObservedTime = -1f;
                UnityEngine.Debug.Log($"[Space4XShotDirector] beats_loaded=1 path='{resolvedPath}' count={_beats.Length}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XShotDirector] failed to parse beats path='{resolvedPath}' error='{ex.Message}'");
            }
        }

        private static bool TryGetWorldTimeSeconds(out float worldSeconds)
        {
            worldSeconds = 0f;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var entityManager = world.EntityManager;
            using var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (timeQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            worldSeconds = timeQuery.GetSingleton<TimeState>().WorldSeconds;
            return true;
        }

        private static string ResolvePath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private int FindFirstBeatAfter(float worldSeconds)
        {
            for (var i = 0; i < _beats.Length; i++)
            {
                if (_beats[i].time_s > worldSeconds)
                {
                    return i;
                }
            }

            return _beats.Length;
        }
    }
}
