using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Single input frame per tick for divine hand interaction.
    /// Produced by HandInputCollectorSystem (non-Burst) and consumed by Burst systems.
    /// All fields are blittable for Burst compatibility.
    /// </summary>
    public struct HandInputFrame : IComponentData
    {
        public uint SampleId;
        public float2 CursorScreenPos;
        public float3 RayOrigin;  // World ray origin (blittable)
        public float3 RayDirection;  // World ray direction (normalized, blittable)
        public bool RmbPressed;
        public bool RmbHeld;
        public bool RmbReleased;
        public bool LmbPressed;
        public bool LmbHeld;
        public bool LmbReleased;
        public bool ShiftHeld;
        public bool CtrlHeld;
        public bool AltHeld;
        public bool ReleaseOnePressed;  // Hotkey edge for queue release one (mapped from Key in input collector)
        public bool ReleaseAllPressed;  // Hotkey edge for queue release all (mapped from Key in input collector)
        public float ScrollDelta;  // Mouse wheel for hold distance
        public bool CancelAction;
        public bool ToggleThrowMode;
    }
}

