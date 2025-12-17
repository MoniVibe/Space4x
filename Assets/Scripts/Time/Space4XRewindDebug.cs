using UnityEngine;
using UnityEngine.InputSystem;
using Space4X.Temporal;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.TimeDebug
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Debug MonoBehaviour for testing preview rewind functionality in Space4X.
    /// Hold R to scrub backwards through time (ghosts preview rewind).
    /// Release R to freeze preview.
    /// Press Space to commit rewind.
    /// Press C or Escape to cancel rewind.
    /// </summary>
    public class Space4XRewindDebug : MonoBehaviour
    {
        [Header("Rewind Debug Controls")]
        [Tooltip("Key to hold for scrubbing rewind")]
        [SerializeField] private Key keyRewind = Key.R;
        
        [Tooltip("Key to commit rewind from preview")]
        [SerializeField] private Key keyCommit = Key.Space;
        
        [Tooltip("Key to cancel rewind preview")]
        [SerializeField] private Key keyCancel = Key.C;
        
        [Tooltip("Rewind scrub speed multiplier (1-4x)")]
        [Range(1f, 4f)]
        [SerializeField] private float scrubSpeed = 2.0f;
        
        [Header("Debug")]
        [Tooltip("Log rewind events")]
        [SerializeField] private bool logRewindEvents = true;

        private bool _isScrubbing = false;

        private void Start()
        {
            UnityDebug.Log("[Space4XRewindDebug] Active");
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // Start preview rewind on R down
            if (!_isScrubbing && Keyboard.current[keyRewind].wasPressedThisFrame)
            {
                _isScrubbing = true;
                UnityDebug.Log($"[Space4XRewindDebug] R key pressed - calling BeginRewindPreview({scrubSpeed:F2}x)");
                Space4XTimeAPI.BeginRewindPreview(scrubSpeed);
                
                if (logRewindEvents)
                {
                    UnityDebug.Log($"[Space4XRewindDebug] Begin rewind preview (speed={scrubSpeed:F2}x)");
                }
            }

            // While holding, you *could* change scrubSpeed and push updates here
            if (_isScrubbing && Keyboard.current[keyRewind].isPressed)
            {
                // For now just send the same speed.
                // Could add dynamic speed adjustment here if needed
                Space4XTimeAPI.UpdateRewindPreviewSpeed(scrubSpeed);
            }

            // Release → freeze preview
            if (_isScrubbing && Keyboard.current[keyRewind].wasReleasedThisFrame)
            {
                _isScrubbing = false;
                UnityDebug.Log("[Space4XRewindDebug] R key released - calling EndRewindScrub()");
                Space4XTimeAPI.EndRewindScrub();
                
                if (logRewindEvents)
                {
                    UnityDebug.Log("[Space4XRewindDebug] End scrub, preview frozen");
                }
            }

            // Commit from preview
            if (Keyboard.current[keyCommit].wasPressedThisFrame)
            {
                UnityDebug.Log("[Space4XRewindDebug] Space key pressed - calling CommitRewindFromPreview()");
                Space4XTimeAPI.CommitRewindFromPreview();
                
                if (logRewindEvents)
                {
                    UnityDebug.Log("[Space4XRewindDebug] Commit rewind from preview");
                }
                
                _isScrubbing = false; // Reset state
            }

            // Cancel preview
            if (Keyboard.current[keyCancel].wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                UnityDebug.Log("[Space4XRewindDebug] Cancel key pressed - calling CancelRewindPreview()");
                Space4XTimeAPI.CancelRewindPreview();
                
                if (logRewindEvents)
                {
                    UnityDebug.Log("[Space4XRewindDebug] Cancel rewind preview");
                }
                
                _isScrubbing = false; // Reset state
            }
        }
    }
}

