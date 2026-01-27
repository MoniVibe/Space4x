using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Minimal runtime overlay that surfaces the current rewind mode and playback tick.
    /// Attach to any GameObject inside validation scenes to visualise record/catch-up/playback transitions.
    /// </summary>
    public sealed class RewindTimelineDebug : MonoBehaviour
    {
        [Header("Visual Settings")]
        [Tooltip("Screen position in pixels for the overlay window.")]
        public Vector2 windowPosition = new Vector2(20f, 20f);

        [Tooltip("Overlay size in pixels.")]
        public Vector2 windowSize = new Vector2(260f, 80f);

        [Tooltip("Optional toggle to hide the overlay without removing the component.")]
        public bool overlayVisible = true;

        private World _world;
        private EntityQuery _rewindQuery;
        private EntityQuery _timeQuery;
        private EntityQuery _timeContextQuery;
        private EntityQuery _timelineQuery;
        private FixedString64Bytes _modeString;
        private uint _playbackTick;
        private uint _recordTick;
        private bool _caughtUp;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogWarning($"{nameof(RewindTimelineDebug)} could not locate the default world; overlay disabled.", this);
                enabled = false;
                return;
            }

            var entityManager = _world.EntityManager;
            _rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            _timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            _timeContextQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeContext>());
            _timelineQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DebugDisplayData>());
        }

        private void Update()
        {
            if (!overlayVisible || _world == null || _world.EntityManager == null)
            {
                return;
            }

            var entityManager = _world.EntityManager;

            _modeString = default;
            _playbackTick = 0;
            _recordTick = 0;
            _caughtUp = false;

            if (_rewindQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var rewindState = _rewindQuery.GetSingleton<RewindState>();
            uint viewTick = 0;
            if (!_timeContextQuery.IsEmptyIgnoreFilter)
            {
                viewTick = _timeContextQuery.GetSingleton<TimeContext>().ViewTick;
            }
            else if (!_timeQuery.IsEmptyIgnoreFilter)
            {
                viewTick = _timeQuery.GetSingleton<TimeState>().Tick;
            }
            switch (rewindState.Mode)
            {
                case RewindMode.Play:
                    _modeString.Append("Play");
                    break;
                case RewindMode.Paused:
                    _modeString.Append("Paused");
                    break;
                case RewindMode.Rewind:
                    _modeString.Append("Rewind");
                    _playbackTick = viewTick;
                    break;
                case RewindMode.Step:
                    _modeString.Append("Step");
                    break;
                default:
                    _modeString.Append("Unknown");
                    break;
            }

            if (!_timeQuery.IsEmptyIgnoreFilter)
            {
                _recordTick = _timeQuery.GetSingleton<TimeState>().Tick;
            }
        }

        private void OnGUI()
        {
            if (!overlayVisible)
            {
                return;
            }

            var rect = new Rect(windowPosition, windowSize);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("<b>Rewind Timeline</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Space(5f);

            GUILayout.Label($"Mode: {_modeString}");
            GUILayout.Label($"Record Tick: {_recordTick}");
            if (_caughtUp)
            {
                GUILayout.Label($"CatchUp Target: {_playbackTick}");
            }
            else if (_playbackTick != 0)
            {
                GUILayout.Label($"Playback Tick: {_playbackTick}");
            }
            else
            {
                GUILayout.Label("Playback Tick: --");
            }

            if (!_timelineQuery.IsEmptyIgnoreFilter)
            {
                var debugData = _timelineQuery.GetSingleton<DebugDisplayData>();
                GUILayout.Space(6f);
                GUILayout.Label(debugData.RewindStateText.ToString());
            }

            GUILayout.EndArea();
        }
    }
}
