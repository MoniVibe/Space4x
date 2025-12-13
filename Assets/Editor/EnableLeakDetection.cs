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
        if (!Application.isBatchMode)
        {
            Debug.Log("Native Leak Detection enabled with stack trace.");
        }
    }
}
#endif
