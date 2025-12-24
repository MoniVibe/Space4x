using UnityEngine;
using UCamera = UnityEngine.Camera;
using UObject = UnityEngine.Object;

namespace Space4X.Camera
{
    /// <summary>
    /// Helper shim for editor tooling that expects Space4X.Camera.main.
    /// Ensures a tagged main camera exists and returns it.
    /// </summary>
    public static class main
    {
        public static UCamera EnsureMainCamera()
        {
#if UNITY_EDITOR
            var cam = UCamera.main;

            if (cam == null)
            {
                cam = UObject.FindAnyObjectByType<UCamera>();
            }

            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<UCamera>();
            }

            if (cam != null && cam.tag != "MainCamera")
            {
                cam.tag = "MainCamera";
            }

            return cam;
#else
            return UCamera.main;
#endif
        }
    }
}



