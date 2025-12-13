#if UNITY_EDITOR
using UnityEngine;

namespace Space4X.Debugging
{
    public static class MissingBehaviourRuntimeProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void DumpFirstMissing()
        {
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in objects)
            {
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        Debug.LogError($"[MissingBehaviourRuntimeProbe] Missing script on GO: {GetPath(go)} (component index {i})");
                        return;
                    }
                }
            }
        }

        static string GetPath(GameObject go)
        {
            var current = go.transform;
            var path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = $"{current.name}/{path}";
            }

            return path;
        }
    }
}
#endif
