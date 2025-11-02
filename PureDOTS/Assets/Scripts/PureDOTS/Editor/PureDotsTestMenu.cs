#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PureDOTS.Editor
{
    public static class PureDotsTestMenu
    {
        [MenuItem("PureDOTS/Run PlayMode Tests", priority = 100)]
        public static void RunPlaymodeTests()
        {
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = TestMode.PlayMode
            };
            testRunnerApi.Execute(new ExecutionSettings(filter));
            EditorUtility.DisplayDialog("PureDOTS", "PlayMode tests started. Check the Test Runner window for results.", "OK");
        }

        [MenuItem("PureDOTS/Run EditMode Tests", priority = 101)]
        public static void RunEditmodeTests()
        {
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = TestMode.EditMode
            };
            testRunnerApi.Execute(new ExecutionSettings(filter));
            EditorUtility.DisplayDialog("PureDOTS", "EditMode tests started. Check the Test Runner window for results.", "OK");
        }
    }
}
#endif
