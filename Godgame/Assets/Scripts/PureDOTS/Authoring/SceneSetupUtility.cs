using PureDOTS.Authoring;
using PureDOTS.Presentation.Runtime;
using UnityEditor;
using UnityEngine;

namespace PureDOTS.Editor
{
    public static class SceneSetupUtility
    {
        [MenuItem("PureDOTS/Setup Template Scene", priority = 10)]
        public static void SetupTemplateScene()
        {
            // Ensure global runtime config object exists
            var configObj = GameObject.FindObjectOfType<PureDotsConfigAuthoring>();
            if (configObj == null)
            {
                var go = new GameObject("PureDotsConfig");
                go.AddComponent<PureDotsConfigAuthoring>();
            }

            // Ensure time controls object exists
            if (GameObject.FindObjectOfType<TimeControlsAuthoring>() == null)
            {
                var go = new GameObject("TimeControls");
                go.AddComponent<TimeControlsAuthoring>();
            }

            // Ensure hand authoring exists (DOTS singleton seed)
            if (GameObject.FindObjectOfType<HandAuthoring>() == null)
            {
                var go = new GameObject("DivineHand");
                go.AddComponent<HandAuthoring>();
            }

            // Ensure hybrid bridge exists
            if (GameObject.FindObjectOfType<HandInputBridge>() == null)
            {
                new GameObject("HandInputBridge").AddComponent<HandInputBridge>();
            }
        }
    }
}


