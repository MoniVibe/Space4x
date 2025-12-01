#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
static class RenderDiagEarly
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void Log()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        var name = rp ? rp.GetType().Name : "<null>";
        Debug.Log($"[RenderDiagEarly] SRP:{name} API:{SystemInfo.graphicsDeviceType} Compute:{SystemInfo.supportsComputeShaders}");
    }
}
#endif
