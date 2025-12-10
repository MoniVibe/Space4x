using UnityEngine;

namespace Space4X.Camera
{
    /// <summary>
    /// Helper shim for editor tooling that expects Space4X.Camera.main.
    /// Ensures a tagged main camera exists and returns it.
    /// </summary>
    public static class main
    {
        public static UnityEngine.Camera EnsureMainCamera()
        {
            var cam = UnityEngine.Camera.main;

            if (cam == null)
            {
                cam = Object.FindAnyObjectByType<UnityEngine.Camera>();
            }

            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<UnityEngine.Camera>();
            }

            if (cam != null && cam.tag != "MainCamera")
            {
                cam.tag = "MainCamera";
            }

            return cam;
        }
    }
}



