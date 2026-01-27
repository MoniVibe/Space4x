using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Config
{
    /// <summary>
    /// Runtime blob data for need curve (hunger, energy, morale thresholds).
    /// Stores sampled curve points for Burst-friendly evaluation.
    /// </summary>
    public struct NeedCurveData
    {
        public FixedString64Bytes CurveName;
        public byte NeedType; // 0 = Hunger, 1 = Energy, 2 = Morale
        
        // Sampled curve points (normalized 0-1 input -> 0-1 output)
        public BlobArray<float2> CurvePoints; // (input, output) pairs
        
        // Evaluation: linear interpolation between points
        public float Evaluate(float normalizedInput)
        {
            if (CurvePoints.Length == 0)
            {
                return 0f;
            }
            
            var clampedInput = math.clamp(normalizedInput, 0f, 1f);
            
            // Find surrounding points
            for (int i = 0; i < CurvePoints.Length - 1; i++)
            {
                var p0 = CurvePoints[i];
                var p1 = CurvePoints[i + 1];
                
                if (clampedInput >= p0.x && clampedInput <= p1.x)
                {
                    // Linear interpolation
                    var t = (clampedInput - p0.x) / math.max(p1.x - p0.x, 1e-6f);
                    return math.lerp(p0.y, p1.y, t);
                }
            }
            
            // Clamp to first/last point
            if (clampedInput <= CurvePoints[0].x)
            {
                return CurvePoints[0].y;
            }
            return CurvePoints[CurvePoints.Length - 1].y;
        }
    }
    
    /// <summary>
    /// Blob asset containing all need curve definitions.
    /// </summary>
    public struct VillagerNeedCurveCatalogBlob
    {
        public BlobArray<NeedCurveData> Curves;
        
        public int FindCurveIndex(in FixedString64Bytes name)
        {
            for (int i = 0; i < Curves.Length; i++)
            {
                ref var curve = ref Curves[i];
                if (curve.CurveName.Equals(name))
                {
                    return i;
                }
            }
            return -1;
        }
        
        public bool TryGetCurveIndex(in FixedString64Bytes name, out int curveIndex)
        {
            curveIndex = FindCurveIndex(name);
            return curveIndex >= 0;
        }
        
        public ref NeedCurveData GetCurve(int curveIndex) => ref Curves[curveIndex];
    }
}
