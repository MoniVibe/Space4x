using PureDOTS.Runtime.Components;
using Space4X.Temporal;
using Unity.Entities;
using UnityEngine;

namespace Space4X.TimeDebug
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [DisallowMultipleComponent]
    public sealed class Space4XRewindDebug : MonoBehaviour
    {
        [Header("Hotkeys")]
        [SerializeField] private KeyCode _previewKey = KeyCode.R;
        [SerializeField] private KeyCode _commitKey = KeyCode.Space;
        [SerializeField] private KeyCode _cancelKey = KeyCode.C;
        [SerializeField] private KeyCode _cancelAltKey = KeyCode.Escape;

        [Header("Scrub")]
        [SerializeField] private float _startScrubSpeed = 1f;
        [SerializeField] private float _maxScrubSpeed = 4f;
        [SerializeField] private float _scrubAccelPerSecond = 0.75f;

        [Header("Logging")]
        [SerializeField] private bool _logStateTransitions = true;
        [SerializeField] private bool _logTickSamples = false;
        [SerializeField] private float _tickSampleIntervalSec = 1f;

        private World _world;
        private EntityQuery _rewindQuery;
        private EntityQuery _tickQuery;
        private EntityQuery _controlQuery;

        private bool _previewHeld;
        private float _holdStartRealtime;
        private float _lastSentSpeed;
        private float _lastTickSampleTime;

        private RewindMode _lastMode = (RewindMode)255;
        private RewindPhase _lastPhase = (RewindPhase)255;
        private uint _lastTick;

        private void OnEnable()
        {
            TryBindWorld();
            LogHotkeyGuide();
        }

        private void Update()
        {
            if (!TryBindWorld())
            {
                return;
            }

            HandleInput();
            LogStateTransitions();
            LogTickSample();
        }

        private bool TryBindWorld()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _world = null;
                return false;
            }

            if (_world == world)
            {
                return true;
            }

            _world = world;
            var em = _world.EntityManager;
            _rewindQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            _tickQuery = em.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>());
            _controlQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RewindControlState>());
            return true;
        }

        private void HandleInput()
        {
            if (UnityEngine.Input.GetKeyDown(_previewKey))
            {
                _previewHeld = true;
                _holdStartRealtime = UnityEngine.Time.realtimeSinceStartup;
                _lastSentSpeed = Mathf.Clamp(_startScrubSpeed, 1f, _maxScrubSpeed);
                Space4XTimeAPI.BeginRewindPreview(_lastSentSpeed);
                UnityEngine.Debug.Log($"[Space4XRewindDebug] BeginPreview key={_previewKey} speed={_lastSentSpeed:F2}");
            }

            if (_previewHeld && UnityEngine.Input.GetKey(_previewKey))
            {
                var heldFor = UnityEngine.Time.realtimeSinceStartup - _holdStartRealtime;
                var targetSpeed = Mathf.Clamp(_startScrubSpeed + (heldFor * _scrubAccelPerSecond), 1f, _maxScrubSpeed);
                if (Mathf.Abs(targetSpeed - _lastSentSpeed) >= 0.05f)
                {
                    _lastSentSpeed = targetSpeed;
                    Space4XTimeAPI.UpdateRewindPreviewSpeed(_lastSentSpeed);
                }
            }

            if (_previewHeld && UnityEngine.Input.GetKeyUp(_previewKey))
            {
                _previewHeld = false;
                Space4XTimeAPI.EndRewindScrub();
                UnityEngine.Debug.Log("[Space4XRewindDebug] EndScrubPreview (freeze at current preview)");
            }

            if (UnityEngine.Input.GetKeyDown(_commitKey))
            {
                Space4XTimeAPI.CommitRewindFromPreview();
                UnityEngine.Debug.Log("[Space4XRewindDebug] CommitRewindFromPreview");
            }

            if (UnityEngine.Input.GetKeyDown(_cancelKey) || UnityEngine.Input.GetKeyDown(_cancelAltKey))
            {
                _previewHeld = false;
                Space4XTimeAPI.CancelRewindPreview();
                UnityEngine.Debug.Log("[Space4XRewindDebug] CancelRewindPreview");
            }
        }

        private void LogStateTransitions()
        {
            if (!_logStateTransitions)
            {
                return;
            }

            if (_tickQuery.IsEmptyIgnoreFilter || _rewindQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var mode = _rewindQuery.GetSingleton<RewindState>().Mode;
            var tick = _tickQuery.GetSingleton<TickTimeState>().Tick;
            var phase = _controlQuery.IsEmptyIgnoreFilter
                ? RewindPhase.Inactive
                : _controlQuery.GetSingleton<RewindControlState>().Phase;

            if (mode != _lastMode || phase != _lastPhase)
            {
                UnityEngine.Debug.Log($"[Space4XRewindDebug] RewindState mode={mode} phase={phase} tick={tick}");
                _lastMode = mode;
                _lastPhase = phase;
                _lastTick = tick;
            }
        }

        private void LogTickSample()
        {
            if (!_logTickSamples)
            {
                return;
            }

            if (_tickQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now - _lastTickSampleTime < Mathf.Max(0.1f, _tickSampleIntervalSec))
            {
                return;
            }

            _lastTickSampleTime = now;
            var tick = _tickQuery.GetSingleton<TickTimeState>().Tick;
            if (tick != _lastTick)
            {
                UnityEngine.Debug.Log($"[Space4XRewindDebug] TickTimeState.Tick={tick}");
                _lastTick = tick;
            }
        }

        private void LogHotkeyGuide()
        {
            UnityEngine.Debug.Log($"[Space4XRewindDebug] Ready: hold {_previewKey} to scrub, release {_previewKey} to freeze preview, {_commitKey}=commit, {_cancelKey}/{_cancelAltKey}=cancel.");
        }
    }
#endif
}
