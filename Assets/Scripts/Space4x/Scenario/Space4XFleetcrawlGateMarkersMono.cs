using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlGateMarkersMono : MonoBehaviour
    {
        private sealed class MarkerView
        {
            public GameObject Root;
            public Renderer Renderer;
            public Space4XRunGateKind Kind;
            public int Ordinal;
            public float3 Position;
        }

        [SerializeField] private float forwardDistance = 46f;
        [SerializeField] private float lateralSpread = 20f;
        [SerializeField] private float markerHeight = 2.2f;
        [SerializeField] private float proximityRadius = 10f;
        [SerializeField] private float markerLerp = 10f;
        [SerializeField] private float anchorFollowSpeed = 0.75f;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _directorQuery;
        private EntityQuery _flagshipQuery;
        private bool _queriesReady;

        private readonly MarkerView[] _markers = new MarkerView[3];
        private int _activeRoomIndex = -1;
        private int _activeGateCount;
        private float3 _anchorPosition;
        private float3 _anchorForward = new float3(0f, 0f, 1f);
        private int _lastLoggedPickRoom = -1;
        private int _lastLoggedPickOrdinal = -1;
        private bool _loggedReady;

        private void OnDisable()
        {
            SetMarkersVisible(false);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _markers.Length; i++)
            {
                if (_markers[i]?.Root != null)
                {
                    Destroy(_markers[i].Root);
                }
                _markers[i] = null;
            }
        }

        private void Update()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                SetMarkersVisible(false);
                return;
            }

            if (!TryEnsureQueries())
            {
                return;
            }

            if (_scenarioQuery.IsEmptyIgnoreFilter || _directorQuery.IsEmptyIgnoreFilter || _flagshipQuery.IsEmptyIgnoreFilter)
            {
                SetMarkersVisible(false);
                return;
            }

            var scenario = _scenarioQuery.GetSingleton<ScenarioInfo>();
            if (!Space4XFleetcrawlUiBridge.IsFleetcrawlScenario(scenario.ScenarioId))
            {
                SetMarkersVisible(false);
                return;
            }

            var directorEntity = _directorQuery.GetSingletonEntity();
            var director = _entityManager.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            if (director.RunCompleted != 0 || director.CurrentRoomIndex < 0)
            {
                SetMarkersVisible(false);
                return;
            }

            var rooms = _entityManager.GetBuffer<Space4XFleetcrawlRoom>(directorEntity);
            if (rooms.Length == 0)
            {
                SetMarkersVisible(false);
                return;
            }

            var roomIndex = math.clamp(director.CurrentRoomIndex, 0, rooms.Length - 1);
            var room = rooms[roomIndex];
            var flagshipEntity = _flagshipQuery.GetSingletonEntity();
            var flagshipTransform = _entityManager.GetComponentData<LocalTransform>(flagshipEntity);
            var flagshipPosition = flagshipTransform.Position;
            var flagshipForward = math.normalizesafe(math.mul(flagshipTransform.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));

            if (roomIndex != _activeRoomIndex)
            {
                _activeRoomIndex = roomIndex;
                _activeGateCount = Space4XFleetcrawlUiBridge.ResolveGateCount(room.Kind);
                _anchorPosition = flagshipPosition;
                _anchorForward = flagshipForward;
                _lastLoggedPickRoom = -1;
                _lastLoggedPickOrdinal = -1;
            }
            else
            {
                _anchorPosition = math.lerp(_anchorPosition, flagshipPosition, math.saturate(anchorFollowSpeed * Time.deltaTime));
                _anchorForward = math.normalizesafe(math.lerp(_anchorForward, flagshipForward, math.saturate(0.85f * Time.deltaTime)), _anchorForward);
            }

            EnsureMarkersCreated();
            UpdateMarkerTransforms(room, director.CurrentRoomIndex);
            ResolveProximityPick(directorEntity, director.CurrentRoomIndex, room);

            if (!_loggedReady)
            {
                _loggedReady = true;
                Debug.Log("[FleetcrawlGate] ready=1 source=proximity_markers");
            }
        }

        private void UpdateMarkerTransforms(in Space4XFleetcrawlRoom room, int roomIndex)
        {
            var right = math.normalizesafe(new float3(_anchorForward.z, 0f, -_anchorForward.x), new float3(1f, 0f, 0f));
            var anchor = _anchorPosition + _anchorForward * forwardDistance + new float3(0f, markerHeight, 0f);
            for (var ordinal = 0; ordinal < _markers.Length; ordinal++)
            {
                var marker = _markers[ordinal];
                var active = ordinal < _activeGateCount;
                marker.Root.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var lane = ComputeLaneOffset(ordinal, _activeGateCount);
                var kind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, ordinal);
                var kindChanged = marker.Kind != kind;
                marker.Kind = kind;
                marker.Ordinal = ordinal;
                marker.Root.name = $"GateMarker_{ordinal}_{kind}_R{roomIndex}";

                var targetPos = anchor + right * (lane * lateralSpread);
                marker.Root.transform.position = Vector3.Lerp(marker.Root.transform.position, targetPos, Mathf.Clamp01(markerLerp * Time.deltaTime));
                marker.Root.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
                marker.Position = marker.Root.transform.position;
                if (kindChanged)
                {
                    ApplyMarkerColor(marker.Renderer, kind);
                }
            }
        }

        private void ResolveProximityPick(Entity directorEntity, int roomIndex, in Space4XFleetcrawlRoom room)
        {
            if (_flagshipQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var flagship = _entityManager.GetComponentData<LocalTransform>(_flagshipQuery.GetSingletonEntity()).Position;
            var nearestOrdinal = -1;
            var nearestDistanceSq = float.MaxValue;
            var radiusSq = proximityRadius * proximityRadius;

            for (var ordinal = 0; ordinal < _activeGateCount; ordinal++)
            {
                var marker = _markers[ordinal];
                if (!marker.Root.activeSelf)
                {
                    continue;
                }

                var distanceSq = math.lengthsq(flagship - marker.Position);
                if (distanceSq > radiusSq || distanceSq >= nearestDistanceSq)
                {
                    continue;
                }

                nearestDistanceSq = distanceSq;
                nearestOrdinal = ordinal;
            }

            if (nearestOrdinal < 0)
            {
                return;
            }

            var hasPending = _entityManager.HasComponent<Space4XRunPendingGatePick>(directorEntity);
            var previous = hasPending
                ? _entityManager.GetComponentData<Space4XRunPendingGatePick>(directorEntity)
                : default;

            var targetPick = new Space4XRunPendingGatePick
            {
                RoomIndex = roomIndex,
                GateOrdinal = nearestOrdinal
            };

            var changed = !hasPending ||
                          previous.RoomIndex != targetPick.RoomIndex ||
                          previous.GateOrdinal != targetPick.GateOrdinal;
            if (!changed)
            {
                return;
            }

            if (hasPending)
            {
                _entityManager.SetComponentData(directorEntity, targetPick);
            }
            else
            {
                _entityManager.AddComponentData(directorEntity, targetPick);
            }

            if (_lastLoggedPickRoom != roomIndex || _lastLoggedPickOrdinal != nearestOrdinal)
            {
                _lastLoggedPickRoom = roomIndex;
                _lastLoggedPickOrdinal = nearestOrdinal;
                var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, nearestOrdinal);
                Debug.Log($"[FleetcrawlGate] PICK room={roomIndex} gate_ordinal={nearestOrdinal} gate={gateKind} via=proximity");
            }
        }

        private static float ComputeLaneOffset(int ordinal, int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            if (count == 2)
            {
                return ordinal == 0 ? -0.55f : 0.55f;
            }

            return ordinal switch
            {
                0 => -1f,
                1 => 0f,
                _ => 1f
            };
        }

        private void EnsureMarkersCreated()
        {
            for (var i = 0; i < _markers.Length; i++)
            {
                if (_markers[i] != null && _markers[i].Root != null)
                {
                    continue;
                }

                var root = new GameObject($"GateMarker_{i}");
                root.transform.SetParent(transform, worldPositionStays: false);
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "Body";
                body.transform.SetParent(root.transform, worldPositionStays: false);
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = new Vector3(0.52f, 0.32f, 0.52f);
                var collider = body.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Cap";
                cap.transform.SetParent(root.transform, worldPositionStays: false);
                cap.transform.localPosition = new Vector3(0f, 0.62f, 0f);
                cap.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
                var capCollider = cap.GetComponent<Collider>();
                if (capCollider != null)
                {
                    Destroy(capCollider);
                }

                var renderer = body.GetComponent<Renderer>();
                _markers[i] = new MarkerView
                {
                    Root = root,
                    Renderer = renderer,
                    Ordinal = i,
                    Kind = (Space4XRunGateKind)255
                };
            }
        }

        private static void ApplyMarkerColor(Renderer renderer, Space4XRunGateKind gateKind)
        {
            if (renderer == null)
            {
                return;
            }

            var color = gateKind switch
            {
                Space4XRunGateKind.Boon => new Color(0.14f, 0.82f, 1f, 1f),
                Space4XRunGateKind.Blueprint => new Color(1f, 0.63f, 0.13f, 1f),
                _ => new Color(0.2f, 0.95f, 0.45f, 1f)
            };

            if (renderer.sharedMaterial != null)
            {
                renderer.material.color = color;
            }
        }

        private void SetMarkersVisible(bool visible)
        {
            for (var i = 0; i < _markers.Length; i++)
            {
                var marker = _markers[i];
                if (marker?.Root != null)
                {
                    marker.Root.SetActive(visible);
                }
            }
        }

        private bool TryEnsureQueries()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (_queriesReady && world == _world)
            {
                return true;
            }

            _world = world;
            _entityManager = world.EntityManager;
            _scenarioQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>());
            _directorQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XFleetcrawlDirectorState>(),
                ComponentType.ReadOnly<Space4XFleetcrawlRoom>());
            _flagshipQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerFlagshipTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _queriesReady = true;
            return true;
        }
    }
}
