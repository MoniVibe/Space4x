using System;
using TMPro;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;

namespace Space4X.Demo
{
    /// <summary>
    /// Simple HUD binder that surfaces TelemetrySnapshot metrics and demo controls (Input System).
    /// </summary>
    public class Space4XDemoHud : MonoBehaviour
    {
        [Header("Telemetry Labels")]
        [SerializeField] private TextMeshProUGUI damageLabel;
        [SerializeField] private TextMeshProUGUI hitsLabel;
        [SerializeField] private TextMeshProUGUI critLabel;
        [SerializeField] private TextMeshProUGUI modulesLabel;
        [SerializeField] private TextMeshProUGUI miningLabel;
        [SerializeField] private TextMeshProUGUI sanctionsLabel;
        [SerializeField] private TextMeshProUGUI tickLabel;
        [SerializeField] private TextMeshProUGUI snapshotLabel;

        [Header("Hotkeys (Input System)")]
        [SerializeField] private Key pauseKey = Key.P;
        [SerializeField] private Key stepKey = Key.Period;
        [SerializeField] private Key speedUpKey = Key.Equals;
        [SerializeField] private Key speedDownKey = Key.Minus;
        [SerializeField] private Key rewindKey = Key.R;
        [SerializeField] private Key bindingsSwapKey = Key.B;
        [SerializeField] private Key veteranToggleKey = Key.V;
        [SerializeField] private float speedStep = 0.5f;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _telemetryQuery;
        private EntityQuery _rewindQuery;
        private EntityQuery _demoOptionsQuery;
        private EntityQuery _demoStateQuery;
        private EntityQuery _timeStateQuery;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world != null && _world.IsCreated)
            {
                _entityManager = _world.EntityManager;
                _telemetryQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetrySnapshot>());
                _rewindQuery = _entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<RewindState>(),
                    ComponentType.ReadWrite<TimeControlCommand>());
                _demoOptionsQuery = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<DemoOptions>());
                _demoStateQuery = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<DemoBootstrapState>());
                _timeStateQuery = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimeState>());
            }
        }

        private void Update()
        {
            if (_world == null || !_world.IsCreated)
                return;

            UpdateTelemetryUi();
            HandleHotkeys();
        }

        private void OnDestroy()
        {
            if (_world != null && _world.IsCreated)
            {
                if (_telemetryQuery != default) _telemetryQuery.Dispose();
                if (_rewindQuery != default) _rewindQuery.Dispose();
                if (_demoOptionsQuery != default) _demoOptionsQuery.Dispose();
                if (_demoStateQuery != default) _demoStateQuery.Dispose();
                if (_timeStateQuery != default) _timeStateQuery.Dispose();
            }
        }

        private void UpdateTelemetryUi()
        {
            if (_telemetryQuery == default || _telemetryQuery.IsEmptyIgnoreFilter)
                return;

            var snapshot = _telemetryQuery.GetSingleton<TelemetrySnapshot>();

            if (damageLabel) damageLabel.text = $"DMG {snapshot.DamageTotal:F1}";
            if (hitsLabel) hitsLabel.text = $"HIT {snapshot.Hits}";
            if (critLabel) critLabel.text = $"CRIT {snapshot.CritPercent:F1}%";
            if (modulesLabel) modulesLabel.text = $"MODX {snapshot.ModulesDestroyed}";
            if (miningLabel) miningLabel.text = $"ORE {snapshot.MiningThroughput:F1}";
            if (sanctionsLabel) sanctionsLabel.text = $"SAN {snapshot.Sanctions:F1}";
            if (tickLabel) tickLabel.text = $"TICK {snapshot.FixedTickMs:F2} ms";
            if (snapshotLabel) snapshotLabel.text = $"BUF {snapshot.SnapshotKilobytes:F1} KB v{snapshot.Version}";
        }

        private void HandleHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (WasPressed(keyboard, pauseKey))
            {
                TogglePause();
            }

            if (WasPressed(keyboard, stepKey))
            {
                StepOneTick();
            }

            if (WasPressed(keyboard, speedUpKey))
            {
                AdjustSpeed(speedStep);
            }

            if (WasPressed(keyboard, speedDownKey))
            {
                AdjustSpeed(-speedStep);
            }

            if (WasPressed(keyboard, rewindKey))
            {
                ToggleRewind();
            }

            if (WasPressed(keyboard, bindingsSwapKey))
            {
                SwapBindings();
            }

            if (WasPressed(keyboard, veteranToggleKey))
            {
                ToggleVeteran();
            }
        }

        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            return Enum.IsDefined(typeof(Key), key) && keyboard[key] != null && keyboard[key].wasPressedThisFrame;
        }

        private void TogglePause()
        {
            byte paused = 0;
            if (_demoStateQuery != default && !_demoStateQuery.IsEmptyIgnoreFilter)
            {
                var entity = _demoStateQuery.GetSingletonEntity();
                var demo = _entityManager.GetComponentData<DemoBootstrapState>(entity);
                demo.Paused = (byte)(demo.Paused == 1 ? 0 : 1);
                paused = demo.Paused;
                _entityManager.SetComponentData(entity, demo);
            }

            if (_timeStateQuery != default && !_timeStateQuery.IsEmptyIgnoreFilter)
            {
                var timeEntity = _timeStateQuery.GetSingletonEntity();
                var timeState = _entityManager.GetComponentData<TimeState>(timeEntity);
                timeState.IsPaused = paused == 1;
                _entityManager.SetComponentData(timeEntity, timeState);
            }
        }

        private void StepOneTick()
        {
            if (_rewindQuery != default && !_rewindQuery.IsEmptyIgnoreFilter)
            {
                var rewindEntity = _rewindQuery.GetSingletonEntity();
                if (_entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
                {
                    var buffer = _entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
                    buffer.Add(new TimeControlCommand
                    {
                        Type = TimeControlCommand.CommandType.StepTicks,
                        UintParam = 1
                    });
                }
            }
        }

        private void AdjustSpeed(float delta)
        {
            float newScale = 1f;

            if (_demoStateQuery != default && !_demoStateQuery.IsEmptyIgnoreFilter)
            {
                var entity = _demoStateQuery.GetSingletonEntity();
                var demo = _entityManager.GetComponentData<DemoBootstrapState>(entity);
                demo.TimeScale = math.max(0.1f, demo.TimeScale + delta);
                newScale = demo.TimeScale;
                _entityManager.SetComponentData(entity, demo);
            }

            if (_rewindQuery != default && !_rewindQuery.IsEmptyIgnoreFilter)
            {
                var rewindEntity = _rewindQuery.GetSingletonEntity();
                if (_entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
                {
                    var buffer = _entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
                    buffer.Add(new TimeControlCommand
                    {
                        Type = TimeControlCommand.CommandType.SetSpeed,
                        FloatParam = newScale
                    });
                }
            }
        }

        private void ToggleRewind()
        {
            byte rewindEnabled = 0;

            if (_demoStateQuery != default && !_demoStateQuery.IsEmptyIgnoreFilter)
            {
                var entity = _demoStateQuery.GetSingletonEntity();
                var demo = _entityManager.GetComponentData<DemoBootstrapState>(entity);
                demo.RewindEnabled = (byte)(demo.RewindEnabled == 1 ? 0 : 1);
                rewindEnabled = demo.RewindEnabled;
                _entityManager.SetComponentData(entity, demo);
            }

            if (_rewindQuery != default && !_rewindQuery.IsEmptyIgnoreFilter)
            {
                var rewindEntity = _rewindQuery.GetSingletonEntity();
                if (_entityManager.HasBuffer<TimeControlCommand>(rewindEntity))
                {
                    var buffer = _entityManager.GetBuffer<TimeControlCommand>(rewindEntity);
                    buffer.Add(new TimeControlCommand
                    {
                        Type = rewindEnabled == 1
                            ? TimeControlCommand.CommandType.StartRewind
                            : TimeControlCommand.CommandType.StopRewind
                    });
                }
            }
        }

        private void SwapBindings()
        {
            if (_demoOptionsQuery != default && !_demoOptionsQuery.IsEmptyIgnoreFilter)
            {
                var entity = _demoOptionsQuery.GetSingletonEntity();
                var options = _entityManager.GetComponentData<DemoOptions>(entity);
                options.BindingsSet = (byte)(options.BindingsSet == 1 ? 0 : 1);
                _entityManager.SetComponentData(entity, options);
            }
        }

        private void ToggleVeteran()
        {
            if (_demoOptionsQuery != default && !_demoOptionsQuery.IsEmptyIgnoreFilter)
            {
                var entity = _demoOptionsQuery.GetSingletonEntity();
                var options = _entityManager.GetComponentData<DemoOptions>(entity);
                options.Veteran = (byte)(options.Veteran == 1 ? 0 : 1);
                _entityManager.SetComponentData(entity, options);
            }
        }
    }
}
