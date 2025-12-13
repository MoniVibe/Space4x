#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
using PureDOTS.Runtime.Core;
static class RenderDiagEarly
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void Log()
    {
        if (RuntimeMode.IsHeadless)
            return;
        if (Application.platform == RuntimePlatform.LinuxPlayer)
            return;

        var rp = GraphicsSettings.currentRenderPipeline;
        var name = rp ? rp.GetType().Name : "<null>";
        Debug.Log($"[RenderDiagEarly] SRP:{name} API:{SystemInfo.graphicsDeviceType} Compute:{SystemInfo.supportsComputeShaders}");
    }
}
#endif
