using UnityEditor;
using Unity.Burst;
using UnityEngine;

public class DisableBurstScript
{
    public static void Execute()
    {
        BurstCompiler.Options.EnableBurstCompilation = false;
        BurstCompiler.Options.EnableBurstSafetyChecks = false;
        Debug.Log("Burst Compilation and Safety Checks have been disabled.");
    }
}
