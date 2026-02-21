using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Space4X.Presentation.Overlay
{
    internal static class Space4XOverlayInput
    {
        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
            return TryGetKey(key, out var keyControl) && keyControl.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool GetKey(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
            return TryGetKey(key, out var keyControl) && keyControl.isPressed;
#else
            return false;
#endif
        }

        public static bool GetMouseButtonDown(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#elif ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            return button switch
            {
                0 => mouse.leftButton.wasPressedThisFrame,
                1 => mouse.rightButton.wasPressedThisFrame,
                2 => mouse.middleButton.wasPressedThisFrame,
                _ => false
            };
#else
            return false;
#endif
        }

        public static bool TryGetMousePosition(out Vector2 position)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return true;
#elif ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                position = default;
                return false;
            }

            position = mouse.position.ReadValue();
            return true;
#else
            position = default;
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryGetKey(KeyCode keyCode, out KeyControl keyControl)
        {
            keyControl = null;
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            if (!TryMapKeyCode(keyCode, out var mapped))
            {
                return false;
            }

            keyControl = keyboard[mapped];
            return keyControl != null;
        }

        private static bool TryMapKeyCode(KeyCode keyCode, out Key mapped)
        {
            mapped = default;
            var name = keyCode.ToString();
            return Enum.TryParse(name, ignoreCase: true, out mapped);
        }
#endif
    }
}
