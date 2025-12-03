using UnityEngine;
using Space4X.Time;

namespace Space4X.TimeDebug
{
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
        [SerializeField] private KeyCode rewindKey = KeyCode.R;
        
        [Tooltip("Key to commit rewind from preview")]
        [SerializeField] private KeyCode commitKey = KeyCode.Space;
        
        [Tooltip("Key to cancel rewind preview")]
        [SerializeField] private KeyCode cancelKey = KeyCode.C;
        
        [Tooltip("Rewind scrub speed multiplier (1-4x)")]
        [Range(1f, 4f)]
        [SerializeField] private float scrubSpeed = 2.0f;
        
        [Header("Debug")]
        [Tooltip("Log rewind events")]
        [SerializeField] private bool logRewindEvents = true;

        private bool _isScrubbing = false;

        private void Start()
        {
            Debug.Log("[Space4XRewindDebug] Active");
        }

        private void Update()
        {
            // Start preview rewind on R down
            if (!_isScrubbing && Input.GetKeyDown(rewindKey))
            {
                _isScrubbing = true;
                Debug.Log($"[Space4XRewindDebug] R key pressed - calling BeginRewindPreview({scrubSpeed:F2}x)");
                Space4XTimeAPI.BeginRewindPreview(scrubSpeed);
                
                if (logRewindEvents)
                {
                    Debug.Log($"[Space4XRewindDebug] Begin rewind preview (speed={scrubSpeed:F2}x)");
                }
            }

            // While holding, you *could* change scrubSpeed and push updates here
            if (_isScrubbing && Input.GetKey(rewindKey))
            {
                // For now just send the same speed.
                // Could add dynamic speed adjustment here if needed
                Space4XTimeAPI.UpdateRewindPreviewSpeed(scrubSpeed);
            }

            // Release → freeze preview
            if (_isScrubbing && Input.GetKeyUp(rewindKey))
            {
                _isScrubbing = false;
                Debug.Log("[Space4XRewindDebug] R key released - calling EndRewindScrub()");
                Space4XTimeAPI.EndRewindScrub();
                
                if (logRewindEvents)
                {
                    Debug.Log("[Space4XRewindDebug] End scrub, preview frozen");
                }
            }

            // Commit from preview
            if (Input.GetKeyDown(commitKey))
            {
                Debug.Log("[Space4XRewindDebug] Space key pressed - calling CommitRewindFromPreview()");
                Space4XTimeAPI.CommitRewindFromPreview();
                
                if (logRewindEvents)
                {
                    Debug.Log("[Space4XRewindDebug] Commit rewind from preview");
                }
                
                _isScrubbing = false; // Reset state
            }

            // Cancel preview
            if (Input.GetKeyDown(cancelKey) || Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[Space4XRewindDebug] Cancel key pressed - calling CancelRewindPreview()");
                Space4XTimeAPI.CancelRewindPreview();
                
                if (logRewindEvents)
                {
                    Debug.Log("[Space4XRewindDebug] Cancel rewind preview");
                }
                
                _isScrubbing = false; // Reset state
            }
        }
    }
}

