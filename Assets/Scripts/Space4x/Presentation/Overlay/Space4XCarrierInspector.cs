using PureDOTS.Runtime.Platform;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Presentation.Overlay
{
    /// <summary>
    /// Read-only carrier summary panel for selected carrier entities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCarrierInspector : MonoBehaviour
    {
        [SerializeField] private bool _showInspector = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.I;
        [SerializeField] private float _refreshRateHz = 4f;
        [SerializeField] private Rect _panelRect = new Rect(12f, 286f, 520f, 230f);
        [SerializeField] private int _fontSize = 13;

        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private float _nextRefreshAt;
        private InspectorSnapshot _snapshot;

        private struct InspectorSnapshot
        {
            public bool HasSelection;
            public string SelectedEntity;
            public string CarrierId;
            public string FleetId;
            public string FleetPosture;
            public string Side;
            public bool IsHostile;
            public int CrewCount;
            public int ModuleCount;
            public int DockedCount;
            public int NearbyMinerCount;
            public int NearbyStrikeCraftCount;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showInspector = !_showInspector;
            }

            if (!_showInspector)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextRefreshAt)
            {
                return;
            }

            _nextRefreshAt = now + (1f / Mathf.Max(1f, _refreshRateHz));
            RefreshSnapshot();
        }

        private void OnGUI()
        {
            if (!_showInspector)
            {
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(_panelRect, GUIContent.none, _panelStyle);
            GUILayout.Label("Carrier Inspector", _labelStyle);
            GUILayout.Space(4f);

            if (!_snapshot.HasSelection)
            {
                GUILayout.Label("Selection: none (click a carrier)", _labelStyle);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Entity: {_snapshot.SelectedEntity}", _labelStyle);
            GUILayout.Label($"CarrierId: {_snapshot.CarrierId}", _labelStyle);
            GUILayout.Label($"Fleet: {_snapshot.FleetId}  posture={_snapshot.FleetPosture}", _labelStyle);
            GUILayout.Label($"Side: {_snapshot.Side}  hostile={_snapshot.IsHostile}", _labelStyle);
            GUILayout.Label($"Hierarchy: crew={_snapshot.CrewCount} modules={_snapshot.ModuleCount} docked={_snapshot.DockedCount}", _labelStyle);
            GUILayout.Label($"Children: miners={_snapshot.NearbyMinerCount} strike_craft={_snapshot.NearbyStrikeCraftCount}", _labelStyle);
            GUILayout.EndArea();
        }

        private void RefreshSnapshot()
        {
            var snapshot = new InspectorSnapshot
            {
                HasSelection = false,
                SelectedEntity = "none",
                CarrierId = "n/a",
                FleetId = "n/a",
                FleetPosture = "n/a",
                Side = "n/a",
                IsHostile = false,
                CrewCount = 0,
                ModuleCount = 0,
                DockedCount = 0,
                NearbyMinerCount = 0,
                NearbyStrikeCraftCount = 0
            };

            if (!Space4XEntityPicker.TryGetSelectedCarrier(out var selectedCarrier))
            {
                _snapshot = snapshot;
                return;
            }

            if (!TryGetEntityManager(out var entityManager) || !entityManager.Exists(selectedCarrier))
            {
                Space4XEntityPicker.ClearSelection();
                _snapshot = snapshot;
                return;
            }

            snapshot.HasSelection = true;
            snapshot.SelectedEntity = $"{selectedCarrier.Index}:{selectedCarrier.Version}";

            if (entityManager.HasComponent<Carrier>(selectedCarrier))
            {
                snapshot.CarrierId = entityManager.GetComponentData<Carrier>(selectedCarrier).CarrierId.ToString();
            }

            if (entityManager.HasComponent<Space4XFleet>(selectedCarrier))
            {
                var fleet = entityManager.GetComponentData<Space4XFleet>(selectedCarrier);
                snapshot.FleetId = fleet.FleetId.ToString();
                snapshot.FleetPosture = fleet.Posture.ToString();
            }

            if (entityManager.HasComponent<ScenarioSide>(selectedCarrier))
            {
                var side = entityManager.GetComponentData<ScenarioSide>(selectedCarrier).Side;
                snapshot.Side = side.ToString();
            }

            if (entityManager.HasComponent<EntityDisposition>(selectedCarrier))
            {
                var flags = entityManager.GetComponentData<EntityDisposition>(selectedCarrier).Flags;
                snapshot.IsHostile = (flags & EntityDispositionFlags.Hostile) != 0;
            }

            if (entityManager.HasBuffer<PlatformCrewMember>(selectedCarrier))
            {
                snapshot.CrewCount = entityManager.GetBuffer<PlatformCrewMember>(selectedCarrier).Length;
            }

            if (entityManager.HasBuffer<CarrierModuleSlot>(selectedCarrier))
            {
                snapshot.ModuleCount = entityManager.GetBuffer<CarrierModuleSlot>(selectedCarrier).Length;
            }

            if (entityManager.HasBuffer<DockedEntity>(selectedCarrier))
            {
                snapshot.DockedCount = entityManager.GetBuffer<DockedEntity>(selectedCarrier).Length;
            }

            snapshot.NearbyMinerCount = CountMinersForCarrier(entityManager, selectedCarrier);
            snapshot.NearbyStrikeCraftCount = CountStrikeCraftForCarrier(entityManager, selectedCarrier);

            _snapshot = snapshot;
        }

        private static int CountMinersForCarrier(EntityManager entityManager, Entity carrierEntity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MiningVessel>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var count = 0;
            using var miners = query.ToComponentDataArray<MiningVessel>(Allocator.Temp);
            for (var i = 0; i < miners.Length; i++)
            {
                if (miners[i].CarrierEntity == carrierEntity)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountStrikeCraftForCarrier(EntityManager entityManager, Entity carrierEntity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StrikeCraftProfile>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var count = 0;
            using var strikeCraft = query.ToComponentDataArray<StrikeCraftProfile>(Allocator.Temp);
            for (var i = 0; i < strikeCraft.Length; i++)
            {
                if (strikeCraft[i].Carrier == carrierEntity)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryGetEntityManager(out EntityManager entityManager)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            return true;
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null && _labelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = _fontSize,
                padding = new RectOffset(10, 10, 8, 8)
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize
            };
            _labelStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 1f);
        }
    }
}
