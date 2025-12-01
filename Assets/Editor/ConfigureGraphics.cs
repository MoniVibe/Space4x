#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ConfigureGraphics
{
    public static void Execute()
    {
        // Windows
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new GraphicsDeviceType[] {
            GraphicsDeviceType.Direct3D11,
            GraphicsDeviceType.Direct3D12
        });

        // macOS
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new GraphicsDeviceType[] {
            GraphicsDeviceType.Metal
        });

        Debug.Log("Configured Graphics APIs for Windows and macOS.");
    }
}
#endif
