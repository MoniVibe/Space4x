using PureDOTS.Runtime.WorldGen;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.WorldGen
{
    [CreateAssetMenu(fileName = "SurfaceConstraintMap", menuName = "PureDOTS/WorldGen/Surface Constraint Map", order = 2)]
    public sealed class SurfaceConstraintMapAsset : ScriptableObject
    {
        [Header("World Bounds (Planar XZ)")]
        public Vector2 worldMinXZ = new(-256f, -256f);
        public Vector2 worldMaxXZ = new(256f, 256f);

        [Header("Constraint Textures (optional)")]
        [Tooltip("Signed height bias: 0=-1, 128~=0, 255=+1 (use red channel).")]
        public Texture2D heightBias;

        [Tooltip("Ocean preference mask: 0=none, 255=force ocean (use red channel).")]
        public Texture2D oceanMask;

        [Tooltip("Ridge preference mask: 0=none, 255=strong ridges (use red channel).")]
        public Texture2D ridgeMask;

        public bool TryBuildBlobAsset(out BlobAssetReference<SurfaceConstraintMapBlob> blob, out string error)
        {
            blob = default;
            error = string.Empty;

            if (worldMaxXZ.x <= worldMinXZ.x || worldMaxXZ.y <= worldMinXZ.y)
            {
                error = "Invalid world bounds for constraint map.";
                return false;
            }

            var width = 0;
            var height = 0;
            if (TryGetResolution(heightBias, ref width, ref height, out error) &&
                TryGetResolution(oceanMask, ref width, ref height, out error) &&
                TryGetResolution(ridgeMask, ref width, ref height, out error))
            {
                if (width <= 1 || height <= 1)
                {
                    error = "No constraint textures assigned (need at least one).";
                    return false;
                }
            }
            else
            {
                return false;
            }

            var heightBytes = ReadRedChannel(heightBias, width, height);
            var oceanBytes = ReadRedChannel(oceanMask, width, height);
            var ridgeBytes = ReadRedChannel(ridgeMask, width, height);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SurfaceConstraintMapBlob>();
            root.SchemaVersion = 1;
            root.WorldMinXZ = new Unity.Mathematics.float2(worldMinXZ.x, worldMinXZ.y);
            root.WorldMaxXZ = new Unity.Mathematics.float2(worldMaxXZ.x, worldMaxXZ.y);
            root.Resolution = new Unity.Mathematics.int2(width, height);

            if (heightBytes != null)
            {
                var dst = builder.Allocate(ref root.HeightBiasQ, heightBytes.Length);
                for (int i = 0; i < heightBytes.Length; i++)
                {
                    dst[i] = heightBytes[i];
                }
            }
            else
            {
                builder.Allocate(ref root.HeightBiasQ, 0);
            }

            if (oceanBytes != null)
            {
                var dst = builder.Allocate(ref root.OceanMaskQ, oceanBytes.Length);
                for (int i = 0; i < oceanBytes.Length; i++)
                {
                    dst[i] = oceanBytes[i];
                }
            }
            else
            {
                builder.Allocate(ref root.OceanMaskQ, 0);
            }

            if (ridgeBytes != null)
            {
                var dst = builder.Allocate(ref root.RidgeMaskQ, ridgeBytes.Length);
                for (int i = 0; i < ridgeBytes.Length; i++)
                {
                    dst[i] = ridgeBytes[i];
                }
            }
            else
            {
                builder.Allocate(ref root.RidgeMaskQ, 0);
            }

            blob = builder.CreateBlobAssetReference<SurfaceConstraintMapBlob>(Allocator.Persistent);
            builder.Dispose();
            return true;
        }

        private static bool TryGetResolution(Texture2D texture, ref int width, ref int height, out string error)
        {
            error = string.Empty;
            if (texture == null)
            {
                return true;
            }

            if (width == 0 && height == 0)
            {
                width = texture.width;
                height = texture.height;
                return true;
            }

            if (texture.width != width || texture.height != height)
            {
                error = $"Constraint textures must match resolution ({width}x{height}). '{texture.name}' is {texture.width}x{texture.height}.";
                return false;
            }

            return true;
        }

        private static byte[] ReadRedChannel(Texture2D texture, int width, int height)
        {
            if (texture == null)
            {
                return null;
            }

            var pixels = texture.GetPixels32();
            if (pixels == null || pixels.Length != width * height)
            {
                return null;
            }

            var bytes = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i] = pixels[i].r;
            }

            return bytes;
        }
    }
}

