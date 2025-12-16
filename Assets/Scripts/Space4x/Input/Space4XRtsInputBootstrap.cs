using PureDOTS.Input;
using PureDOTS.Runtime.Core;
using UnityEngine;

namespace Space4X.Input
{
    /// <summary>
    /// Ensures an RTS input bridge exists so selection/right-click orders work.
    /// Skips headless runs.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class Space4XRtsInputBootstrap : MonoBehaviour
    {
        [SerializeField] private LayerMask selectionMask = ~0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (RuntimeMode.IsHeadless)
                return;

            if (FindFirstObjectByType<RtsInputBridge>() != null)
                return;

            var go = new GameObject("RTS Input Bridge");
            var bridge = go.AddComponent<RtsInputBridge>();
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            bridge.RaycastCamera = mainCam;
            bridge.SelectionMask = ~0;
            go.AddComponent<Space4XRtsInputBootstrap>();
            DontDestroyOnLoad(go);
        }
    }
}
