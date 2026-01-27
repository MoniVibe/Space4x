using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Buffered debug command routed from DOTS systems to presentation bridges.
    /// </summary>
    public struct DebugCommand : IBufferElementData
    {
        public enum CommandType : byte
        {
            ToggleHUD = 0,
            ShowHUD = 1,
            HideHUD = 2,
            ClearStreamingCooldowns = 3,
            ReloadPresentation = 4
        }

        public CommandType Type;
    }

    /// <summary>
    /// Tag identifying the singleton entity that owns the debug command buffer.
    /// </summary>
    public struct DebugCommandSingletonTag : IComponentData
    {
    }
}
