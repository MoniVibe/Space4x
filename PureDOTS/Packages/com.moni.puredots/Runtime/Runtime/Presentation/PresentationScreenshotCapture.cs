using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace PureDOTS.Runtime.Presentation
{
    public static class PresentationScreenshotCapture
    {
        /// <summary>
        /// Captures the current screen, computes a hash, and compares against an optional baseline.
        /// </summary>
        public static ScreenshotValidationResult CaptureAndCompare(Hash128 baseline = default, bool allowStyleChange = false)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            var result = PresentationScreenshotUtility.Compare(texture, baseline, allowStyleChange);
            Object.Destroy(texture);
            return result;
        }
    }
}
