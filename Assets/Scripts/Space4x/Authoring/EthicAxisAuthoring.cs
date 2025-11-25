using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for ethic axis values (sparse buffer).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Ethic Axes")]
    public sealed class EthicAxisAuthoring : MonoBehaviour
    {
        [Serializable]
        public class EthicAxisEntry
        {
            public EthicAxisId axis;
            [Tooltip("Value in [-2, +2]. ±1 = regular conviction, ±2 = fanatic")]
            [Range(-2f, 2f)]
            public float value = 0f;
        }

        [Tooltip("Ethic axis convictions (sparse - only include non-zero values)")]
        public List<EthicAxisEntry> ethicAxes = new List<EthicAxisEntry>();

        public sealed class Baker : Unity.Entities.Baker<EthicAxisAuthoring>
        {
            public override void Bake(EthicAxisAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<EthicAxisValue>(entity);

                if (authoring.ethicAxes != null)
                {
                    foreach (var entry in authoring.ethicAxes)
                    {
                        if (math.abs(entry.value) > 0.01f) // Only add non-zero values
                        {
                            buffer.Add(new EthicAxisValue
                            {
                                Axis = entry.axis,
                                Value = (half)math.clamp(entry.value, -2f, 2f)
                            });
                        }
                    }
                }
            }
        }
    }
}

