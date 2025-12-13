#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Collections;

[InitializeOnLoad]
public class EnableLeakDetection
{
    static EnableLeakDetection()
    {
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
#if UNITY_EDITOR && PURE_DOTS_DIAG
        if (!Application.isBatchMode)
        {
            const string sessionKey = "Space4X.EnableLeakDetection.Logged";
            if (!SessionState.GetBool(sessionKey, false))
            {
                SessionState.SetBool(sessionKey, true);
                Debug.Log("Native Leak Detection enabled with stack trace.");
            }
        }
#endif
    }
}
#endif
