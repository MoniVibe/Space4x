using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Coplay.Controllers.Functions;

public static class SetupSpace4XURP
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        const string targetAssetPath = "Assets/Resources/Rendering/Space4XURP.asset";
        const string sourceAssetPath = "Assets/Resources/Rendering/ScenarioURP.asset";

        Directory.CreateDirectory(Path.GetDirectoryName(targetAssetPath)!);

        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(targetAssetPath);
        if (urpAsset == null)
        {
            var sourceAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(sourceAssetPath);
            if (sourceAsset != null)
            {
                if (AssetDatabase.CopyAsset(sourceAssetPath, targetAssetPath))
                {
                    sb.AppendLine($"Copied URP asset from {sourceAssetPath} to {targetAssetPath}.");
                    urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(targetAssetPath);
                }
                else
                {
                    sb.AppendLine("Failed to copy ScenarioURP asset; creating a fresh URP asset.");
                }
            }

            if (urpAsset == null)
            {
                urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(urpAsset, targetAssetPath);
                sb.AppendLine($"Created new URP asset at {targetAssetPath}.");
            }
        }
        else
        {
            sb.AppendLine("Space4XURP asset already existed; reusing it.");
        }

        if (urpAsset == null)
        {
            throw new IOException("Unable to obtain or create Space4XURP asset.");
        }

        GraphicsSettings.defaultRenderPipeline = urpAsset;
        sb.AppendLine("Assigned GraphicsSettings.defaultRenderPipeline.");

        var qualityNames = QualitySettings.names;
        QualitySettings.renderPipeline = urpAsset;
        sb.AppendLine($"Assigned URP asset to active quality level '{QualitySettings.names[QualitySettings.GetQualityLevel()]}' (total defined: {qualityNames.Length}).");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return sb.ToString();
    }
}
