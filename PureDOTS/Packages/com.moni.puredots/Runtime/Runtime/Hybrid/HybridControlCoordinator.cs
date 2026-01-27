using System;
using UnityEngine;

#nullable enable

namespace PureDOTS.Runtime.Hybrid
{
    /// <summary>
    /// Centralizes runtime intent for the hybrid showcase so both games can cooperate on shared inputs.
    /// </summary>
    public static class HybridControlCoordinator
    {
        public enum InputMode
        {
            Dual,          // Both control schemes active (default in non-showcase scenes)
            GodgameOnly,   // Disable Space4X camera input to spotlight divine hand controls
            Space4XOnly    // Disable Godgame hand input to spotlight RTS camera controls
        }

        private static InputMode s_mode = InputMode.Dual;

        public static event Action<InputMode>? ModeChanged;

        public static InputMode Mode
        {
            get => s_mode;
            set
            {
                if (s_mode == value)
                {
                    return;
                }

                s_mode = value;
                ModeChanged?.Invoke(s_mode);

                Debug.Log($"[HybridControlCoordinator] Input mode switched to {s_mode}.");
            }
        }

        public static bool Space4XInputEnabled => s_mode != InputMode.GodgameOnly;

        public static bool GodgameInputEnabled => s_mode != InputMode.Space4XOnly;

        public static void CycleMode()
        {
            switch (s_mode)
            {
                case InputMode.Dual:
                    Mode = InputMode.Space4XOnly;
                    break;
                case InputMode.Space4XOnly:
                    Mode = InputMode.GodgameOnly;
                    break;
                default:
                    Mode = InputMode.Dual;
                    break;
            }
        }
    }
}


