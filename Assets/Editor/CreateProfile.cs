using UnityEngine;
using UnityEditor;
using PureDOTS.Authoring;

public class CreateProfile
{
    public static void Execute()
    {
        var profile = ScriptableObject.CreateInstance<SpatialPartitionProfile>();
        // Set some default values if needed, but defaults in class should be fine
        // profile.SetWorldBounds(Vector3.zero, new Vector3(512, 128, 512));
        
        if (!System.IO.Directory.Exists("Assets/Data"))
        {
            System.IO.Directory.CreateDirectory("Assets/Data");
        }

        AssetDatabase.CreateAsset(profile, "Assets/Data/DefaultSpatialPartitionProfile.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Created SpatialPartitionProfile at Assets/Data/DefaultSpatialPartitionProfile.asset");
    }
}
