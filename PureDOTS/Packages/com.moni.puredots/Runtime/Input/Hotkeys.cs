using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace PureDOTS.Input
{
    public static class Hotkeys
    {
        public static bool F6Down() => KeyDown(KeyCode.F6);

        public static bool F7Down() => KeyDown(KeyCode.F7);

        public static bool KeyDown(KeyCode keyCode) => GetKeyState(keyCode, KeyState.Down);

        public static bool KeyUp(KeyCode keyCode) => GetKeyState(keyCode, KeyState.Up);

        public static bool KeyHeld(KeyCode keyCode) => GetKeyState(keyCode, KeyState.Held);

        private enum KeyState
        {
            Down,
            Up,
            Held
        }

        private static bool GetKeyState(KeyCode keyCode, KeyState state)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (!TryGetKeyControl(keyboard, keyCode, out var control))
                return false;

            return state switch
            {
                KeyState.Down => control.wasPressedThisFrame,
                KeyState.Up => control.wasReleasedThisFrame,
                KeyState.Held => control.isPressed,
                _ => false
            };
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryGetKeyControl(Keyboard keyboard, KeyCode keyCode, out KeyControl control)
        {
            control = null;
            if (keyCode == KeyCode.None)
                return false;

            if (Enum.TryParse(keyCode.ToString(), out Key mappedKey))
            {
                control = keyboard[mappedKey];
                if (control != null)
                    return true;
            }

            // Handle common aliases that do not match enum names exactly.
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    control = keyboard.enterKey;
                    return true;
                case KeyCode.LeftArrow:
                    control = keyboard.leftArrowKey;
                    return true;
                case KeyCode.RightArrow:
                    control = keyboard.rightArrowKey;
                    return true;
                case KeyCode.UpArrow:
                    control = keyboard.upArrowKey;
                    return true;
                case KeyCode.DownArrow:
                    control = keyboard.downArrowKey;
                    return true;
            }

            return false;
        }
#endif
    }
}
