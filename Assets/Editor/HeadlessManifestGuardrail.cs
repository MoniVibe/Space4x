#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class HeadlessManifestGuardrail
{
    static HeadlessManifestGuardrail()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string packagesDir = Path.Combine(projectRoot, "Packages");
        bool hasIssue = false;

        if (File.Exists(Path.Combine(packagesDir, "manifest.json.bak")))
            hasIssue = true;
        if (File.Exists(Path.Combine(packagesDir, "packages-lock.json.bak")))
            hasIssue = true;
        if (Directory.Exists(Path.Combine(packagesDir, "Coplay.disabled")))
            hasIssue = true;

        if (hasIssue)
        {
            Debug.LogError(
                "*** HEADLESS MANIFEST LEFTOVERS DETECTED ***\n" +
                "Packages/manifest.json.bak, packages-lock.json.bak, or Coplay.disabled exist.\n" +
                "Run: ./restore_manifest.sh (from project root)\n" +
                "Then close Unity and reopen to use the restored manifest.");
        }
    }
}
#endif
