using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Optional input handler for debug commands.
    /// Sends debug commands to DOTS command buffer for processing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugInputHandler : MonoBehaviour
    {
        [Header("Keyboard Shortcuts")]
        [Tooltip("Key to toggle debug HUD visibility")]
        public KeyCode toggleHUDKey = KeyCode.F1;

        [Tooltip("Key to show debug HUD")]
        public KeyCode showHUDKey = KeyCode.F2;

        [Tooltip("Key to hide debug HUD")]
        public KeyCode hideHUDKey = KeyCode.F3;

        [Tooltip("Key to clear streaming cooldowns")]
        public KeyCode clearStreamingCooldownsKey = KeyCode.F4;

        [Tooltip("Key to reload all presentation visuals")]
        public KeyCode reloadPresentationKey = KeyCode.F8;

        [Header("Settings")]
        [Tooltip("Enable keyboard shortcuts")]
        public bool enableKeyboardShortcuts = true;

        private World _world;
        private EntityQuery _commandQuery;
        private bool _hasCommandQuery;
        private bool _warnedMissingWorld;

        private void Awake()
        {
            InitializeWorld();
        }

        private void OnEnable()
        {
            // Reinitialize on world reload
            InitializeWorld();
        }

        private void InitializeWorld()
        {
            var newWorld = World.DefaultGameObjectInjectionWorld;
            
            if (newWorld == null || !newWorld.IsCreated)
            {
                if (!_warnedMissingWorld)
                {
                    Debug.LogWarning("DebugInputHandler did not find a DefaultGameObjectInjectionWorld.", this);
                    _warnedMissingWorld = true;
                }
                if (_hasCommandQuery)
                {
                    _commandQuery.Dispose();
                    _hasCommandQuery = false;
                }
                _world = null;
                return;
            }

            // Dispose old query if world changed
            if (_world != null && _world != newWorld)
            {
                if (_hasCommandQuery)
                {
                    _commandQuery.Dispose();
                    _hasCommandQuery = false;
                }
            }

            _world = newWorld;
            _warnedMissingWorld = false;

            // Cache query for new world
            var entityManager = _world.EntityManager;
            _commandQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DebugCommandSingletonTag>());
            _hasCommandQuery = true;
        }

        private void OnDestroy()
        {
            // Dispose cached query (EntityQuery is a struct, check IsCreated)
            if (_hasCommandQuery)
            {
                _commandQuery.Dispose();
                _hasCommandQuery = false;
            }
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts)
            {
                return;
            }

            if (_world == null || !_world.IsCreated)
            {
                InitializeWorld();
                if (_world == null || !_world.IsCreated)
                {
                    return;
                }
            }

            if (_commandQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = _commandQuery.GetSingletonEntity();
            var entityManager = _world.EntityManager;
            if (!entityManager.HasBuffer<DebugCommand>(entity))
            {
                return;
            }

            var commands = entityManager.GetBuffer<DebugCommand>(entity);

            // Check for keyboard input
            if (WasPressed(toggleHUDKey))
            {
                commands.Add(new DebugCommand { Type = DebugCommand.CommandType.ToggleHUD });
            }
            else if (WasPressed(showHUDKey))
            {
                commands.Add(new DebugCommand { Type = DebugCommand.CommandType.ShowHUD });
            }
            else if (WasPressed(hideHUDKey))
            {
                commands.Add(new DebugCommand { Type = DebugCommand.CommandType.HideHUD });
            }
            else if (WasPressed(clearStreamingCooldownsKey))
            {
                commands.Add(new DebugCommand { Type = DebugCommand.CommandType.ClearStreamingCooldowns });
            }
            else if (WasPressed(reloadPresentationKey))
            {
                commands.Add(new DebugCommand { Type = DebugCommand.CommandType.ReloadPresentation });
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static bool WasPressed(KeyCode keyCode)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return keyCode switch
            {
                KeyCode.F1 => keyboard.f1Key.wasPressedThisFrame,
                KeyCode.F2 => keyboard.f2Key.wasPressedThisFrame,
                KeyCode.F3 => keyboard.f3Key.wasPressedThisFrame,
                KeyCode.F4 => keyboard.f4Key.wasPressedThisFrame,
                KeyCode.F5 => keyboard.f5Key.wasPressedThisFrame,
                KeyCode.F6 => keyboard.f6Key.wasPressedThisFrame,
                KeyCode.F7 => keyboard.f7Key.wasPressedThisFrame,
                KeyCode.F8 => keyboard.f8Key.wasPressedThisFrame,
                _ => false
            };
        }
#else
        private static bool WasPressed(KeyCode keyCode) => false;
#endif
    }
}
