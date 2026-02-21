using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
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
        private EntityQuery _enemyTelegraphQuery;
        private bool _queriesReady;

        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _mutedLabelStyle;
        private bool _stylesReady;

        private bool _introInitialized;
        private float _introUntilRealtime;
        private int _lastOfferSummaryRoom = int.MinValue;
        private uint _lastOfferSummarySeed;

        private void OnDestroy()
        {
            _queriesReady = false;
            _stylesReady = false;
            _introInitialized = false;
            _introUntilRealtime = 0f;
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

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                _introUntilRealtime = 0f;
            }

            var roomIndex = math.clamp(director.CurrentRoomIndex, 0, rooms.Length - 1);
            var room = rooms[roomIndex];
            var time = _entityManager.GetComponentData<TimeState>(_timeQuery.GetSingletonEntity());
            var dt = time.FixedDeltaTime > 0f ? time.FixedDeltaTime : (1f / 60f);
            var remainingSeconds = math.max(0f, (director.RoomEndTick - time.Tick) * dt);
            var gateCount = Space4XFleetcrawlUiBridge.ResolveGateCount(room.Kind);
            var perkOps = _entityManager.GetBuffer<Space4XRunPerkOp>(directorEntity);
            var installed = _entityManager.GetBuffer<Space4XRunInstalledBlueprint>(directorEntity);
            EmitOfferSummaryLogs(director, room, gateCount, directorEntity);

            if (!_introInitialized && director.Initialized != 0)
            {
                _introInitialized = true;
                _introUntilRealtime = Time.realtimeSinceStartup + 9f;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(18f, 18f, 700f, 640f), GUIContent.none, _panelStyle);
            GUILayout.Label("FleetCrawl HUD", _headerStyle);
            GUILayout.Label($"Scenario: {scenarioInfo.ScenarioId}", _labelStyle);
            GUILayout.Label($"Room {roomIndex + 1}/{rooms.Length}  kind={room.Kind}  remaining={remainingSeconds:0.0}s", _labelStyle);
            GUILayout.Label($"Tick {time.Tick}  dt={dt:0.000}  digest={director.StableDigest}", _labelStyle);
            GUILayout.Label($"Status: {director.DifficultyStatus}  kills={director.EnemiesDestroyedInRoom}/{math.max(0, director.EnemiesSpawnedInRoom)}  mini={director.MiniBossesDestroyedInRoom}  boss={director.BossesDestroyedInRoom}", _labelStyle);
            GUILayout.Label(BuildRoomObjectiveText(room, director, remainingSeconds), _labelStyle);
            GUILayout.Label(BuildTelegraphSummary(), _labelStyle);

            var status = Space4XFleetcrawlPlayerControlMono.CurrentStatus;
            GUILayout.Label(
                $"Pilot: boost={status.Boost01 * 100f:0}%  dash_cd={status.DashCooldown:0.00}s  speed={status.Speed:0.0}  special={status.SpecialEnergyCurrent:0.#}/{status.SpecialEnergyMax:0.#} ({status.SpecialEnergy01 * 100f:0}%)",
                _labelStyle);
            GUILayout.Label("Controls: WASD move, Space/Ctrl vertical, Shift boost, Q dash, E special, F snap camera, Mouse wheel zoom", _labelStyle);

            GUILayout.Space(6f);
            GUILayout.Label($"Perks ({perkOps.Length}): {FormatPerkList(perkOps)}", _labelStyle);
            GUILayout.Label($"Weapon BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Weapon)}", _labelStyle);
            GUILayout.Label($"Reactor BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Reactor)}", _labelStyle);
            GUILayout.Label($"Hangar BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Hangar)}", _labelStyle);

            GUILayout.Space(10f);
            GUILayout.Label($"Gate Choice ({gateCount})", _headerStyle);
            DrawGateChoiceButtons(directorEntity, director, room, gateCount);

            GUILayout.Space(10f);
            DrawBoonChoiceButtons(directorEntity, director, room, gateCount);
            GUILayout.EndArea();

            if (_introUntilRealtime > Time.realtimeSinceStartup && director.RunCompleted == 0)
            {
                DrawRunStartPanel(installed, perkOps);
            }

            if (director.RunCompleted != 0)
            {
                DrawRunEndPanel(directorEntity, director, rooms);
            }
        }

        private void DrawGateChoiceButtons(Entity directorEntity, in Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int gateCount)
        {
            var selectedGateOrdinal = ResolveSelectedGateOrdinal(directorEntity, director, gateCount);
            var selectedBoonOffer = ResolveSelectedBoonOffer(directorEntity, director);

            for (var gateOrdinal = 0; gateOrdinal < gateCount; gateOrdinal++)
            {
                var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, gateOrdinal);
                var selected = gateOrdinal == selectedGateOrdinal ? " [selected]" : string.Empty;
                var offerIndex = gateKind == Space4XRunGateKind.Boon ? selectedBoonOffer : Space4XFleetcrawlUiBridge.ResolveAutoOfferIndex(director.Seed, director.CurrentRoomIndex, gateKind, 3);
                var picked = Space4XFleetcrawlUiBridge.ResolvePickedOffer(director.Seed, director.CurrentRoomIndex, gateKind, offerIndex);
                Space4XFleetcrawlUiBridge.ResolveGateOffers(director.Seed, director.CurrentRoomIndex, gateKind, out var offerA, out var offerB, out var offerC);

                var label = $"Gate {gateOrdinal + 1} [{gateKind}]{selected}\n" +
                            $"{Space4XFleetcrawlUiBridge.DescribeOffer(picked)}\n" +
                            $"Offers: {offerA.RewardId} | {offerB.RewardId} | {offerC.RewardId}";
                if (!GUILayout.Button(label, GUILayout.Height(60f)))
                {
                    continue;
                }

                UpsertComponent(directorEntity, new Space4XRunPendingGatePick
                {
                    RoomIndex = director.CurrentRoomIndex,
                    GateOrdinal = gateOrdinal
                });
                Debug.Log($"[FleetcrawlUI] PendingGatePick room={director.CurrentRoomIndex} gate_ordinal={gateOrdinal} gate={gateKind} summary='{Space4XFleetcrawlUiBridge.DescribeOffer(picked)}'.");
            }
        }

        private void DrawBoonChoiceButtons(Entity directorEntity, in Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int gateCount)
        {
            var gateOrdinal = ResolveSelectedGateOrdinal(directorEntity, director, gateCount);
            var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, gateOrdinal);
            if (gateKind != Space4XRunGateKind.Boon)
            {
                GUILayout.Label("Boon Choice: select a Boon gate to preview and pick boons.", _mutedLabelStyle);
                return;
            }

            GUILayout.Label("Boon Choice (3)", _headerStyle);
            var selectedOffer = ResolveSelectedBoonOffer(directorEntity, director);
            for (var offerIndex = 0; offerIndex < 3; offerIndex++)
            {
                var perkId = Space4XFleetcrawlUiBridge.ResolveBoonOfferIdAt(director.Seed, director.CurrentRoomIndex, offerIndex);
                var selected = offerIndex == selectedOffer ? " [selected]" : string.Empty;
                var perkSummary = Space4XFleetcrawlUiBridge.DescribePerk(perkId);
                var label = $"Boon {offerIndex + 1}{selected}: {perkId}\n{perkSummary}";
                if (!GUILayout.Button(label, GUILayout.Height(48f)))
                {
                    continue;
                }

                UpsertComponent(directorEntity, new Space4XRunPendingBoonPick
                {
                    RoomIndex = director.CurrentRoomIndex,
                    OfferIndex = offerIndex
                });
                Debug.Log($"[FleetcrawlUI] PendingBoonPick room={director.CurrentRoomIndex} offer={offerIndex} perk={perkId} summary='{perkSummary}'.");
            }
        }

        private void DrawRunStartPanel(DynamicBuffer<Space4XRunInstalledBlueprint> installed, DynamicBuffer<Space4XRunPerkOp> perkOps)
        {
            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 260f, 30f, 520f, 175f), GUIContent.none, _panelStyle);
            GUILayout.Label("Run Start", _headerStyle);
            GUILayout.Label($"Starter Weapon BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Weapon)}", _labelStyle);
            GUILayout.Label($"Starter Reactor BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Reactor)}", _labelStyle);
            GUILayout.Label($"Starter Hangar BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Hangar)}", _labelStyle);
            GUILayout.Label($"Starter perks: {FormatPerkList(perkOps)}", _labelStyle);
            GUILayout.Label("Pick gate first, then pick boon when Boon gate is selected. Press Enter to close.", _mutedLabelStyle);
            GUILayout.EndArea();
        }

        private void DrawRunEndPanel(Entity directorEntity, in Space4XFleetcrawlDirectorState director, DynamicBuffer<Space4XFleetcrawlRoom> rooms)
        {
            var roomsCleared = math.clamp(director.CurrentRoomIndex + 1, 0, rooms.Length);
            var bossRoomsCleared = 0;
            for (var i = 0; i < roomsCleared; i++)
            {
                if (rooms[i].Kind == Space4XFleetcrawlRoomKind.Boss)
                {
                    bossRoomsCleared++;
                }
            }

            var currency = _entityManager.HasComponent<RunCurrency>(directorEntity)
                ? _entityManager.GetComponentData<RunCurrency>(directorEntity).Value
                : 0;

            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.5f - 120f, 520f, 230f), GUIContent.none, _panelStyle);
            GUILayout.Label("Run Complete", _headerStyle);
            GUILayout.Label($"Rooms Cleared: {roomsCleared}", _labelStyle);
            GUILayout.Label($"Boss Rooms Cleared: {bossRoomsCleared}", _labelStyle);
            GUILayout.Label($"Currency: {currency}", _labelStyle);
            GUILayout.Label($"Build Digest: {director.StableDigest}", _labelStyle);
            GUILayout.Label("Review your build in HUD and restart the scenario for another deterministic run.", _mutedLabelStyle);
            GUILayout.EndArea();
        }

        private void EmitOfferSummaryLogs(in Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int gateCount, Entity directorEntity)
        {
            if (_lastOfferSummaryRoom == director.CurrentRoomIndex && _lastOfferSummarySeed == director.Seed)
            {
                return;
            }

            _lastOfferSummaryRoom = director.CurrentRoomIndex;
            _lastOfferSummarySeed = director.Seed;
            var selectedGate = ResolveSelectedGateOrdinal(directorEntity, director, gateCount);
            var selectedBoon = ResolveSelectedBoonOffer(directorEntity, director);
            for (var gateOrdinal = 0; gateOrdinal < gateCount; gateOrdinal++)
            {
                var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, gateOrdinal);
                Space4XFleetcrawlUiBridge.ResolveGateOffers(director.Seed, director.CurrentRoomIndex, gateKind, out var offerA, out var offerB, out var offerC);
                var pickIndex = gateKind == Space4XRunGateKind.Boon
                    ? selectedBoon
                    : Space4XFleetcrawlUiBridge.ResolveAutoOfferIndex(director.Seed, director.CurrentRoomIndex, gateKind, 3);
                var picked = Space4XFleetcrawlUiBridge.ResolvePickedOffer(director.Seed, director.CurrentRoomIndex, gateKind, pickIndex);
                Debug.Log($"[FleetcrawlUI] GATE_OFFER_SUMMARY room={director.CurrentRoomIndex} gate_ordinal={gateOrdinal} selected={(gateOrdinal == selectedGate ? 1 : 0)} gate={gateKind} offer0={offerA.RewardId} offer1={offerB.RewardId} offer2={offerC.RewardId} picked={picked.RewardId} picked_summary='{Space4XFleetcrawlUiBridge.DescribeOffer(picked)}'.");
                if (gateKind == Space4XRunGateKind.Boon)
                {
                    Debug.Log($"[FleetcrawlUI] BOON_OFFER_SUMMARY room={director.CurrentRoomIndex} gate_ordinal={gateOrdinal} selected_offer={selectedBoon} offer0='{Space4XFleetcrawlUiBridge.DescribePerk(offerA.RewardId)}' offer1='{Space4XFleetcrawlUiBridge.DescribePerk(offerB.RewardId)}' offer2='{Space4XFleetcrawlUiBridge.DescribePerk(offerC.RewardId)}'.");
                }
            }
        }

        private int ResolveSelectedGateOrdinal(Entity directorEntity, in Space4XFleetcrawlDirectorState director, int gateCount)
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

            return selectedGateOrdinal;
        }

        private int ResolveSelectedBoonOffer(Entity directorEntity, in Space4XFleetcrawlDirectorState director)
        {
            var selectedOffer = Space4XFleetcrawlUiBridge.ResolveAutoOfferIndex(director.Seed, director.CurrentRoomIndex, Space4XRunGateKind.Boon, 3);
            if (_entityManager.HasComponent<Space4XRunPendingBoonPick>(directorEntity))
            {
                var pending = _entityManager.GetComponentData<Space4XRunPendingBoonPick>(directorEntity);
                if (pending.RoomIndex == director.CurrentRoomIndex && pending.OfferIndex >= 0 && pending.OfferIndex < 3)
                {
                    selectedOffer = pending.OfferIndex;
                }
            }

            return selectedOffer;
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
            _enemyTelegraphQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XEnemyTelegraphState>(),
                ComponentType.ReadOnly<Space4XRunEnemyTag>());
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
            _mutedLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                richText = false,
                normal = { textColor = new Color(0.74f, 0.74f, 0.74f, 1f) }
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

        private static string FormatPerkList(DynamicBuffer<Space4XRunPerkOp> perkOps)
        {
            if (perkOps.Length == 0)
            {
                return "<none>";
            }

            var text = string.Empty;
            for (var i = 0; i < perkOps.Length; i++)
            {
                if (i > 0)
                {
                    text += " | ";
                }

                text += perkOps[i].PerkId.ToString();
            }

            return text;
        }

        private static string FindBlueprint(DynamicBuffer<Space4XRunInstalledBlueprint> installed, Space4XRunBlueprintKind kind)
        {
            for (var i = 0; i < installed.Length; i++)
            {
                if (installed[i].Kind == kind)
                {
                    return installed[i].BlueprintId.ToString();
                }
            }

            return "<none>";
        }

        private static string BuildRoomObjectiveText(in Space4XFleetcrawlRoom room, in Space4XFleetcrawlDirectorState director, float remainingSeconds)
        {
            var conditions = room.EndConditions;
            if (conditions == 0)
            {
                conditions = Space4XFleetcrawlRoomEndConditionFlags.Timer;
            }

            var text = $"Objective ({room.EndLogic}): ";
            var hasPart = false;
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.Timer) != 0)
            {
                text += $"timer {remainingSeconds:0.0}s";
                hasPart = true;
            }
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.KillQuota) != 0)
            {
                if (hasPart) text += " | ";
                var target = math.max(1, room.KillQuota);
                var progress = math.min(target, director.EnemiesDestroyedInRoom);
                text += $"kills {progress}/{target}";
                hasPart = true;
            }
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.MiniBossQuota) != 0)
            {
                if (hasPart) text += " | ";
                var target = math.max(1, room.MiniBossQuota);
                var progress = math.min(target, director.MiniBossesDestroyedInRoom);
                text += $"mini {progress}/{target}";
                hasPart = true;
            }
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.BossQuota) != 0)
            {
                if (hasPart) text += " | ";
                var target = math.max(1, room.BossQuota);
                var progress = math.min(target, director.BossesDestroyedInRoom);
                text += $"boss {progress}/{target}";
                hasPart = true;
            }

            return hasPart ? text : "Objective: <none>";
        }

        private string BuildTelegraphSummary()
        {
            if (_enemyTelegraphQuery.IsEmptyIgnoreFilter)
            {
                return "Telegraphs: windup N:0 M:0 B:0 | burst N:0 M:0 B:0";
            }

            var states = _enemyTelegraphQuery.ToComponentDataArray<Space4XEnemyTelegraphState>(Allocator.Temp);
            var tags = _enemyTelegraphQuery.ToComponentDataArray<Space4XRunEnemyTag>(Allocator.Temp);
            try
            {
                var normalWindup = 0;
                var miniWindup = 0;
                var bossWindup = 0;
                var normalBurst = 0;
                var miniBurst = 0;
                var bossBurst = 0;
                var count = math.min(states.Length, tags.Length);
                for (var i = 0; i < count; i++)
                {
                    var telegraphing = states[i].IsTelegraphing != 0;
                    var bursting = states[i].IsBursting != 0;
                    switch (tags[i].EnemyClass)
                    {
                        case Space4XFleetcrawlEnemyClass.MiniBoss:
                            if (telegraphing) miniWindup++;
                            if (bursting) miniBurst++;
                            break;
                        case Space4XFleetcrawlEnemyClass.Boss:
                            if (telegraphing) bossWindup++;
                            if (bursting) bossBurst++;
                            break;
                        default:
                            if (telegraphing) normalWindup++;
                            if (bursting) normalBurst++;
                            break;
                    }
                }

                return $"Telegraphs: windup N:{normalWindup} M:{miniWindup} B:{bossWindup} | burst N:{normalBurst} M:{miniBurst} B:{bossBurst}";
            }
            finally
            {
                states.Dispose();
                tags.Dispose();
            }
        }
    }
}
