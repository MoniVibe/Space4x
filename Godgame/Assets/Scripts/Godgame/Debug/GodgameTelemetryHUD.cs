using System.Text;
using Godgame.Registry;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Godgame.Debugging
{
    /// <summary>
    /// Simple runtime bridge that reads <see cref="GodgameRegistrySnapshot"/> and <see cref="TelemetryStream"/>
    /// to display the metrics on a Unity UI Canvas. Intended for validating the Godgame registry bridge.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GodgameTelemetryHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text villagerSummaryText;
        [SerializeField] private Text storehouseSummaryText;
        [SerializeField] private Text telemetrySummaryText;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _snapshotQuery;
        private EntityQuery _telemetryQuery;
        private uint _lastTelemetryVersion;
        private readonly StringBuilder _builder = new StringBuilder(256);

        private void Awake()
        {
            EnsureWorld();
        }

        private void OnEnable()
        {
            EnsureWorld();
        }

        private void OnDisable()
        {
            ResetWorld();
        }

        private void OnDestroy()
        {
            ResetWorld();
        }

        private void Update()
        {
            if (!EnsureWorld())
            {
                ClearText(villagerSummaryText);
                ClearText(storehouseSummaryText);
                ClearText(telemetrySummaryText);
                return;
            }

            UpdateSnapshotUI();
            UpdateTelemetryUI();
        }

        private bool EnsureWorld()
        {
            if (_world != null && _world.IsCreated)
            {
                return true;
            }

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                return false;
            }

            _entityManager = _world.EntityManager;
            DisposeQueries();
            _snapshotQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GodgameRegistrySnapshot>());
            _telemetryQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            _lastTelemetryVersion = 0;
            return true;
        }

        private void ResetWorld()
        {
            DisposeQueries();
            _entityManager = default;
            _world = null;
        }

        private void DisposeQueries()
        {
            if (_snapshotQuery != default)
            {
                _snapshotQuery.Dispose();
            }

            if (_telemetryQuery != default)
            {
                _telemetryQuery.Dispose();
            }

            _snapshotQuery = default;
            _telemetryQuery = default;
        }

        private void UpdateSnapshotUI()
        {
            if (_snapshotQuery == default || _snapshotQuery.IsEmptyIgnoreFilter)
            {
                ClearText(villagerSummaryText);
                ClearText(storehouseSummaryText);
                return;
            }

            var snapshot = _snapshotQuery.GetSingleton<GodgameRegistrySnapshot>();

            if (villagerSummaryText != null)
            {
                _builder.Clear();
                _builder.Append("Villagers  ");
                _builder.Append(snapshot.VillagerCount);
                _builder.Append(" (Avail:");
                _builder.Append(snapshot.AvailableVillagers);
                _builder.Append(" Idle:");
                _builder.Append(snapshot.IdleVillagers);
                _builder.Append(" Combat:");
                _builder.Append(snapshot.CombatReadyVillagers);
                _builder.Append(")  HP:");
                _builder.Append(snapshot.AverageVillagerHealth.ToString("0.0"));
                _builder.Append(" Morale:");
                _builder.Append(snapshot.AverageVillagerMorale.ToString("0.0"));
                _builder.Append(" Energy:");
                _builder.Append(snapshot.AverageVillagerEnergy.ToString("0.0"));
                villagerSummaryText.text = _builder.ToString();
            }

            if (storehouseSummaryText != null)
            {
                _builder.Clear();
                _builder.Append("Storehouses  ");
                _builder.Append(snapshot.StorehouseCount);
                _builder.Append("  Capacity:");
                _builder.Append(snapshot.TotalStorehouseCapacity.ToString("0"));
                _builder.Append("  Stored:");
                _builder.Append(snapshot.TotalStorehouseStored.ToString("0"));
                _builder.Append("  Reserved:");
                _builder.Append(snapshot.TotalStorehouseReserved.ToString("0"));
                storehouseSummaryText.text = _builder.ToString();
            }
        }

        private void UpdateTelemetryUI()
        {
            if (telemetrySummaryText == null || _telemetryQuery == default || _telemetryQuery.IsEmptyIgnoreFilter)
            {
                ClearText(telemetrySummaryText);
                return;
            }

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var stream = _entityManager.GetComponentData<TelemetryStream>(telemetryEntity);
            if (stream.Version == _lastTelemetryVersion)
            {
                return;
            }

            _lastTelemetryVersion = stream.Version;
            var buffer = _entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            if (buffer.Length == 0)
            {
                ClearText(telemetrySummaryText);
                return;
            }

            _builder.Clear();
            for (int i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                _builder.Append(metric.Key.ToString());
                _builder.Append(':');
                _builder.Append(metric.Value.ToString("0.##"));
                if (i < buffer.Length - 1)
                {
                    _builder.Append("  |  ");
                }
            }

            telemetrySummaryText.text = _builder.ToString();
        }

        private static void ClearText(Text text)
        {
            if (text != null)
            {
                text.text = string.Empty;
            }
        }
    }
}
