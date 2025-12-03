using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    /// <summary>
    /// Quick fix for the DualMiningDemo.unity nested folder issue.
    /// This menu item will delete the problematic folder so the scene can be saved.
    /// </summary>
    public static class FixNestedFolder
    {
        [MenuItem("Tools/Space4X/Fix: Delete DualMiningDemo.unity Folder")]
        public static void DeleteNestedFolder()
        {
            var directoryPath = Path.Combine(Application.dataPath.Replace("Assets", ""), "Assets/Scenes/Demo/DualMiningDemo.unity");
            
            if (!Directory.Exists(directoryPath))
            {
                Debug.Log("✓ No nested folder found - everything is fine!");
                EditorUtility.DisplayDialog("Clean", 
                    "No problematic folder found. The scene should save correctly.",
                    "OK");
                return;
            }
            
            Debug.LogWarning($"Found DualMiningDemo.unity as a DIRECTORY at: {directoryPath}");
            Debug.LogWarning("This prevents Unity from saving a file with the same name.");
            
            bool confirmed = EditorUtility.DisplayDialog("Delete Nested Folder?",
                $"Found DualMiningDemo.unity as a directory:\n{directoryPath}\n\n" +
                "This prevents saving the scene file.\n\n" +
                "Would you like to delete this folder?\n\n" +
                "WARNING: This will delete all contents of that folder!",
                "Yes, Delete It",
                "Cancel");
            
            if (!confirmed)
            {
                Debug.Log("Cancelled. The folder was not deleted.");
                return;
            }
            
            try
            {
                // Delete directory
                Directory.Delete(directoryPath, true);
                Debug.Log($"✓ Deleted directory: {directoryPath}");
                
                // Also delete the .meta file if it exists
                var metaPath = directoryPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                    Debug.Log($"✓ Deleted meta file: {metaPath}");
                }
                
                AssetDatabase.Refresh();
                
                Debug.Log("✓ Successfully cleaned up nested folder!");
                EditorUtility.DisplayDialog("Success", 
                    "Nested folder deleted successfully!\n\n" +
                    "Now run 'Tools → Space4X → Setup Dual Mining Demo Scene' again.",
                    "OK");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogError($"✗ Access Denied: {ex.Message}");
                Debug.LogError("\nSOLUTION:");
                Debug.LogError("1. Close ALL Unity instances");
                Debug.LogError("2. Close File Explorer if it has that folder open");
                Debug.LogError($"3. Manually delete: {directoryPath}");
                Debug.LogError("4. Then run the setup menu item again");
                
                EditorUtility.DisplayDialog("Access Denied",
                    $"Cannot delete the folder - Access Denied.\n\n" +
                    $"Please manually delete:\n{directoryPath}\n\n" +
                    "Close Unity and File Explorer first, then delete the folder.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"✗ Failed to delete folder: {ex.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"Failed to delete folder:\n{ex.Message}",
                    "OK");
            }
        }
    }
}




























