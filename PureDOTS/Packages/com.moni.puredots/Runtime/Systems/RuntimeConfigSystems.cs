using PureDOTS.Runtime.Config;
using PureDOTS.Runtime.Debugging;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Bootstraps runtime configuration registry and ensures the console is available.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [CreateAfter(typeof(CoreSingletonBootstrapSystem))]
    public sealed partial class RuntimeConfigBootstrapSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RuntimeConfigRegistry.Initialize();
            RuntimeDebugConsole.EnsureConsole();
            RuntimeDebugConsole.EnsureOverlay();
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            RuntimeConfigRegistry.SaveIfDirty();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // no-op; this system only runs in OnCreate/OnDestroy.
        }
    }
}


