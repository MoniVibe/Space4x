using System;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Stats
{
    /// <summary>
    /// ScriptableObject describing the trait deltas emitted by a specific action type.
    /// </summary>
    [CreateAssetMenu(fileName = "ActionFootprint", menuName = "PureDOTS/Stats/Action Footprint")]
    public sealed class ActionFootprintAsset : ScriptableObject
    {
        [Serializable]
        public struct AxisDeltaEntry
        {
            public string axisId;
            public float delta;
        }

        [Tooltip("Action identifier (Kill, Torture, Mercy, Charity, etc.).")]
        public string actionTypeId = "Action";

        [Tooltip("Axis deltas emitted when this action resolves.")]
        public AxisDeltaEntry[] axisDeltas = Array.Empty<AxisDeltaEntry>();

        public BlobAssetReference<ActionFootprintBlob> BuildBlobAsset(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ActionFootprintBlob>();

            var actionId = string.IsNullOrWhiteSpace(actionTypeId) ? "Action" : actionTypeId.Trim();
            root.ActionTypeId = new FixedString32Bytes(actionId);

            var deltasArray = builder.Allocate(ref root.Deltas, axisDeltas?.Length ?? 0);
            for (int i = 0; i < deltasArray.Length; i++)
            {
                var entry = axisDeltas[i];
                if (string.IsNullOrWhiteSpace(entry.axisId))
                {
                    deltasArray[i] = new TraitAxisDelta
                    {
                        AxisId = new FixedString32Bytes($"Axis_{i}"),
                        Delta = entry.delta
                    };
                    continue;
                }

                deltasArray[i] = new TraitAxisDelta
                {
                    AxisId = new FixedString32Bytes(entry.axisId.Trim()),
                    Delta = entry.delta
                };
            }

            var blobAsset = builder.CreateBlobAssetReference<ActionFootprintBlob>(allocator);
            builder.Dispose();
            return blobAsset;
        }
    }
}



