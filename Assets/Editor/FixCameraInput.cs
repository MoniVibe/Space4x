using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using Space4X.Registry;

public static class FixCameraInput
{
    [MenuItem("Tools/Fix Camera Input")]
    public static void FixInput()
    {
        // Find the Main Camera
        var camera = Camera.main;
        if (camera == null)
        {
            camera = Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            Debug.LogError("No Camera found in scene!");
            return;
        }

        // Get the authoring component
        var authoring = camera.GetComponent<Space4XCameraInputAuthoring>();
        if (authoring == null)
        {
            Debug.LogError($"Camera '{camera.name}' doesn't have Space4XCameraInputAuthoring component!");
            return;
        }

        // Load the InputActionAsset
        var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (inputAsset == null)
        {
            Debug.LogError("Could not load InputSystem_Actions.inputactions!");
            return;
        }

        // Set it using SerializedObject to properly serialize
        var serialized = new SerializedObject(authoring);
        var inputActionsProp = serialized.FindProperty("inputActions");
        if (inputActionsProp != null)
        {
            inputActionsProp.objectReferenceValue = inputAsset;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(authoring);
            Debug.Log($"Assigned InputActionAsset to {camera.name}'s Space4XCameraInputAuthoring component.");
        }
        else
        {
            Debug.LogError("Could not find 'inputActions' property on Space4XCameraInputAuthoring!");
        }
    }
}

