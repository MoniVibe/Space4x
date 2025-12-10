using UnityEngine;

namespace Space4X.CameraSystem
{
    /// <summary>
    /// Disables extra AudioListeners at startup to avoid Unity's duplicate-listener warning.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class EnsureSingleAudioListener : MonoBehaviour
    {
        void Awake()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var preferred = TryGetComponent<AudioListener>(out var selfListener) ? selfListener : null;
            if (preferred == null && listeners.Length > 0)
            {
                preferred = listeners[0];
            }

            foreach (var listener in listeners)
            {
                if (listener == null)
                    continue;

                bool keep = listener == preferred;
                listener.enabled = keep;
            }
        }
    }
}
