#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
internal static class HybridRendererDefineGuard
{
    private const string DisabledDefine = "HYBRID_RENDERER_DISABLED";

    static HybridRendererDefineGuard()
    {
        EditorApplication.delayCall += () => EnsureDisabledDefineRemoved(false);
    }

    [MenuItem("Space4X/Tools/Rendering/Clear HYBRID_RENDERER_DISABLED")]
    private static void ForceClearDisabledDefine()
    {
        EnsureDisabledDefineRemoved(forceLog: true);
    }

    private static void EnsureDisabledDefineRemoved(bool forceLog = false)
    {
        if (Application.isBatchMode)
        {
            return;
        }

        var changed = false;

#pragma warning disable CS0618
        var standaloneGroupDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
        var updatedStandaloneGroupDefines = RemoveDefine(standaloneGroupDefines, DisabledDefine);
        if (!string.Equals(standaloneGroupDefines, updatedStandaloneGroupDefines, StringComparison.Ordinal))
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, updatedStandaloneGroupDefines);
            changed = true;
        }
#pragma warning restore CS0618

        if (changed)
        {
            Debug.Log("[HybridRendererDefineGuard] Removed HYBRID_RENDERER_DISABLED from standalone scripting define symbols.");
            CompilationPipeline.RequestScriptCompilation();
            return;
        }

        if (forceLog)
        {
            Debug.Log("[HybridRendererDefineGuard] HYBRID_RENDERER_DISABLED not present.");
        }
    }

    private static string RemoveDefine(string defines, string defineToRemove)
    {
        if (string.IsNullOrWhiteSpace(defines))
        {
            return string.Empty;
        }

        var tokens = defines
            .Split(';')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        var changed = false;
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(tokens[i], defineToRemove, StringComparison.Ordinal))
            {
                continue;
            }

            tokens.RemoveAt(i);
            changed = true;
        }

        if (!changed)
        {
            return defines;
        }

        return string.Join(";", tokens);
    }
}
#endif
