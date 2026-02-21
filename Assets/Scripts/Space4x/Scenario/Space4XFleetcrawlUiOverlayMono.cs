using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlUiOverlayMono : MonoBehaviour
    {
        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _timeQuery;
        private EntityQuery _directorQuery;
        private bool _queriesReady;

        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesReady;

        private void OnDestroy()
        {
            _queriesReady = false;
            _stylesReady = false;
        }

        private void OnGUI()
        {
            if (!TryEnsureQueries())
            {
                return;
            }

            if (!TryGetScenarioInfo(out var scenarioInfo) || !Space4XFleetcrawlUiBridge.IsFleetcrawlScenario(scenarioInfo.ScenarioId))
            {
                return;
            }

            if (_directorQuery.IsEmptyIgnoreFilter || _timeQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var directorEntity = _directorQuery.GetSingletonEntity();
            var director = _entityManager.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            if (director.CurrentRoomIndex < 0)
            {
                return;
            }

            var rooms = _entityManager.GetBuffer<Space4XFleetcrawlRoom>(directorEntity);
            if (rooms.Length == 0)
            {
                return;
            }

            var roomIndex = math.clamp(director.CurrentRoomIndex, 0, rooms.Length - 1);
            var room = rooms[roomIndex];
            var time = _entityManager.GetComponentData<TimeState>(_timeQuery.GetSingletonEntity());
            var dt = time.FixedDeltaTime > 0f ? time.FixedDeltaTime : (1f / 60f);
            var remainingSeconds = math.max(0f, (director.RoomEndTick - time.Tick) * dt);
            var gateCount = Space4XFleetcrawlUiBridge.ResolveGateCount(room.Kind);
            var perkCount = _entityManager.GetBuffer<Space4XRunPerkOp>(directorEntity).Length;
            var installed = _entityManager.GetBuffer<Space4XRunInstalledBlueprint>(directorEntity);

            EnsureStyles();
            GUILayout.BeginArea(new Rect(18f, 18f, 560f, 500f), GUIContent.none, _panelStyle);
            GUILayout.Label("FleetCrawl HUD", _headerStyle);
            GUILayout.Label($"Room {roomIndex + 1}/{rooms.Length}  kind={room.Kind}  remaining={remainingSeconds:0.0}s", _labelStyle);
            GUILayout.Label($"Tick {time.Tick}  dt={dt:0.000}  digest={director.StableDigest}", _labelStyle);
            GUILayout.Label($"Perks={perkCount}  Blueprints={FormatBlueprints(installed)}", _labelStyle);
            GUILayout.Label("Controls: WASD move, Shift boost, Q special dash, Mouse wheel zoom", _labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label($"Gate Choice ({gateCount})", _headerStyle);
            DrawGateChoiceButtons(directorEntity, director, room, gateCount);

            GUILayout.Space(8f);
            DrawBoonChoiceButtons(directorEntity, director, room, gateCount);
            GUILayout.EndArea();
        }

        private void DrawGateChoiceButtons(Entity directorEntity, in Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int gateCount)
        {
            var selectedGateOrdinal = Space4XFleetcrawlUiBridge.ResolveAutoGateOrdinal(director.Seed, director.CurrentRoomIndex, gateCount);
            if (_entityManager.HasComponent<Space4XRunPendingGatePick>(directorEntity))
            {
                var pending = _entityManager.GetComponentData<Space4XRunPendingGatePick>(directorEntity);
                if (pending.RoomIndex == director.CurrentRoomIndex && pending.GateOrdinal >= 0 && pending.GateOrdinal < gateCount)
                {
                    selectedGateOrdinal = pending.GateOrdinal;
                }
            }

            for (var gateOrdinal = 0; gateOrdinal < gateCount; gateOrdinal++)
            {
                var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, gateOrdinal);
                var selected = gateOrdinal == selectedGateOrdinal ? " [selected]" : string.Empty;
                if (!GUILayout.Button($"Gate {gateOrdinal + 1}: {gateKind}{selected}", GUILayout.Height(28f)))
                {
                    continue;
                }

                UpsertComponent(directorEntity, new Space4XRunPendingGatePick
                {
                    RoomIndex = director.CurrentRoomIndex,
                    GateOrdinal = gateOrdinal
                });
                Debug.Log($"[FleetcrawlUI] PendingGatePick room={director.CurrentRoomIndex} gate_ordinal={gateOrdinal} gate={gateKind}.");
            }
        }

        private void DrawBoonChoiceButtons(Entity directorEntity, in Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int gateCount)
        {
            var gateOrdinal = Space4XFleetcrawlUiBridge.ResolveAutoGateOrdinal(director.Seed, director.CurrentRoomIndex, gateCount);
            if (_entityManager.HasComponent<Space4XRunPendingGatePick>(directorEntity))
            {
                var pending = _entityManager.GetComponentData<Space4XRunPendingGatePick>(directorEntity);
                if (pending.RoomIndex == director.CurrentRoomIndex && pending.GateOrdinal >= 0 && pending.GateOrdinal < gateCount)
                {
                    gateOrdinal = pending.GateOrdinal;
                }
            }

            var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, gateOrdinal);
            if (gateKind != Space4XRunGateKind.Boon)
            {
                GUILayout.Label("Boon Choice: select a Boon gate to pick a boon.", _labelStyle);
                return;
            }

            GUILayout.Label("Boon Choice (3)", _headerStyle);
            var selectedOffer = Space4XFleetcrawlUiBridge.ResolveAutoOfferIndex(director.Seed, director.CurrentRoomIndex, gateKind, 3);
            if (_entityManager.HasComponent<Space4XRunPendingBoonPick>(directorEntity))
            {
                var pending = _entityManager.GetComponentData<Space4XRunPendingBoonPick>(directorEntity);
                if (pending.RoomIndex == director.CurrentRoomIndex && pending.OfferIndex >= 0 && pending.OfferIndex < 3)
                {
                    selectedOffer = pending.OfferIndex;
                }
            }

            for (var offerIndex = 0; offerIndex < 3; offerIndex++)
            {
                var perkId = Space4XFleetcrawlUiBridge.ResolveBoonOfferIdAt(director.Seed, director.CurrentRoomIndex, offerIndex);
                var selected = offerIndex == selectedOffer ? " [selected]" : string.Empty;
                if (!GUILayout.Button($"Boon {offerIndex + 1}: {perkId}{selected}", GUILayout.Height(26f)))
                {
                    continue;
                }

                UpsertComponent(directorEntity, new Space4XRunPendingBoonPick
                {
                    RoomIndex = director.CurrentRoomIndex,
                    OfferIndex = offerIndex
                });
                Debug.Log($"[FleetcrawlUI] PendingBoonPick room={director.CurrentRoomIndex} offer={offerIndex} perk={perkId}.");
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
            _timeQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            _directorQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XFleetcrawlDirectorState>());
            _queriesReady = true;
            return true;
        }

        private bool TryGetScenarioInfo(out ScenarioInfo info)
        {
            info = default;
            if (_scenarioQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            info = _scenarioQuery.GetSingleton<ScenarioInfo>();
            return true;
        }

        private void EnsureStyles()
        {
            if (_stylesReady)
            {
                return;
            }

            _stylesReady = true;
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 10, 10)
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = false
            };
        }

        private void UpsertComponent<T>(Entity entity, in T value) where T : unmanaged, IComponentData
        {
            if (_entityManager.HasComponent<T>(entity))
            {
                _entityManager.SetComponentData(entity, value);
            }
            else
            {
                _entityManager.AddComponentData(entity, value);
            }
        }

        private static string FormatBlueprints(DynamicBuffer<Space4XRunInstalledBlueprint> installed)
        {
            if (installed.Length == 0)
            {
                return "<none>";
            }

            var text = string.Empty;
            for (var i = 0; i < installed.Length; i++)
            {
                if (i > 0)
                {
                    text += ",";
                }

                text += installed[i].BlueprintId.ToString();
            }

            return text;
        }
    }
}
