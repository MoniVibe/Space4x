#if UNITY_EDITOR
using PureDOTS.Runtime.Camera;
using UnityEngine;

namespace PureDOTS.Editor.Camera
{
    /// <summary>
    /// Editor-only diagnostics behaviour that warns about missing camera rig publishers
    /// or misconfigured CameraRigApplier components.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    public sealed class CameraRigDiagnosticsMono : MonoBehaviour
    {
        private int _lastCheckedFrame = -1;
        private bool _warnedNoState;
        private bool _warnedNoCamera;

        private void LateUpdate()
        {
            int currentFrame = Time.frameCount;

            if (_lastCheckedFrame == currentFrame)
            {
                return;
            }
            _lastCheckedFrame = currentFrame;

            // Warn if nobody published this frame.
            if (!CameraRigService.HasState)
            {
                if (!_warnedNoState)
                {
                    Debug.LogWarning("[CameraRigDiagnostics] No camera state published yet. Ensure a rig controller calls CameraRigService.Publish().", this);
                    _warnedNoState = true;
                }
            }
            else
            {
                _warnedNoState = false;
            }

            // Warn if multiple publishers per frame.
            var diag = CameraRigService.Diagnostics;
            if (diag.PublishCountThisFrame > 1)
            {
                Debug.LogError($"[CameraRigDiagnostics] Multiple camera publishers detected ({diag.PublishCountThisFrame} publishes this frame). Last rig type: {diag.LastRigType}.", this);
            }

            // Warn if the applier cannot find a camera to drive.
            var applier = FindFirstObjectByType<CameraRigApplier>();
            if (applier != null)
            {
                var camera = applier.GetComponent<UnityEngine.Camera>() ?? UnityEngine.Camera.main;
                if (camera == null)
                {
                    if (!_warnedNoCamera)
                    {
                        Debug.LogWarning("[CameraRigDiagnostics] CameraRigApplier did not find a Camera to apply to. Attach it to a Camera GameObject or ensure Camera.main exists.", applier);
                        _warnedNoCamera = true;
                    }
                }
                else
                {
                    _warnedNoCamera = false;
                }
            }
        }
    }
}
#endif


