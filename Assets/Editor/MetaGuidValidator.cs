#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MetaGuidValidator
{
    private static readonly Regex GuidLine = new Regex(@"^guid:\s*([0-9a-f]{32})\s*$", RegexOptions.Compiled);

    [MenuItem("Tools/PureDOTS/Validate .meta GUIDs (warn)")]
    public static void ValidateMetas()
    {
        var metaPaths = Directory.GetFiles(Application.dataPath, "*.meta", SearchOption.AllDirectories);
        int bad = 0;

        foreach (var abs in metaPaths)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(abs);
            }
            catch
            {
                bad++;
                Debug.LogError($"[MetaGuidValidator] Cannot read: {abs}");
                continue;
            }

            bool hasGuid = false;
            foreach (var l in lines)
            {
                if (l.StartsWith("<<<<<<<") || l.StartsWith(">>>>>>>") || l.StartsWith("======="))
                {
                    bad++;
                    Debug.LogError($"[MetaGuidValidator] Merge markers found in meta: {ToProjectPath(abs)}");
                    hasGuid = true;
                    break;
                }

                if (GuidLine.IsMatch(l))
                {
                    hasGuid = true;
                    break;
                }
            }

            if (!hasGuid)
            {
                bad++;
                Debug.LogError($"[MetaGuidValidator] Missing/invalid guid line: {ToProjectPath(abs)}");
            }
        }

        Debug.Log($"[MetaGuidValidator] Finished. Bad meta files: {bad}");
    }

    private static string ToProjectPath(string abs)
    {
        abs = abs.Replace('\\', '/');
        var idx = abs.IndexOf("/Assets/");
        return idx >= 0 ? abs.Substring(idx + 1) : abs;
    }
}
#endif
