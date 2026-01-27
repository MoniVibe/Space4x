using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Gameplay intent derived from device-level input.
    /// Maps raw input (buttons, axes) to gameplay actions (pan, zoom, select, etc.).
    /// Single-writer: only IntentMappingSystem writes this.
    /// </summary>
    public struct GodIntent : IComponentData
    {
        public uint LastUpdateTick;
        public byte PlayerId;               // Player identifier (0 = default/single-player)

        // Camera intents
        public float2 PanIntent;            // Pan delta (world-space)
        public float ZoomIntent;            // Zoom delta (positive = zoom in)
        public float2 OrbitIntent;          // Orbit delta (yaw/pitch)
        public byte StartPan;               // Flag: start panning
        public byte StopPan;                // Flag: stop panning
        public byte StartOrbit;             // Flag: start orbiting
        public byte StopOrbit;              // Flag: stop orbiting
        public float2 FreeMoveIntent;       // WASD planar movement
        public float VerticalMoveIntent;    // Z/X vertical translation
        public byte CameraYAxisUnlocked;    // Whether movement follows camera orientation

        // Hand intents
        public byte StartSelect;            // Flag: start selection (grab)
        public byte ConfirmPlace;           // Flag: confirm placement (release with charge)
        public byte CancelAction;           // Flag: cancel current action
        public float3 SelectPosition;       // World-space selection position
        public Entity SelectTarget;         // Entity being selected/hovered

        // Multi-select (future)
        public byte StartMultiSelect;       // Flag: start box selection
        public byte UpdateMultiSelect;      // Flag: update box selection
        public byte ConfirmMultiSelect;     // Flag: confirm box selection
        public float2 MultiSelectStart;     // Screen-space box start
        public float2 MultiSelectEnd;       // Screen-space box end
    }
}
