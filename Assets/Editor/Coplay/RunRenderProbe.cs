using UnityEngine;
using UnityEditor;
using Space4x.Diagnostics;

public class RunRenderProbe
{
    public static string Execute()
    {
        // Create a GameObject to hold the probe
        GameObject probeGO = new GameObject("Space4XRenderProbe");
        
        // Add the component
        probeGO.AddComponent<Space4XRenderProbeMono>();
        
        // Run the logging logic immediately to get the output
        Space4XRenderProbeMono.LogDiagnostics();
        
        return "Created Space4XRenderProbe GameObject and ran diagnostics. Check console for output.";
    }
}
