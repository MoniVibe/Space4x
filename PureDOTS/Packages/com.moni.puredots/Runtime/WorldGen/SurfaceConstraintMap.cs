using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen
{
    public struct SurfaceConstraintMapBlob
    {
        public uint SchemaVersion;
        public float2 WorldMinXZ;
        public float2 WorldMaxXZ;
        public int2 Resolution;

        /// <summary>Signed height bias encoded as 0..255 where 0=-1, 255=+1.</summary>
        public BlobArray<byte> HeightBiasQ;

        /// <summary>Ocean preference mask encoded as 0..255 where 0=none, 255=force ocean.</summary>
        public BlobArray<byte> OceanMaskQ;

        /// <summary>Ridge preference mask encoded as 0..255 where 0=none, 255=strong ridges.</summary>
        public BlobArray<byte> RidgeMaskQ;
    }

    public struct SurfaceConstraintMapComponent : IComponentData
    {
        public BlobAssetReference<SurfaceConstraintMapBlob> Map;
    }

    public readonly struct SurfaceConstraintMapSampler
    {
        public readonly BlobAssetReference<SurfaceConstraintMapBlob> Map;

        public SurfaceConstraintMapSampler(BlobAssetReference<SurfaceConstraintMapBlob> map)
        {
            Map = map;
        }

        public bool IsCreated =>
            Map.IsCreated &&
            Map.Value.Resolution.x > 1 &&
            Map.Value.Resolution.y > 1 &&
            (Map.Value.WorldMaxXZ.x > Map.Value.WorldMinXZ.x) &&
            (Map.Value.WorldMaxXZ.y > Map.Value.WorldMinXZ.y);

        public float SampleHeightBiasSigned01(float2 worldXZ)
        {
            if (!IsCreated || Map.Value.HeightBiasQ.Length == 0)
            {
                return 0f;
            }

            ref var map = ref Map.Value;
            var q01 = SampleQ01(ref map.HeightBiasQ, worldXZ);
            return (q01 * 2f) - 1f;
        }

        public float SampleOceanMask01(float2 worldXZ)
        {
            if (!IsCreated || Map.Value.OceanMaskQ.Length == 0)
            {
                return 0f;
            }

            ref var map = ref Map.Value;
            return SampleQ01(ref map.OceanMaskQ, worldXZ);
        }

        public float SampleRidgeMask01(float2 worldXZ)
        {
            if (!IsCreated || Map.Value.RidgeMaskQ.Length == 0)
            {
                return 0f;
            }

            ref var map = ref Map.Value;
            return SampleQ01(ref map.RidgeMaskQ, worldXZ);
        }

        private float SampleQ01(ref BlobArray<byte> src, float2 worldXZ)
        {
            ref var map = ref Map.Value;
            var uv = math.saturate((worldXZ - map.WorldMinXZ) / (map.WorldMaxXZ - map.WorldMinXZ));
            var gx = uv.x * (map.Resolution.x - 1);
            var gy = uv.y * (map.Resolution.y - 1);
            var x0 = (int)math.floor(gx);
            var y0 = (int)math.floor(gy);
            var x1 = math.min(x0 + 1, map.Resolution.x - 1);
            var y1 = math.min(y0 + 1, map.Resolution.y - 1);
            var tx = gx - x0;
            var ty = gy - y0;

            var i00 = x0 + y0 * map.Resolution.x;
            var i10 = x1 + y0 * map.Resolution.x;
            var i01 = x0 + y1 * map.Resolution.x;
            var i11 = x1 + y1 * map.Resolution.x;

            var v00 = src[i00] * (1f / 255f);
            var v10 = src[i10] * (1f / 255f);
            var v01 = src[i01] * (1f / 255f);
            var v11 = src[i11] * (1f / 255f);

            var a = math.lerp(v00, v10, tx);
            var b = math.lerp(v01, v11, tx);
            return math.lerp(a, b, ty);
        }
    }
}
