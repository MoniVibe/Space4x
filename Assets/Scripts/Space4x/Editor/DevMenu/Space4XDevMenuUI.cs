using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor.DevMenu
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// RimWorld-style developer menu for Space4X.
    /// Provides runtime spawning, inspection, and manipulation of entities.
    /// Press ~ (tilde) or F12 to toggle the dev menu.
    /// </summary>
    public class Space4XDevMenuUI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private Space4XDevSpawnRegistry spawnRegistry;
        [SerializeField] private Key toggleKey = Key.F12;
        [SerializeField] private Key altToggleKey = Key.Backquote; // Tilde

        [Header("Spawn Settings")]
        [SerializeField] private string currentFactionId = "player";
        [SerializeField] private int spawnCount = 1;
        [SerializeField] private float spawnSpread = 5f;

        // UI State
        private bool _isOpen;
        private Rect _windowRect = new Rect(10, 10, 350, 500);
        private Vector2 _scrollPosition;
        private string _currentCategory = "";
        private string _selectedTemplateId = "";
        private bool _showSpawnOptions;
        private bool _showDebugInfo;
        private bool _showEntityList;
        private string _searchFilter = "";

        // Spawn mode
        private bool _spawnAtCursor;
        private float3 _spawnPosition;

        // Entity list
        private Vector2 _entityListScroll;
        private List<(Entity entity, string name, float3 position)> _cachedEntities = new();
        private float _lastEntityRefresh;
        private const float EntityRefreshInterval = 1f;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _templateStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;

        private void Update()
        {
            if (Keyboard.current == null) return;

            // Toggle menu
            if (KeyPressed(toggleKey) || KeyPressed(altToggleKey))
            {
                _isOpen = !_isOpen;
            }

            // Update spawn position from mouse
            if (_spawnAtCursor && _isOpen)
            {
                UpdateSpawnPositionFromMouse();
            }

            // Refresh entity list periodically
            if (_showEntityList && Time.time - _lastEntityRefresh > EntityRefreshInterval)
            {
                RefreshEntityList();
                _lastEntityRefresh = Time.time;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitializeStyles();

            _windowRect = GUILayout.Window(
                GetInstanceID(),
                _windowRect,
                DrawWindow,
                "Space4X Dev Menu",
                GUILayout.MinWidth(300),
                GUILayout.MinHeight(400));
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _categoryStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5)
            };

            _templateStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(20, 10, 3, 3)
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12
            };

            _stylesInitialized = true;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(5);

            // Top toolbar
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn", _buttonStyle, GUILayout.Width(70)))
            {
                _showSpawnOptions = true;
                _showDebugInfo = false;
                _showEntityList = false;
            }
            if (GUILayout.Button("Entities", _buttonStyle, GUILayout.Width(70)))
            {
                _showSpawnOptions = false;
                _showDebugInfo = false;
                _showEntityList = true;
                RefreshEntityList();
            }
            if (GUILayout.Button("Debug", _buttonStyle, GUILayout.Width(70)))
            {
                _showSpawnOptions = false;
                _showDebugInfo = true;
                _showEntityList = false;
            }
            if (GUILayout.Button("X", _buttonStyle, GUILayout.Width(30)))
            {
                _isOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            if (_showSpawnOptions)
            {
                DrawSpawnPanel();
            }
            else if (_showDebugInfo)
            {
                DrawDebugPanel();
            }
            else if (_showEntityList)
            {
                DrawEntityListPanel();
            }
            else
            {
                DrawSpawnPanel(); // Default
            }

            GUILayout.EndScrollView();

            // Make window draggable
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }

        private void DrawSpawnPanel()
        {
            GUILayout.Label("Spawn Entities", _headerStyle);
            GUILayout.Space(10);

            // Spawn settings
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Spawn Settings", EditorStyles.boldLabel);

            // Faction selection
            GUILayout.BeginHorizontal();
            GUILayout.Label("Faction:", GUILayout.Width(70));
            if (spawnRegistry != null)
            {
                foreach (var faction in spawnRegistry.factions)
                {
                    GUI.color = faction.id == currentFactionId ? Color.green : Color.white;
                    if (GUILayout.Button(faction.displayName, GUILayout.Width(80)))
                    {
                        currentFactionId = faction.id;
                    }
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            // Count
            GUILayout.BeginHorizontal();
            GUILayout.Label("Count:", GUILayout.Width(70));
            if (GUILayout.Button("-", GUILayout.Width(30))) spawnCount = Mathf.Max(1, spawnCount - 1);
            GUILayout.Label(spawnCount.ToString(), GUILayout.Width(40));
            if (GUILayout.Button("+", GUILayout.Width(30))) spawnCount = Mathf.Min(100, spawnCount + 1);
            if (GUILayout.Button("x10", GUILayout.Width(40))) spawnCount = Mathf.Min(1000, spawnCount * 10);
            GUILayout.EndHorizontal();

            // Spread
            GUILayout.BeginHorizontal();
            GUILayout.Label("Spread:", GUILayout.Width(70));
            spawnSpread = GUILayout.HorizontalSlider(spawnSpread, 0f, 100f, GUILayout.Width(150));
            GUILayout.Label($"{spawnSpread:F1}", GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Position mode
            GUILayout.BeginHorizontal();
            _spawnAtCursor = GUILayout.Toggle(_spawnAtCursor, "Spawn at Cursor");
            if (!_spawnAtCursor)
            {
                GUILayout.Label($"Pos: ({_spawnPosition.x:F0}, {_spawnPosition.y:F0}, {_spawnPosition.z:F0})");
            }
            GUILayout.EndHorizontal();

            // Manual position
            if (!_spawnAtCursor)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("X:", GUILayout.Width(20));
                float.TryParse(GUILayout.TextField(_spawnPosition.x.ToString("F0"), GUILayout.Width(50)), out float x);
                GUILayout.Label("Y:", GUILayout.Width(20));
                float.TryParse(GUILayout.TextField(_spawnPosition.y.ToString("F0"), GUILayout.Width(50)), out float y);
                GUILayout.Label("Z:", GUILayout.Width(20));
                float.TryParse(GUILayout.TextField(_spawnPosition.z.ToString("F0"), GUILayout.Width(50)), out float z);
                _spawnPosition = new float3(x, y, z);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Search filter
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = GUILayout.TextField(_searchFilter);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                _searchFilter = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Category buttons
            if (spawnRegistry != null)
            {
                foreach (var category in spawnRegistry.GetCategories())
                {
                    bool isExpanded = _currentCategory == category;
                    GUI.color = isExpanded ? new Color(0.7f, 0.9f, 0.7f) : Color.white;

                    if (GUILayout.Button((isExpanded ? "▼ " : "► ") + category, _categoryStyle))
                    {
                        _currentCategory = isExpanded ? "" : category;
                    }

                    GUI.color = Color.white;

                    if (isExpanded)
                    {
                        DrawCategoryTemplates(category);
                    }
                }
            }
            else
            {
                GUILayout.Label("No Spawn Registry assigned!", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } });
            }
        }

        private void DrawCategoryTemplates(string category)
        {
            var templates = spawnRegistry.GetTemplatesInCategory(category);

            foreach (var (id, displayName, description) in templates)
            {
                // Filter by search
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    if (!displayName.ToLower().Contains(_searchFilter.ToLower()) &&
                        !id.ToLower().Contains(_searchFilter.ToLower()))
                    {
                        continue;
                    }
                }

                GUILayout.BeginHorizontal();

                bool isSelected = _selectedTemplateId == id;
                GUI.color = isSelected ? new Color(0.8f, 0.9f, 1f) : Color.white;

                if (GUILayout.Button(displayName, _templateStyle, GUILayout.Width(180)))
                {
                    _selectedTemplateId = id;
                }

                GUI.color = Color.green;
                if (GUILayout.Button("Spawn", GUILayout.Width(60)))
                {
                    SpawnEntity(category, id);
                }
                GUI.color = Color.white;

                GUILayout.EndHorizontal();

                // Show description if selected
                if (isSelected && !string.IsNullOrEmpty(description))
                {
                    GUILayout.Label("  " + description, new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true });
                }
            }
        }

        private void DrawDebugPanel()
        {
            GUILayout.Label("Debug Info", _headerStyle);
            GUILayout.Space(10);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                GUILayout.Label("No ECS World active", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } });
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"World: {world.Name}");
            GUILayout.Label($"Entity Count: {world.EntityManager.UniversalQuery.CalculateEntityCount()}");
            GUILayout.Label($"Time: {Time.time:F2}s");
            GUILayout.Label($"Frame: {Time.frameCount}");
            GUILayout.Label($"Delta: {Time.deltaTime * 1000:F2}ms");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Quick actions
            GUILayout.Label("Quick Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Spawn Test Fleet (1 Carrier + 12 Fighters)"))
            {
                SpawnTestFleet();
            }

            if (GUILayout.Button("Spawn Combat Scenario (2 Fleets)"))
            {
                SpawnCombatScenario();
            }

            if (GUILayout.Button("Spawn Mining Operation"))
            {
                SpawnMiningOperation();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Clear All Entities (DANGEROUS)"))
            {
                if (Event.current.shift)
                {
                    ClearAllEntities();
                }
                else
                {
                    UnityDebug.LogWarning("[DevMenu] Hold Shift and click to clear all entities");
                }
            }
        }

        private void DrawEntityListPanel()
        {
            GUILayout.Label("Entity List", _headerStyle);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
            {
                RefreshEntityList();
            }
            GUILayout.Label($"Count: {_cachedEntities.Count}");
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            _entityListScroll = GUILayout.BeginScrollView(_entityListScroll, GUILayout.Height(350));

            foreach (var (entity, name, position) in _cachedEntities)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"[{entity.Index}] {name}", GUILayout.Width(180));
                GUILayout.Label($"({position.x:F0}, {position.y:F0}, {position.z:F0})", GUILayout.Width(100));
                if (GUILayout.Button("→", GUILayout.Width(25)))
                {
                    // Focus camera on entity
                    FocusCameraOnPosition(position);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void SpawnEntity(string category, string templateId)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                UnityDebug.LogError("[DevMenu] No ECS World active");
                return;
            }

            var em = world.EntityManager;
            var random = new Unity.Mathematics.Random((uint)DateTime.Now.Ticks);

            for (int i = 0; i < spawnCount; i++)
            {
                float3 offset = random.NextFloat3(-spawnSpread, spawnSpread);
                offset.y = 0; // Keep on same plane
                float3 pos = _spawnPosition + offset;

                Entity entity = em.CreateEntity();

                // Add basic components
                em.AddComponentData(entity, LocalTransform.FromPosition(pos));

                // Add spawn marker for runtime systems to pick up
                em.AddComponentData(entity, new DevSpawnRequest
                {
                    Category = new Unity.Collections.FixedString32Bytes(category),
                    TemplateId = new Unity.Collections.FixedString64Bytes(templateId),
                    FactionId = new Unity.Collections.FixedString32Bytes(currentFactionId),
                    Position = pos
                });

                UnityDebug.Log($"[DevMenu] Spawned {templateId} at {pos} (Entity {entity.Index})");
            }

            UnityDebug.Log($"[DevMenu] Spawned {spawnCount}x {templateId} in category {category}");
        }

        private void SpawnTestFleet()
        {
            _spawnPosition = float3.zero;
            currentFactionId = "player";

            // Spawn carrier
            spawnCount = 1;
            SpawnEntity("Carriers", "carrier_medium");

            // Spawn fighters around it
            spawnCount = 12;
            spawnSpread = 15f;
            SpawnEntity("Strike Craft", "fighter");

            UnityDebug.Log("[DevMenu] Spawned test fleet: 1 Carrier + 12 Fighters");
        }

        private void SpawnCombatScenario()
        {
            // Player fleet
            _spawnPosition = new float3(-50, 0, 0);
            currentFactionId = "player";
            spawnCount = 1;
            SpawnEntity("Carriers", "carrier_medium");

            spawnCount = 8;
            spawnSpread = 20f;
            SpawnEntity("Strike Craft", "fighter");

            spawnCount = 2;
            SpawnEntity("Capital Ships", "frigate");

            // Enemy fleet
            _spawnPosition = new float3(50, 0, 0);
            currentFactionId = "enemy_pirates";
            spawnCount = 1;
            SpawnEntity("Carriers", "carrier_light");

            spawnCount = 6;
            spawnSpread = 15f;
            SpawnEntity("Strike Craft", "fighter");

            UnityDebug.Log("[DevMenu] Spawned combat scenario: Player vs Pirates");
        }

        private void SpawnMiningOperation()
        {
            _spawnPosition = float3.zero;
            currentFactionId = "player";

            // Carrier
            spawnCount = 1;
            SpawnEntity("Carriers", "carrier_light");

            // Mining vessels
            _spawnPosition = new float3(10, 0, 0);
            spawnCount = 3;
            spawnSpread = 5f;
            SpawnEntity("Support Vessels", "miner");

            // Asteroids
            _spawnPosition = new float3(40, 0, 0);
            spawnCount = 5;
            spawnSpread = 30f;
            SpawnEntity("Celestial", "asteroid_medium");

            UnityDebug.Log("[DevMenu] Spawned mining operation");
        }

        private void ClearAllEntities()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            em.DestroyEntity(em.UniversalQuery);
            UnityDebug.Log("[DevMenu] Cleared all entities");
        }

        private void RefreshEntityList()
        {
            _cachedEntities.Clear();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Query entities with LocalTransform
            var query = em.CreateEntityQuery(typeof(LocalTransform));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                var transform = em.GetComponentData<LocalTransform>(entity);
                string name = "Entity";

                // Try to get a name from various components
                if (em.HasComponent<Registry.Carrier>(entity))
                    name = "Carrier";
                else if (em.HasComponent<Registry.MiningVessel>(entity))
                    name = "Miner";
                else if (em.HasComponent<Registry.Asteroid>(entity))
                    name = "Asteroid";
                else if (em.HasComponent<DevSpawnRequest>(entity))
                {
                    var req = em.GetComponentData<DevSpawnRequest>(entity);
                    name = $"[Spawn] {req.TemplateId}";
                }

                _cachedEntities.Add((entity, name, transform.Position));

                if (_cachedEntities.Count >= 100) break; // Limit for performance
            }

            entities.Dispose();
        }

        private void UpdateSpawnPositionFromMouse()
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            var screenPos = mouse.position.ReadValue();
            Ray ray = camera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            Plane ground = new Plane(Vector3.up, Vector3.zero);

            if (ground.Raycast(ray, out float distance))
            {
                Vector3 point = ray.GetPoint(distance);
                _spawnPosition = new float3(point.x, point.y, point.z);
            }
        }

        private void FocusCameraOnPosition(float3 position)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null) return;

            // Simple camera move - in production, you'd integrate with your camera system
            camera.transform.position = new Vector3(position.x, position.y + 50, position.z - 30);
            camera.transform.LookAt(new Vector3(position.x, position.y, position.z));
        }

        // Helper for bold label style
        private static bool KeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Enum.IsDefined(typeof(Key), key))
                return false;

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private static class EditorStyles
        {
            public static GUIStyle boldLabel => new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        }
    }

    /// <summary>
    /// Component added to entities spawned via dev menu.
    /// Systems will process these and add appropriate components.
    /// </summary>
    public struct DevSpawnRequest : IComponentData
    {
        public Unity.Collections.FixedString32Bytes Category;
        public Unity.Collections.FixedString64Bytes TemplateId;
        public Unity.Collections.FixedString32Bytes FactionId;
        public float3 Position;
    }
}

