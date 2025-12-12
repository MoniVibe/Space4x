#if UNITY_EDITOR
using UnityEditor;

namespace Space4X.DebugSystems
{
    /// <summary>
    /// Simple editor-only toggles to trigger one-shot debug dumps from ECS systems.
    /// </summary>
    static class Space4XDebugSettings
    {
        static bool _renderSnapshotRequested;
        static bool _miningSnapshotRequested;

        const string RenderMenuPath = "Space4X/Debug/Log Render Snapshot";
        const string MiningMenuPath = "Space4X/Debug/Log Mining Snapshot";

        [MenuItem(RenderMenuPath)]
        static void RequestRenderSnapshot()
        {
            _renderSnapshotRequested = true;
            UnityEngine.Debug.Log("[Space4X Debug] Render snapshot will be logged on next simulation update.");
        }

        [MenuItem(MiningMenuPath)]
        static void RequestMiningSnapshot()
        {
            _miningSnapshotRequested = true;
            UnityEngine.Debug.Log("[Space4X Debug] Mining snapshot will be logged on next simulation update.");
        }

        [MenuItem(RenderMenuPath, true)]
        static bool ValidateRenderMenu()
        {
            Menu.SetChecked(RenderMenuPath, _renderSnapshotRequested);
            return true;
        }

        [MenuItem(MiningMenuPath, true)]
        static bool ValidateMiningMenu()
        {
            Menu.SetChecked(MiningMenuPath, _miningSnapshotRequested);
            return true;
        }

        public static bool TryConsumeRenderSnapshotRequest()
        {
            if (!_renderSnapshotRequested)
                return false;

            _renderSnapshotRequested = false;
            Menu.SetChecked(RenderMenuPath, false);
            return true;
        }

        public static bool TryConsumeMiningSnapshotRequest()
        {
            if (!_miningSnapshotRequested)
                return false;

            _miningSnapshotRequested = false;
            Menu.SetChecked(MiningMenuPath, false);
            return true;
        }
    }
}
#endif
