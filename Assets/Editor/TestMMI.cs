using UnityEngine;
using Unity.Rendering;

public class TestMMI
{
    public static void Execute()
    {
        var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);
        Debug.Log($"MMI(0,0) -> Material: {mmi.Material}, Mesh: {mmi.Mesh}");
        
        var mmi2 = MaterialMeshInfo.FromRenderMeshArrayIndices(1, 1);
        Debug.Log($"MMI(1,1) -> Material: {mmi2.Material}, Mesh: {mmi2.Mesh}");
    }
}
