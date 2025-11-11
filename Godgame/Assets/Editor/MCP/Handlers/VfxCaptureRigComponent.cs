using UnityEngine;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Component attached to capture cameras in the VFX capture rig.
    /// Manages RenderTexture and provides capture functionality.
    /// </summary>
    public class VfxCaptureRigComponent : MonoBehaviour
    {
        [SerializeField] private Camera captureCamera;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private string cameraName;
        
        public Camera CaptureCamera => captureCamera;
        public RenderTexture RenderTexture => renderTexture;
        public string CameraName => cameraName;

        private void Awake()
        {
            if (captureCamera == null)
                captureCamera = GetComponent<Camera>();
        }

        public void Initialize(int width, int height, string name, Color backgroundColor)
        {
            cameraName = name;
            
            if (captureCamera == null)
                captureCamera = GetComponent<Camera>();
            
            if (captureCamera == null)
            {
                Debug.LogError($"[VfxCaptureRigComponent] Camera component not found on {gameObject.name}");
                return;
            }

            // Create or update RenderTexture
            if (renderTexture != null)
            {
                if (renderTexture.width != width || renderTexture.height != height)
                {
                    renderTexture.Release();
                    DestroyImmediate(renderTexture);
                    renderTexture = null;
                }
            }

            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.name = $"CaptureRT_{name}";
            }

            captureCamera.targetTexture = renderTexture;
            captureCamera.backgroundColor = backgroundColor;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        public Texture2D CaptureFrame()
        {
            if (renderTexture == null || captureCamera == null)
                return null;

            // Force camera to render
            captureCamera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tex.Apply();

            RenderTexture.active = previousActive;

            return tex;
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyImmediate(renderTexture);
            }
        }
    }
}

