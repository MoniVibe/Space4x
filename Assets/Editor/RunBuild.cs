using UnityEditor;
using UnityEngine;
using Space4X.Headless.Editor;

public class RunBuild
{
    public static void Build()
    {
        try
        {
            Space4XHeadlessBuilder.BuildLinuxHeadless();
            Debug.Log("Build Succeeded!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Build Failed: {e.Message}");
        }
    }
}
