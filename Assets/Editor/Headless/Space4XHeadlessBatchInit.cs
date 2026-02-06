#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
internal static class Space4XHeadlessBatchInit
{
    static Space4XHeadlessBatchInit()
    {
        if (!InternalEditorUtility.inBatchMode)
        {
            return;
        }

        GraphicsSettings.defaultRenderPipeline = null;
        QualitySettings.renderPipeline = null;
        Debug.Log("[Space4XHeadlessBatchInit] Cleared render pipeline assets for batchmode.");
    }
}
#endif
