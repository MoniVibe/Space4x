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

        public static Space4XControlMode CurrentMode { get; private set; } = Space4XControlMode.CursorOrient;

        public static void SetMode(Space4XControlMode mode)
        {
            if (CurrentMode == mode)
                return;

            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }

        public static void ResetToDefaultForRun()
        {
            SetMode(Space4XControlMode.CursorOrient);
        }
    }
}
