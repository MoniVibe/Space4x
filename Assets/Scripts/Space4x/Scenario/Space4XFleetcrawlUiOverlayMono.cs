using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Fleetcrawl;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlUiOverlayMono : MonoBehaviour
    {
        private const string UiVisibleEnv = "SPACE4X_FLEETCRAWL_UI_VISIBLE";
        private const string UiShowStatusEnv = "SPACE4X_FLEETCRAWL_UI_SHOW_STATUS";
        private const string UiShowBuildEnv = "SPACE4X_FLEETCRAWL_UI_SHOW_BUILD";
        private const string UiShowChoicesEnv = "SPACE4X_FLEETCRAWL_UI_SHOW_CHOICES";
        private const string UiShowStartPanelEnv = "SPACE4X_FLEETCRAWL_UI_SHOW_START_PANEL";
        private const string UiShowEndPanelEnv = "SPACE4X_FLEETCRAWL_UI_SHOW_END_PANEL";
        private const string UiShowInventoryPanelEnv = "SPACE4X_FLEETCRAWL_UI_SHOW_INVENTORY_PANEL";
        private const string UiInventoryOpenEnv = "SPACE4X_FLEETCRAWL_UI_INVENTORY_OPEN";

        private enum InventoryTab : byte
        {
            CargoLogistics = 0,
            Crew = 1,
            Captain = 2,
            Hangar = 3
        }

        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool showStatusSection = true;
        [SerializeField] private bool showBuildSection = true;
        [SerializeField] private bool showChoiceSection = true;
        [SerializeField] private bool showRunStartPanel = true;
        [SerializeField] private bool showRunEndPanel = true;
        [SerializeField] private bool showInventoryPanel = true;
        [SerializeField] private bool inventoryPanelOpen;
        [SerializeField] private InventoryTab inventoryTab = InventoryTab.CargoLogistics;
        [SerializeField] private Key toggleOverlayKey = Key.F7;
        [SerializeField] private Key toggleStatusKey = Key.F8;
        [SerializeField] private Key toggleBuildKey = Key.F9;
        [SerializeField] private Key toggleChoicesKey = Key.F10;
        [SerializeField] private Key togglePanelsKey = Key.F11;
        [SerializeField] private Key toggleInventoryKey = Key.I;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _timeQuery;
        private EntityQuery _directorQuery;
        private EntityQuery _flagshipQuery;
        private EntityQuery _playerResourcesQuery;
        private EntityQuery _strikeCraftQuery;
        private bool _queriesReady;

        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _mutedLabelStyle;
        private bool _stylesReady;

        private bool _introInitialized;
        private float _introUntilRealtime;

        private void Awake()
        {
            ApplyEnvironmentOverrides();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[toggleInventoryKey].wasPressedThisFrame && showInventoryPanel)
            {
                inventoryPanelOpen = !inventoryPanelOpen;
            }

            if (inventoryPanelOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                inventoryPanelOpen = false;
            }

            if (keyboard[toggleOverlayKey].wasPressedThisFrame)
            {
                showOverlay = !showOverlay;
            }

            if (keyboard[toggleStatusKey].wasPressedThisFrame)
            {
                showStatusSection = !showStatusSection;
            }

            if (keyboard[toggleBuildKey].wasPressedThisFrame)
            {
                showBuildSection = !showBuildSection;
            }

            if (keyboard[toggleChoicesKey].wasPressedThisFrame)
            {
                showChoiceSection = !showChoiceSection;
            }

            if (keyboard[togglePanelsKey].wasPressedThisFrame)
            {
                var enablePanels = !(showRunStartPanel && showRunEndPanel);
                showRunStartPanel = enablePanels;
                showRunEndPanel = enablePanels;
            }

            if (!inventoryPanelOpen)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                inventoryTab = InventoryTab.CargoLogistics;
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                inventoryTab = InventoryTab.Crew;
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                inventoryTab = InventoryTab.Captain;
            }
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                inventoryTab = InventoryTab.Hangar;
            }
        }

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

            if (!showOverlay)
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

            if (!_introInitialized && director.Initialized != 0)
            {
                _introInitialized = true;
                _introUntilRealtime = Time.realtimeSinceStartup + 9f;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(18f, 18f, 700f, 640f), GUIContent.none, _panelStyle);
            GUILayout.Label("FleetCrawl HUD", _headerStyle);
            GUILayout.Label(
                $"Toggles {toggleOverlayKey}=overlay {toggleStatusKey}=status {toggleBuildKey}=build {toggleChoicesKey}=choices {togglePanelsKey}=panels {toggleInventoryKey}=inventory",
                _mutedLabelStyle);

            if (showStatusSection)
            {
                GUILayout.Label($"Scenario: {scenarioInfo.ScenarioId}", _labelStyle);
                GUILayout.Label($"Room {roomIndex + 1}/{rooms.Length}  kind={room.Kind}  remaining={remainingSeconds:0.0}s", _labelStyle);
                GUILayout.Label($"Tick {time.Tick}  dt={dt:0.000}  digest={director.StableDigest}", _labelStyle);

                var status = Space4XFleetcrawlPlayerControlMono.CurrentStatus;
                GUILayout.Label(
                    $"Pilot: boost={status.Boost01 * 100f:0}%  dash_cd={status.DashCooldown:0.00}s  speed={status.Speed:0.0}  special={status.SpecialEnergyCurrent:0.#}/{status.SpecialEnergyMax:0.#} ({status.SpecialEnergy01 * 100f:0}%)",
                    _labelStyle);
                GUILayout.Label("Controls: WASD move, Shift boost, Q dash, F snap camera, Mouse wheel zoom", _labelStyle);
            }

            if (showBuildSection)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"Perks ({perkOps.Length}): {FormatPerkList(perkOps)}", _labelStyle);
                GUILayout.Label($"Weapon BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Weapon)}", _labelStyle);
                GUILayout.Label($"Reactor BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Reactor)}", _labelStyle);
                GUILayout.Label($"Hangar Module BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Hangar)}", _labelStyle);
            }

            if (showChoiceSection)
            {
                GUILayout.Space(10f);
                GUILayout.Label($"Reward Lane Choice ({gateCount})", _headerStyle);
                DrawGateChoiceButtons(directorEntity, director, room, gateCount);

                GUILayout.Space(10f);
                DrawBoonChoiceButtons(directorEntity, director, room, gateCount);
            }
            else
            {
                GUILayout.Space(10f);
                GUILayout.Label("Reward lane choices hidden.", _mutedLabelStyle);
            }
            GUILayout.EndArea();

            var flagshipEntity = ResolveFlagshipEntity();
            if (showInventoryPanel && inventoryPanelOpen)
            {
                DrawInventoryPanel(directorEntity, director, perkOps, installed, flagshipEntity);
            }

            if (showRunStartPanel && _introUntilRealtime > Time.realtimeSinceStartup && director.RunCompleted == 0)
            {
                DrawRunStartPanel(installed, perkOps);
            }

            if (showRunEndPanel && director.RunCompleted != 0)
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
                var gateLane = Space4XFleetcrawlUiBridge.DescribeGateLane(room.Kind, room.ReliefKind, gateKind);
                var selected = gateOrdinal == selectedGateOrdinal ? " [selected]" : string.Empty;
                var offerIndex = gateKind == Space4XRunGateKind.Boon ? selectedBoonOffer : Space4XFleetcrawlUiBridge.ResolveAutoOfferIndex(director.Seed, director.CurrentRoomIndex, gateKind, 3);
                var picked = Space4XFleetcrawlUiBridge.ResolvePickedOffer(director.Seed, director.CurrentRoomIndex, gateKind, offerIndex);
                Space4XFleetcrawlUiBridge.ResolveGateOffers(director.Seed, director.CurrentRoomIndex, gateKind, out var offerA, out var offerB, out var offerC);

                var label = $"Gate {gateOrdinal + 1} [{gateLane}]{selected}\n" +
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
                Debug.Log($"[FleetcrawlUI] PendingGatePick room={director.CurrentRoomIndex} gate_ordinal={gateOrdinal} gate={gateKind} lane='{gateLane}' summary='{Space4XFleetcrawlUiBridge.DescribeOffer(picked)}'.");
            }
        }

        private void DrawBoonChoiceButtons(Entity directorEntity, in Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int gateCount)
        {
            var gateOrdinal = ResolveSelectedGateOrdinal(directorEntity, director, gateCount);
            var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(room.Kind, gateOrdinal);
            if (gateKind != Space4XRunGateKind.Boon)
            {
                GUILayout.Label("Room Boon Choice: select the 'Room Reward: Boon' lane first.", _mutedLabelStyle);
                return;
            }

            GUILayout.Label("Room Completion Boon (3)", _headerStyle);
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
            GUILayout.Label($"Starter Hangar Module BP: {FindBlueprint(installed, Space4XRunBlueprintKind.Hangar)}", _labelStyle);
            GUILayout.Label($"Starter perks: {FormatPerkList(perkOps)}", _labelStyle);
            GUILayout.Label("Pick reward lane first, then choose room boon when 'Room Reward: Boon' lane is selected. Press Enter to close.", _mutedLabelStyle);
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

        private Entity ResolveFlagshipEntity()
        {
            if (_flagshipQuery.IsEmptyIgnoreFilter)
            {
                return Entity.Null;
            }

            if (_flagshipQuery.CalculateEntityCount() == 1)
            {
                return _flagshipQuery.GetSingletonEntity();
            }

            using var entities = _flagshipQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        private void DrawInventoryPanel(
            Entity directorEntity,
            in Space4XFleetcrawlDirectorState director,
            DynamicBuffer<Space4XRunPerkOp> perkOps,
            DynamicBuffer<Space4XRunInstalledBlueprint> installed,
            Entity flagshipEntity)
        {
            var panelWidth = 520f;
            var panelX = math.max(18f, Screen.width - panelWidth - 18f);
            var panelY = 18f;
            var panelHeight = math.max(280f, Screen.height - 36f);

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none, _panelStyle);
            GUILayout.Label("Flagship Console", _headerStyle);
            GUILayout.Label("I toggle  Esc close  1 Cargo  2 Crew  3 Captain  4 Hangar", _mutedLabelStyle);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(inventoryTab == InventoryTab.CargoLogistics, "1 Cargo/Inventory/Logistics", GUI.skin.button))
            {
                inventoryTab = InventoryTab.CargoLogistics;
            }
            if (GUILayout.Toggle(inventoryTab == InventoryTab.Crew, "2 Crew", GUI.skin.button))
            {
                inventoryTab = InventoryTab.Crew;
            }
            if (GUILayout.Toggle(inventoryTab == InventoryTab.Captain, "3 Captain", GUI.skin.button))
            {
                inventoryTab = InventoryTab.Captain;
            }
            if (GUILayout.Toggle(inventoryTab == InventoryTab.Hangar, "4 Hangar", GUI.skin.button))
            {
                inventoryTab = InventoryTab.Hangar;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            switch (inventoryTab)
            {
                case InventoryTab.Crew:
                    DrawCrewTab(flagshipEntity);
                    break;
                case InventoryTab.Captain:
                    DrawCaptainTab(directorEntity, flagshipEntity);
                    break;
                case InventoryTab.Hangar:
                    DrawHangarTab(installed, flagshipEntity);
                    break;
                default:
                    DrawCargoLogisticsTab(directorEntity, director, perkOps, installed, flagshipEntity);
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawCargoLogisticsTab(
            Entity directorEntity,
            in Space4XFleetcrawlDirectorState director,
            DynamicBuffer<Space4XRunPerkOp> perkOps,
            DynamicBuffer<Space4XRunInstalledBlueprint> installed,
            Entity flagshipEntity)
        {
            GUILayout.Label("Cargo/Inventory/Logistics", _headerStyle);
            GUILayout.Label($"Run digest={director.StableDigest} room={director.CurrentRoomIndex + 1}", _mutedLabelStyle);

            if (flagshipEntity != Entity.Null)
            {
                if (_entityManager.HasComponent<HullIntegrity>(flagshipEntity))
                {
                    var hull = _entityManager.GetComponentData<HullIntegrity>(flagshipEntity);
                    GUILayout.Label($"Hull {hull.Current:0}/{hull.Max:0} ({hull.Ratio * 100f:0}%) perm_dmg={hull.PermanentDamageRatio * 100f:0.0}%", _labelStyle);
                }

                if (_entityManager.HasComponent<Space4XShield>(flagshipEntity))
                {
                    var shield = _entityManager.GetComponentData<Space4XShield>(flagshipEntity);
                    GUILayout.Label($"Shield {shield.Current:0}/{shield.Maximum:0} ({shield.Ratio * 100f:0}%)", _labelStyle);
                }

                if (_entityManager.HasComponent<ShipSpecialEnergyState>(flagshipEntity))
                {
                    var special = _entityManager.GetComponentData<ShipSpecialEnergyState>(flagshipEntity);
                    GUILayout.Label($"Special energy {special.Current:0.0}/{special.EffectiveMax:0.0} ({special.Ratio * 100f:0}%)", _labelStyle);
                }

                if (_entityManager.HasComponent<FleetcrawlHeatOutputState>(flagshipEntity))
                {
                    var heat = _entityManager.GetComponentData<FleetcrawlHeatOutputState>(flagshipEntity);
                    GUILayout.Label($"Heat {heat.Heat01 * 100f:0}%  overheat={heat.IsOverheated}  throttle={heat.FireRateThrottleMultiplier:0.00}x", _labelStyle);
                }
            }
            else
            {
                GUILayout.Label("No flagship entity claimed.", _mutedLabelStyle);
            }

            var currency = _entityManager.HasComponent<RunCurrency>(directorEntity)
                ? _entityManager.GetComponentData<RunCurrency>(directorEntity).Value
                : 0;
            var reroll = _entityManager.HasComponent<Space4XRunRerollTokens>(directorEntity)
                ? _entityManager.GetComponentData<Space4XRunRerollTokens>(directorEntity).Value
                : 0;
            GUILayout.Label($"Run wallet currency={currency} reroll_tokens={reroll}", _labelStyle);
            GUILayout.Label($"Perks={perkOps.Length}  weapon={FindBlueprint(installed, Space4XRunBlueprintKind.Weapon)}", _labelStyle);
            GUILayout.Label($"Reactor={FindBlueprint(installed, Space4XRunBlueprintKind.Reactor)}  hangar_module={FindBlueprint(installed, Space4XRunBlueprintKind.Hangar)}", _labelStyle);

            if (_entityManager.HasBuffer<Space4XRunGateRewardRecord>(directorEntity))
            {
                var rewards = _entityManager.GetBuffer<Space4XRunGateRewardRecord>(directorEntity);
                GUILayout.Space(6f);
                GUILayout.Label("Recent rewards", _headerStyle);
                if (rewards.Length == 0)
                {
                    GUILayout.Label("No rewards recorded yet.", _mutedLabelStyle);
                }
                else
                {
                    var start = math.max(0, rewards.Length - 6);
                    for (var i = start; i < rewards.Length; i++)
                    {
                        var record = rewards[i];
                        GUILayout.Label(
                            $"R{record.RoomIndex + 1} {DescribeRewardSource(record.SourceKind)} {record.GateKind}/{record.RewardKind}: {record.RewardId}",
                            _labelStyle);
                    }
                }
            }

            if (flagshipEntity != Entity.Null && _entityManager.HasComponent<SupplyStatus>(flagshipEntity))
            {
                var supply = _entityManager.GetComponentData<SupplyStatus>(flagshipEntity);
                GUILayout.Space(6f);
                GUILayout.Label("Flagship supplies", _headerStyle);
                GUILayout.Label($"Fuel {supply.Fuel:0}/{supply.FuelCapacity:0}  Ammo {supply.Ammunition:0}/{supply.AmmunitionCapacity:0}", _labelStyle);
                GUILayout.Label($"Provisions {supply.Provisions:0}/{supply.ProvisionsCapacity:0}  LifeSupport {supply.LifeSupport:0}/{supply.LifeSupportCapacity:0}", _labelStyle);
                GUILayout.Label($"RepairParts {supply.RepairParts:0}/{supply.RepairPartsCapacity:0}  activity={supply.Activity}", _labelStyle);
            }

            if (flagshipEntity != Entity.Null && _entityManager.HasBuffer<ResourceStorage>(flagshipEntity))
            {
                var storage = _entityManager.GetBuffer<ResourceStorage>(flagshipEntity);
                GUILayout.Space(6f);
                GUILayout.Label("Flagship cargo hold", _headerStyle);
                var listed = 0;
                for (var i = 0; i < storage.Length; i++)
                {
                    var entry = storage[i];
                    if (entry.Amount <= 0.01f)
                    {
                        continue;
                    }

                    GUILayout.Label($"{entry.Type}: {entry.Amount:0.0}/{entry.Capacity:0.0}", _labelStyle);
                    listed++;
                    if (listed >= 8)
                    {
                        break;
                    }
                }

                if (listed == 0)
                {
                    GUILayout.Label("No stored resources on flagship.", _mutedLabelStyle);
                }
            }

            if (!_playerResourcesQuery.IsEmptyIgnoreFilter)
            {
                var playerResources = _playerResourcesQuery.GetSingleton<PlayerResources>();
                GUILayout.Space(6f);
                GUILayout.Label("Player resource totals", _headerStyle);
                GUILayout.Label($"Minerals {playerResources.Minerals:0.0}  RareMetals {playerResources.RareMetals:0.0}  EnergyCrystals {playerResources.EnergyCrystals:0.0}", _labelStyle);
                GUILayout.Label($"Fuel {playerResources.Fuel:0.0}  Supplies {playerResources.Supplies:0.0}  Salvage {playerResources.SalvageComponents:0.0}", _labelStyle);
            }
        }

        private void DrawCrewTab(Entity flagshipEntity)
        {
            GUILayout.Label("Crew", _headerStyle);
            if (flagshipEntity == Entity.Null)
            {
                GUILayout.Label("No flagship entity claimed.", _mutedLabelStyle);
                return;
            }

            if (_entityManager.HasComponent<CrewCapacity>(flagshipEntity))
            {
                var crew = _entityManager.GetComponentData<CrewCapacity>(flagshipEntity);
                GUILayout.Label($"Crew {crew.CurrentCrew}/{crew.MaxCrew}  critical_max={crew.CriticalMax}", _labelStyle);
                GUILayout.Label($"Crew ratio {crew.CrewRatio * 100f:0.0}%  overcrowded={crew.IsOvercrowded}  critical={crew.IsCriticallyOvercrowded}", _labelStyle);
            }

            if (_entityManager.HasBuffer<PlatformCrewMember>(flagshipEntity))
            {
                var crewMembers = _entityManager.GetBuffer<PlatformCrewMember>(flagshipEntity);
                GUILayout.Label($"Crew roster entries: {crewMembers.Length}", _labelStyle);
            }

            if (_entityManager.HasComponent<CrewSkills>(flagshipEntity))
            {
                var skills = _entityManager.GetComponentData<CrewSkills>(flagshipEntity);
                GUILayout.Label($"Skills mining={skills.MiningSkill:0.00} hauling={skills.HaulingSkill:0.00} combat={skills.CombatSkill:0.00}", _labelStyle);
                GUILayout.Label($"Skills repair={skills.RepairSkill:0.00} exploration={skills.ExplorationSkill:0.00}", _labelStyle);
            }

            if (_entityManager.HasComponent<CrewGrowthState>(flagshipEntity))
            {
                var growth = _entityManager.GetComponentData<CrewGrowthState>(flagshipEntity);
                GUILayout.Label($"Growth state current={growth.CurrentCrew:0.0} capacity={growth.Capacity:0.0}", _labelStyle);
            }

            if (_entityManager.HasComponent<CrewGrowthTelemetry>(flagshipEntity))
            {
                var telemetry = _entityManager.GetComponentData<CrewGrowthTelemetry>(flagshipEntity);
                GUILayout.Label($"Growth telemetry breeding={telemetry.BreedingAttempts} cloning={telemetry.CloningAttempts} skipped={telemetry.GrowthSkipped}", _labelStyle);
            }
        }

        private void DrawCaptainTab(Entity directorEntity, Entity flagshipEntity)
        {
            GUILayout.Label("Captain", _headerStyle);

            if (_entityManager.HasComponent<Space4XRunStartingCaptainState>(directorEntity))
            {
                var runCaptain = _entityManager.GetComponentData<Space4XRunStartingCaptainState>(directorEntity);
                GUILayout.Label($"Run profile={runCaptain.ProfileId} pool={runCaptain.CandidatePoolSize}", _labelStyle);
                GUILayout.Label($"UnlockFlags={runCaptain.UnlockFlags} forced={runCaptain.ForcedSelection} random={runCaptain.RandomSelection}", _labelStyle);
            }
            else
            {
                GUILayout.Label("Run captain profile not available.", _mutedLabelStyle);
            }

            if (flagshipEntity == Entity.Null)
            {
                GUILayout.Label("No flagship entity claimed.", _mutedLabelStyle);
                GUILayout.Label("Entity profile panel will expand here next.", _mutedLabelStyle);
                return;
            }

            if (_entityManager.HasComponent<CaptainState>(flagshipEntity))
            {
                var captain = _entityManager.GetComponentData<CaptainState>(flagshipEntity);
                GUILayout.Label($"Captain autonomy={captain.Autonomy} ready={captain.IsReady} confidence={(float)captain.Confidence:0.00} risk={(float)captain.RiskTolerance:0.00}", _labelStyle);
                GUILayout.Label($"Captain results success={captain.SuccessCount} failure={captain.FailureCount}", _labelStyle);
            }

            if (_entityManager.HasComponent<CaptainReadiness>(flagshipEntity))
            {
                var readiness = _entityManager.GetComponentData<CaptainReadiness>(flagshipEntity);
                GUILayout.Label($"Readiness score={(float)readiness.CurrentReadiness:0.00} failed={readiness.FailedChecks}", _labelStyle);
                GUILayout.Label($"Thresholds hull={(float)readiness.MinHullRatio:0.00} fuel={(float)readiness.MinFuelRatio:0.00} ammo={(float)readiness.MinAmmoRatio:0.00}", _labelStyle);
            }

            if (_entityManager.HasComponent<CaptainOrder>(flagshipEntity))
            {
                var order = _entityManager.GetComponentData<CaptainOrder>(flagshipEntity);
                GUILayout.Label($"Order type={order.Type} status={order.Status} priority={order.Priority}", _labelStyle);
                GUILayout.Label($"Order target={order.TargetEntity} timeout_tick={order.TimeoutTick}", _labelStyle);
            }

            if (_entityManager.HasComponent<CaptainAggregateBrief>(flagshipEntity))
            {
                var brief = _entityManager.GetComponentData<CaptainAggregateBrief>(flagshipEntity);
                GUILayout.Label($"Brief alert={brief.AlertLevel} hull={brief.HullRatio * 100f:0}% shield={brief.ShieldRatio * 100f:0}%", _labelStyle);
                GUILayout.Label($"Brief fuel={brief.FuelRatio * 100f:0}% ammo={brief.AmmoRatio * 100f:0}% contacts={brief.ContactsTracked}", _labelStyle);
            }

            GUILayout.Label("Entity profile details hook will be added in the next pass.", _mutedLabelStyle);
        }

        private void DrawHangarTab(DynamicBuffer<Space4XRunInstalledBlueprint> installed, Entity flagshipEntity)
        {
            GUILayout.Label("Hangar", _headerStyle);
            GUILayout.Label($"Installed hangar module blueprint: {FindBlueprint(installed, Space4XRunBlueprintKind.Hangar)}", _labelStyle);

            if (flagshipEntity == Entity.Null)
            {
                GUILayout.Label("No flagship entity claimed.", _mutedLabelStyle);
                return;
            }

            if (_entityManager.HasComponent<HangarCapacity>(flagshipEntity))
            {
                var hangar = _entityManager.GetComponentData<HangarCapacity>(flagshipEntity);
                GUILayout.Label($"Hangar capacity (module): {hangar.Capacity:0.0}", _labelStyle);
            }

            if (_entityManager.HasComponent<DockingCapacity>(flagshipEntity))
            {
                var docking = _entityManager.GetComponentData<DockingCapacity>(flagshipEntity);
                GUILayout.Label($"Docking total {docking.TotalDocked}/{docking.TotalCapacity} ({docking.Utilization * 100f:0}%)", _labelStyle);
                GUILayout.Label($"Small {docking.CurrentSmallCraft}/{docking.MaxSmallCraft}  Medium {docking.CurrentMediumCraft}/{docking.MaxMediumCraft}", _labelStyle);
                GUILayout.Label($"Large {docking.CurrentLargeCraft}/{docking.MaxLargeCraft}  Utility {docking.CurrentUtility}/{docking.MaxUtility}", _labelStyle);
            }

            var totalCraft = 0;
            var dockedCraft = 0;
            var launchedCraft = 0;
            var fighter = 0;
            var interceptor = 0;
            var bomber = 0;
            var recon = 0;
            var suppression = 0;
            var ewar = 0;
            var withPilotLink = 0;
            if (!_strikeCraftQuery.IsEmptyIgnoreFilter)
            {
                using var craftEntities = _strikeCraftQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                using var craftProfiles = _strikeCraftQuery.ToComponentDataArray<StrikeCraftProfile>(Unity.Collections.Allocator.Temp);
                var count = math.min(craftEntities.Length, craftProfiles.Length);
                for (var i = 0; i < count; i++)
                {
                    var profile = craftProfiles[i];
                    if (profile.Carrier != flagshipEntity)
                    {
                        continue;
                    }

                    totalCraft++;
                    if (profile.Phase == AttackRunPhase.Docked)
                    {
                        dockedCraft++;
                    }
                    else
                    {
                        launchedCraft++;
                    }

                    if (_entityManager.HasComponent<StrikeCraftPilotLink>(craftEntities[i]))
                    {
                        withPilotLink++;
                    }

                    switch (profile.Role)
                    {
                        case StrikeCraftRole.Interceptor:
                            interceptor++;
                            break;
                        case StrikeCraftRole.Bomber:
                            bomber++;
                            break;
                        case StrikeCraftRole.Recon:
                            recon++;
                            break;
                        case StrikeCraftRole.Suppression:
                            suppression++;
                            break;
                        case StrikeCraftRole.EWar:
                            ewar++;
                            break;
                        default:
                            fighter++;
                            break;
                    }
                }
            }

            GUILayout.Label($"Craft assigned={totalCraft} docked={dockedCraft} launched={launchedCraft} pilot_links={withPilotLink}", _labelStyle);
            GUILayout.Label($"Roles fighter={fighter} interceptor={interceptor} bomber={bomber} recon={recon} suppression={suppression} ewar={ewar}", _labelStyle);
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
            _flagshipQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerFlagshipTag>());
            _playerResourcesQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerResources>());
            _strikeCraftQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<StrikeCraftProfile>());
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

        private static string DescribeRewardSource(Space4XRunRewardSourceKind source)
        {
            return source switch
            {
                Space4XRunRewardSourceKind.Mission => "mission",
                Space4XRunRewardSourceKind.Market => "market",
                Space4XRunRewardSourceKind.Salvage => "salvage",
                Space4XRunRewardSourceKind.Loot => "loot",
                _ => source.ToString()
            };
        }

        private void ApplyEnvironmentOverrides()
        {
            ApplyOptionalEnvironmentToggle(UiVisibleEnv, ref showOverlay);
            ApplyOptionalEnvironmentToggle(UiShowStatusEnv, ref showStatusSection);
            ApplyOptionalEnvironmentToggle(UiShowBuildEnv, ref showBuildSection);
            ApplyOptionalEnvironmentToggle(UiShowChoicesEnv, ref showChoiceSection);
            ApplyOptionalEnvironmentToggle(UiShowStartPanelEnv, ref showRunStartPanel);
            ApplyOptionalEnvironmentToggle(UiShowEndPanelEnv, ref showRunEndPanel);
            ApplyOptionalEnvironmentToggle(UiShowInventoryPanelEnv, ref showInventoryPanel);
            ApplyOptionalEnvironmentToggle(UiInventoryOpenEnv, ref inventoryPanelOpen);
        }

        private static void ApplyOptionalEnvironmentToggle(string envName, ref bool currentValue)
        {
            if (TryParseBooleanEnvironment(envName, out var parsed))
            {
                currentValue = parsed;
            }
        }

        private static bool TryParseBooleanEnvironment(string envName, out bool value)
        {
            value = false;
            var raw = System.Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var normalized = raw.Trim();
            if (normalized.Equals("1", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("yes", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("on", System.StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (normalized.Equals("0", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("false", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("no", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("off", System.StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }
    }
}
