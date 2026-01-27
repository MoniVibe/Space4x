using System;
using System.Linq;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace PureDOTS.Runtime.Presentation
{
    public static class PresentationScreenshotUtility
    {
        public static Hash128 ComputeHash(Texture2D texture)
        {
            if (texture == null)
            {
                return default;
            }

            var raw = texture.GetRawTextureData();
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(raw.ToArray());
            var hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return new Hash128(hex);
        }

        public static ScreenshotValidationResult Compare(Texture2D texture, in Hash128 baseline, bool allowStyleChange = false)
        {
            var hash = ComputeHash(texture);
            bool matches = hash == baseline;
            return new ScreenshotValidationResult
            {
                Hash = hash,
                MatchesBaseline = matches,
                AllowedDifference = allowStyleChange && !matches
            };
        }
    }

    public struct ScreenshotValidationResult
    {
        public Hash128 Hash;
        public bool MatchesBaseline;
        public bool AllowedDifference;
    }
}
