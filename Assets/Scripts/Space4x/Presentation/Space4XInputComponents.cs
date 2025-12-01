using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    // ============================================================================
    // Selection Input Components
    // ============================================================================

    /// <summary>
    /// Selection input state written by the input bridge each frame.
    /// Singleton component that receives input from the Input System bridge.
    /// </summary>
    public struct SelectionInput : IComponentData
    {
        /// <summary>Screen position of click (normalized 0-1)</summary>
        public float2 ClickPosition;
        /// <summary>True if click was pressed this frame</summary>
        public bool ClickPressed;
        /// <summary>True if click is currently held</summary>
        public bool ClickHeld;
        /// <summary>Screen position where box selection started</summary>
        public float2 BoxStart;
        /// <summary>Current screen position for box selection end</summary>
        public float2 BoxEnd;
        /// <summary>True if box selection is active</summary>
        public bool BoxActive;
        /// <summary>True if shift is held (for multi-select)</summary>
        public bool ShiftHeld;
        /// <summary>True if deselect was requested this frame</summary>
        public bool DeselectRequested;
    }

    /// <summary>
    /// Command input state written by the input bridge each frame.
    /// Singleton component for game commands.
    /// </summary>
    public struct CommandInput : IComponentData
    {
        /// <summary>True if cycle fleets was pressed this frame</summary>
        public bool CycleFleetsPressed;
        /// <summary>True if toggle overlays was pressed this frame</summary>
        public bool ToggleOverlaysPressed;
        /// <summary>True if debug view was pressed this frame</summary>
        public bool DebugViewPressed;
        /// <summary>True if pause was pressed this frame</summary>
        public bool PausePressed;
        /// <summary>True if speed up was pressed this frame</summary>
        public bool SpeedUpPressed;
        /// <summary>True if speed down was pressed this frame</summary>
        public bool SpeedDownPressed;
        /// <summary>True if move command was issued (right-click on ground)</summary>
        public bool IssueMoveCommand;
        /// <summary>True if attack command was issued (right-click on enemy)</summary>
        public bool IssueAttackCommand;
        /// <summary>True if mine command was issued (right-click on asteroid)</summary>
        public bool IssueMineCommand;
        /// <summary>True if cancel command was pressed</summary>
        public bool CancelCommand;
        /// <summary>World position for command target (from right-click)</summary>
        public float3 CommandTargetPosition;
        /// <summary>Entity target for command (from right-click)</summary>
        public Entity CommandTargetEntity;
    }

    // ============================================================================
    // Selection State Components
    // ============================================================================

    /// <summary>
    /// Current selection state. Singleton component.
    /// </summary>
    public struct SelectionState : IComponentData
    {
        /// <summary>Number of currently selected entities</summary>
        public int SelectedCount;
        /// <summary>Primary selected entity (for inspection)</summary>
        public Entity PrimarySelected;
        /// <summary>Selection type (single, multi, box)</summary>
        public SelectionType Type;
    }

    /// <summary>
    /// Selection type enumeration.
    /// </summary>
    public enum SelectionType : byte
    {
        None = 0,
        Single = 1,
        Multi = 2,
        Box = 3
    }

    // ============================================================================
    // Debug Overlay Components
    // ============================================================================

    /// <summary>
    /// Debug overlay configuration. Singleton component.
    /// </summary>
    public struct DebugOverlayConfig : IComponentData
    {
        /// <summary>Show resource field colors on asteroids</summary>
        public bool ShowResourceFields;
        /// <summary>Show faction influence zones</summary>
        public bool ShowFactionZones;
        /// <summary>Show logistics routes</summary>
        public bool ShowLogisticsOverlay;
        /// <summary>Show debug paths for formations</summary>
        public bool ShowDebugPaths;
        /// <summary>Show LOD level visualization</summary>
        public bool ShowLODVisualization;
        /// <summary>Show entity count and metrics</summary>
        public bool ShowMetrics;
        /// <summary>Show selected entity inspector</summary>
        public bool ShowInspector;
    }

    /// <summary>
    /// Selected entity info for UI display.
    /// </summary>
    public struct SelectedEntityInfo : IComponentData
    {
        /// <summary>Entity type (Carrier, Craft, Asteroid, Fleet)</summary>
        public SelectedEntityType EntityType;
        /// <summary>Entity world position</summary>
        public float3 Position;
        /// <summary>Entity faction color</summary>
        public float4 FactionColor;
        /// <summary>Entity health/resources (context-dependent)</summary>
        public float PrimaryValue;
        /// <summary>Secondary value (cargo, resources, etc.)</summary>
        public float SecondaryValue;
    }

    /// <summary>
    /// Selected entity type enumeration.
    /// </summary>
    public enum SelectedEntityType : byte
    {
        None = 0,
        Carrier = 1,
        Craft = 2,
        Asteroid = 3,
        Fleet = 4,
        Colony = 5,
        Station = 6
    }

    // ============================================================================
    // Camera Input Extension Components
    // ============================================================================

    /// <summary>
    /// Extended camera input for selection-related camera behavior.
    /// </summary>
    public struct CameraSelectionInput : IComponentData
    {
        /// <summary>Request to focus camera on selected entity</summary>
        public bool FocusOnSelectionRequested;
        /// <summary>Request to follow selected entity</summary>
        public bool FollowSelectionRequested;
        /// <summary>Entity to focus/follow</summary>
        public Entity TargetEntity;
    }
}

