using PureDOTS.Runtime.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Space4X.Presentation
{
    /// <summary>
    /// Ensures the battle report overlay exists at runtime (no scene edits required).
    /// Mirrors the pattern used by <see cref="Space4X.Camera.Space4XCameraBootstrap"/>.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public sealed class Space4XBattleReportOverlayBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureOnLoad()
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (UObject.FindFirstObjectByType<Space4XBattleReportOverlayBootstrap>() != null ||
                UObject.FindFirstObjectByType<Space4XBattleReportOverlay>() != null)
            {
                return;
            }

            var go = new GameObject("Space4X Battle Report Overlay (Runtime)");
            go.AddComponent<Space4XBattleReportOverlayBootstrap>();
            go.AddComponent<Space4XBattleReportOverlay>();
            UObject.DontDestroyOnLoad(go);
        }
    }
}

