#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Scenes;

namespace Godgame.Editor
{
    public static class GodgameSubSceneUtility
    {
        private const string SubSceneObjectName = "Godgame DOTS SubScene";
        private const string SubSceneAssetPath = "Assets/Scenes/GodgameBootstrapSubScene.unity";

        public static void LinkGodgameSubScene()
        {
            var subSceneGo = GameObject.Find(SubSceneObjectName);
            if (subSceneGo == null)
            {
                Debug.LogError($"[{nameof(GodgameSubSceneUtility)}] Could not find GameObject '{SubSceneObjectName}' in the open scene.");
                return;
            }

            var subScene = subSceneGo.GetComponent<SubScene>();
            if (subScene == null)
            {
                Debug.LogError($"[{nameof(GodgameSubSceneUtility)}] GameObject '{SubSceneObjectName}' has no SubScene component." );
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubSceneAssetPath);
            if (sceneAsset == null)
            {
                Debug.LogError($"[{nameof(GodgameSubSceneUtility)}] Could not load SceneAsset at '{SubSceneAssetPath}'.");
                return;
            }

            subScene.SceneAsset = sceneAsset;
            EditorUtility.SetDirty(subScene);
            Debug.Log($"[{nameof(GodgameSubSceneUtility)}] Linked '{SubSceneAssetPath}' to SubScene '{SubSceneObjectName}'.");
        }

        public static void ConfigureTelemetryHud()
        {
            var canvas = GameObject.Find("Godgame HUD Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[{nameof(GodgameSubSceneUtility)}] Could not find Godgame HUD Canvas in the scene.");
                return;
            }

            var hud = canvas.GetComponent<Godgame.Debugging.GodgameTelemetryHUD>();
            if (hud == null)
            {
                Debug.LogError($"[{nameof(GodgameSubSceneUtility)}] GodgameTelemetryHUD component not found on canvas.");
                return;
            }

            var villager = FindText(canvas.transform, "Godgame HUD Panel/VillagerSummaryText");
            var storehouse = FindText(canvas.transform, "Godgame HUD Panel/StorehouseSummaryText");
            var telemetry = FindText(canvas.transform, "Godgame HUD Panel/TelemetrySummaryText");

            var serializedHud = new SerializedObject(hud);
            serializedHud.FindProperty("villagerSummaryText").objectReferenceValue = villager;
            serializedHud.FindProperty("storehouseSummaryText").objectReferenceValue = storehouse;
            serializedHud.FindProperty("telemetrySummaryText").objectReferenceValue = telemetry;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(hud);
            Debug.Log($"[{nameof(GodgameSubSceneUtility)}] Configured telemetry HUD references.");
        }

        private static Text FindText(Transform root, string relativePath)
        {
            var child = root.Find(relativePath);
            if (child == null)
            {
                Debug.LogWarning($"[{nameof(GodgameSubSceneUtility)}] Could not locate child '{relativePath}'.");
                return null;
            }

            var text = child.GetComponent<Text>();
            if (text == null)
            {
                Debug.LogWarning($"[{nameof(GodgameSubSceneUtility)}] No Text component on '{relativePath}'.");
            }

            return text;
        }

        public static string GetTelemetryHudStatus()
        {
            var canvas = GameObject.Find("Godgame HUD Canvas");
            if (canvas == null)
            {
                return "Canvas not found";
            }

            var hud = canvas.GetComponent<Godgame.Debugging.GodgameTelemetryHUD>();
            if (hud == null)
            {
                return "HUD component missing";
            }

            var serializedHud = new SerializedObject(hud);
            var villagerRef = serializedHud.FindProperty("villagerSummaryText").objectReferenceValue;
            var storehouseRef = serializedHud.FindProperty("storehouseSummaryText").objectReferenceValue;
            var telemetryRef = serializedHud.FindProperty("telemetrySummaryText").objectReferenceValue;

            return $"Villager={(villagerRef != null)} Storehouse={(storehouseRef != null)} Telemetry={(telemetryRef != null)}";
        }
    }
}
#endif
