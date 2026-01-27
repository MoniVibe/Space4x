#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Config;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// ScriptableObject asset for defining need curves (hunger, energy, morale thresholds).
    /// Uses AnimationCurve for designer-friendly editing, then samples to blob.
    /// </summary>
    [CreateAssetMenu(fileName = "VillagerNeedCurve", menuName = "PureDOTS/Villager Need Curve")]
    public class VillagerNeedCurve : ScriptableObject
    {
        public enum NeedType
        {
            Hunger = 0,
            Energy = 1,
            Morale = 2
        }
        
        [Header("Curve Definition")]
        public string curveName = "DefaultCurve";
        public NeedType needType = NeedType.Hunger;
        
        [Header("Animation Curve (Input: 0-1 normalized need, Output: 0-1 utility/priority)")]
        [Tooltip("X-axis: normalized need value (0 = low need, 1 = high need). Y-axis: utility/priority (0 = low priority, 1 = high priority).")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 1f, 1f, 0f); // Default: high utility when need is low
        
        [Header("Sampling")]
        [Range(10, 100)]
        [Tooltip("Number of samples to take from AnimationCurve for blob storage.")]
        public int sampleCount = 32;
        
        /// <summary>
        /// Samples the AnimationCurve into a list of float2 points.
        /// </summary>
        public List<float2> SampleCurve()
        {
            var points = new List<float2>();
            var step = 1f / (sampleCount - 1);
            
            for (int i = 0; i < sampleCount; i++)
            {
                var x = i * step;
                var y = curve.Evaluate(x);
                points.Add(new float2(x, math.clamp(y, 0f, 1f)));
            }
            
            return points;
        }
        
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(curveName))
            {
                curveName = $"{needType}Curve";
            }
            
            // Ensure curve has at least 2 keys
            if (curve == null || curve.length < 2)
            {
                curve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            }
        }
    }
    
    /// <summary>
    /// Catalog asset containing multiple need curves.
    /// </summary>
    [CreateAssetMenu(fileName = "VillagerNeedCurveCatalog", menuName = "PureDOTS/Villager Need Curve Catalog")]
    public class VillagerNeedCurveCatalog : ScriptableObject
    {
        [Header("Need Curves")]
        public List<VillagerNeedCurve> curves = new List<VillagerNeedCurve>();
        
        private void OnValidate()
        {
            // Ensure curve names are unique
            var nameSet = new HashSet<string>();
            foreach (var curve in curves)
            {
                if (curve == null)
                {
                    continue;
                }
                
                if (string.IsNullOrEmpty(curve.curveName))
                {
                    curve.curveName = $"{curve.needType}Curve";
                }
                
                if (nameSet.Contains(curve.curveName))
                {
                    Debug.LogWarning($"Duplicate curve name: {curve.curveName}");
                }
                nameSet.Add(curve.curveName);
            }
        }
    }
}
#endif

