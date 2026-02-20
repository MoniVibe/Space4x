using System;

namespace Space4X.UI
{
    public enum Space4XControlMode : byte
    {
        CursorOrient = 0,
        CruiseLook = 1,
        Rts = 2
    }

    public static class Space4XControlModeState
    {
        public static event Action<Space4XControlMode> ModeChanged;
        public static event Action<Space4XControlMode, bool> ModeVariantChanged;

        public static Space4XControlMode CurrentMode { get; private set; } = Space4XControlMode.CursorOrient;
        private static bool _cursorOrientVariantEnabled;
        private static bool _cruiseLookVariantEnabled;
        private static bool _rtsVariantEnabled;

        public static void SetMode(Space4XControlMode mode)
        {
            if (CurrentMode == mode)
                return;

            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }

        public static bool SetModeOrToggleVariant(Space4XControlMode mode)
        {
            if (CurrentMode == mode)
            {
                ToggleVariant(mode);
                return false;
            }

            SetMode(mode);
            return true;
        }

        public static bool IsVariantEnabled(Space4XControlMode mode)
        {
            return mode switch
            {
                Space4XControlMode.CursorOrient => _cursorOrientVariantEnabled,
                Space4XControlMode.CruiseLook => _cruiseLookVariantEnabled,
                Space4XControlMode.Rts => _rtsVariantEnabled,
                _ => false
            };
        }

        public static void SetVariantEnabled(Space4XControlMode mode, bool enabled)
        {
            switch (mode)
            {
                case Space4XControlMode.CursorOrient:
                    if (_cursorOrientVariantEnabled == enabled) return;
                    _cursorOrientVariantEnabled = enabled;
                    break;
                case Space4XControlMode.CruiseLook:
                    if (_cruiseLookVariantEnabled == enabled) return;
                    _cruiseLookVariantEnabled = enabled;
                    break;
                case Space4XControlMode.Rts:
                    if (_rtsVariantEnabled == enabled) return;
                    _rtsVariantEnabled = enabled;
                    break;
                default:
                    return;
            }

            ModeVariantChanged?.Invoke(mode, enabled);
        }

        public static void ToggleVariant(Space4XControlMode mode)
        {
            SetVariantEnabled(mode, !IsVariantEnabled(mode));
        }

        public static void ResetToDefaultForRun()
        {
            SetVariantEnabled(Space4XControlMode.CursorOrient, false);
            SetVariantEnabled(Space4XControlMode.CruiseLook, false);
            SetVariantEnabled(Space4XControlMode.Rts, false);
            SetMode(Space4XControlMode.CursorOrient);
        }
    }
}
