using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace VFXPlayground.Capture
{
    /// <summary>
    /// Debug component for visualizing and adjusting VFX capture cameras in real-time.
    /// Shows camera previews and allows manual adjustment during play mode.
    /// </summary>
    [ExecuteInEditMode]
    public class VfxCaptureCameraDebug : MonoBehaviour
    {
        [Header("Camera Display")]
        [Tooltip("Show camera previews in a grid layout")]
        public bool showCameraPreviews = true;
        
        [Tooltip("Size of each camera preview")]
        [Range(0.1f, 0.5f)]
        public float previewSize = 0.25f;
        
        [Tooltip("Show camera gizmos in scene view")]
        public bool showGizmos = true;
        
        [Header("Camera Controls")]
        [Tooltip("Selected camera index for adjustment")]
        public int selectedCameraIndex = 0;
        
        [Tooltip("Movement speed for camera adjustment")]
        public float moveSpeed = 1f;
        
        [Tooltip("Rotation speed for camera adjustment")]
        public float rotationSpeed = 30f;
        
        [Tooltip("FOV adjustment speed")]
        public float fovSpeed = 5f;
        
        private List<Camera> _captureCameras = new List<Camera>();
        private List<RenderTexture> _previewTextures = new List<RenderTexture>();
        private VisualEffect _targetVfx;
        private bool _isInitialized = false;
        
        private void OnEnable()
        {
            RefreshCameras();
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CleanupPreviews();
        }
        
        private void Update()
        {
            if (!Application.isPlaying)
                return;
                
            RefreshCameras();
            HandleInput();
            UpdatePreviews();
        }
        
        private void RefreshCameras()
        {
            _captureCameras.Clear();
            
            // Find all capture cameras in the scene
            var allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in allCameras)
            {
                if (cam != null && cam.gameObject.name.StartsWith("CaptureCamera", System.StringComparison.OrdinalIgnoreCase))
                {
                    _captureCameras.Add(cam);
                }
            }
            
            // Find target VFX
            _targetVfx = FindObjectOfType<VisualEffect>();
            
            // Ensure we have preview textures
            while (_previewTextures.Count < _captureCameras.Count)
            {
                var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"PreviewRT_{_previewTextures.Count}"
                };
                rt.Create();
                _previewTextures.Add(rt);
            }
            
            // Assign preview textures to cameras
            for (int i = 0; i < _captureCameras.Count && i < _previewTextures.Count; i++)
            {
                if (_captureCameras[i] != null && _previewTextures[i] != null)
                {
                    // Store original target texture
                    if (_captureCameras[i].targetTexture == null || 
                        !_previewTextures.Contains(_captureCameras[i].targetTexture))
                    {
                        _captureCameras[i].targetTexture = _previewTextures[i];
                        _captureCameras[i].enabled = true;
                    }
                }
            }
            
            _isInitialized = true;
        }
        
        private void HandleInput()
        {
            if (_captureCameras.Count == 0)
                return;
                
            // Clamp selected camera index
            selectedCameraIndex = Mathf.Clamp(selectedCameraIndex, 0, _captureCameras.Count - 1);
            var selectedCam = _captureCameras[selectedCameraIndex];
            if (selectedCam == null)
                return;
            
            // Camera selection with number keys
            for (int i = 0; i < Mathf.Min(9, _captureCameras.Count); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    selectedCameraIndex = i;
                    Debug.Log($"[VfxCaptureCameraDebug] Selected camera {i}: {selectedCam.gameObject.name}");
                }
            }
            
            // Movement controls (WASD + QE for up/down)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += selectedCam.transform.forward;
                if (Input.GetKey(KeyCode.S)) move -= selectedCam.transform.forward;
                if (Input.GetKey(KeyCode.A)) move -= selectedCam.transform.right;
                if (Input.GetKey(KeyCode.D)) move += selectedCam.transform.right;
                if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
                if (Input.GetKey(KeyCode.E)) move += Vector3.up;
                
                selectedCam.transform.position += move * moveSpeed * Time.deltaTime;
            }
            
            // Rotation controls (Arrow keys)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                float rotX = 0f, rotY = 0f;
                if (Input.GetKey(KeyCode.UpArrow)) rotX -= rotationSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.DownArrow)) rotX += rotationSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.LeftArrow)) rotY -= rotationSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.RightArrow)) rotY += rotationSpeed * Time.deltaTime;
                
                selectedCam.transform.Rotate(rotX, rotY, 0f);
            }
            
            // FOV controls (Mouse wheel)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    selectedCam.fieldOfView = Mathf.Clamp(
                        selectedCam.fieldOfView + scroll * fovSpeed,
                        10f, 120f);
                }
            }
            
            // Look at VFX (L key)
            if (Input.GetKeyDown(KeyCode.L) && _targetVfx != null)
            {
                selectedCam.transform.LookAt(_targetVfx.transform.position);
                Debug.Log($"[VfxCaptureCameraDebug] Camera {selectedCameraIndex} looking at VFX");
            }
        }
        
        private void UpdatePreviews()
        {
            if (!showCameraPreviews || _captureCameras.Count == 0)
                return;
                
            // Ensure cameras are rendering
            foreach (var cam in _captureCameras)
            {
                if (cam != null)
                {
                    cam.enabled = true;
                }
            }
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showGizmos || _captureCameras.Count == 0)
                return;
            
            // Draw camera gizmos
            for (int i = 0; i < _captureCameras.Count; i++)
            {
                var cam = _captureCameras[i];
                if (cam == null)
                    continue;
                
                bool isSelected = (i == selectedCameraIndex);
                Color gizmoColor = isSelected ? Color.yellow : Color.cyan;
                
                // Draw camera frustum
                Handles.color = gizmoColor;
                Handles.DrawWireDisc(cam.transform.position, cam.transform.forward, 0.5f);
                
                // Draw forward direction
                Handles.DrawLine(
                    cam.transform.position,
                    cam.transform.position + cam.transform.forward * 2f);
                
                // Draw label
                Handles.Label(
                    cam.transform.position + Vector3.up * 0.5f,
                    $"{cam.gameObject.name}\n{(isSelected ? "[SELECTED]" : "")}");
            }
        }
        
        private void OnGUI()
        {
            if (!showCameraPreviews || !Application.isPlaying || _captureCameras.Count == 0)
                return;
            
            // Draw camera previews in a grid
            int cols = Mathf.CeilToInt(Mathf.Sqrt(_captureCameras.Count));
            int rows = Mathf.CeilToInt((float)_captureCameras.Count / cols);
            
            float previewWidth = Screen.width * previewSize;
            float previewHeight = Screen.height * previewSize;
            
            for (int i = 0; i < _captureCameras.Count; i++)
            {
                if (_captureCameras[i] == null || i >= _previewTextures.Count)
                    continue;
                
                int col = i % cols;
                int row = i / cols;
                
                float x = col * previewWidth;
                float y = row * previewHeight;
                
                Rect rect = new Rect(x, y, previewWidth, previewHeight);
                
                // Draw preview texture
                if (_previewTextures[i] != null)
                {
                    GUI.DrawTexture(rect, _previewTextures[i]);
                }
                
                // Draw border for selected camera
                if (i == selectedCameraIndex)
                {
                    GUI.Box(rect, "", GUI.skin.box);
                }
                
                // Draw camera name
                GUI.Label(new Rect(x, y, previewWidth, 20), _captureCameras[i].gameObject.name);
            }
            
            // Draw controls help
            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.Repaint)
            {
                string helpText = "Controls:\n" +
                    "1-9: Select camera\n" +
                    "Shift+WASD: Move camera\n" +
                    "Shift+QE: Move up/down\n" +
                    "Shift+Arrows: Rotate camera\n" +
                    "Shift+Scroll: Adjust FOV\n" +
                    "L: Look at VFX";
                
                GUI.Box(new Rect(10, Screen.height - 150, 200, 140), helpText);
            }
        }
        
        private void CleanupPreviews()
        {
            foreach (var rt in _previewTextures)
            {
                if (rt != null)
                {
                    rt.Release();
                    DestroyImmediate(rt);
                }
            }
            _previewTextures.Clear();
        }
        
        private void OnDestroy()
        {
            CleanupPreviews();
        }
    }
}

