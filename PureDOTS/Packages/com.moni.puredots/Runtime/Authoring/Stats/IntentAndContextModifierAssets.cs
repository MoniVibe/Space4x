using System;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Stats
{
    /// <summary>
    /// ScriptableObject describing how an intent modifies action footprints.
    /// Multipliers are applied first, then offsets.
    /// </summary>
    [CreateAssetMenu(fileName = "IntentModifier", menuName = "PureDOTS/Stats/Intent Modifier")]
    public sealed class IntentModifierAsset : ScriptableObject
    {
        [Serializable]
        public struct AxisModifier
        {
            public string axisId;
            public float value;
        }

        [Tooltip("Intent identifier (ProtectOthers, PersonalGain, Duty, Revenge, etc.).")]
        public string intentId = "Intent";

        [Tooltip("Multipliers applied to action axis deltas (default 1).")]
        public AxisModifier[] multipliers = Array.Empty<AxisModifier>();

        [Tooltip("Offsets added after multipliers (default 0).")]
        public AxisModifier[] offsets = Array.Empty<AxisModifier>();

        public BlobAssetReference<IntentModifierBlob> BuildBlobAsset(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<IntentModifierBlob>();

            var id = string.IsNullOrWhiteSpace(intentId) ? "Intent" : intentId.Trim();
            root.IntentId = new FixedString32Bytes(id);

            var multiplierArray = builder.Allocate(ref root.Multipliers, multipliers?.Length ?? 0);
            for (int i = 0; i < multiplierArray.Length; i++)
            {
                var entry = multipliers[i];
                var axisId = string.IsNullOrWhiteSpace(entry.axisId) ? $"Axis_{i}" : entry.axisId.Trim();
                multiplierArray[i] = new TraitAxisDelta
                {
                    AxisId = new FixedString32Bytes(axisId),
                    Delta = entry.value
                };
            }

            var offsetArray = builder.Allocate(ref root.Offsets, offsets?.Length ?? 0);
            for (int i = 0; i < offsetArray.Length; i++)
            {
                var entry = offsets[i];
                var axisId = string.IsNullOrWhiteSpace(entry.axisId) ? $"Axis_{i}" : entry.axisId.Trim();
                offsetArray[i] = new TraitAxisDelta
                {
                    AxisId = new FixedString32Bytes(axisId),
                    Delta = entry.value
                };
            }

            var blobAsset = builder.CreateBlobAssetReference<IntentModifierBlob>(allocator);
            builder.Dispose();
            return blobAsset;
        }
    }

    /// <summary>
    /// ScriptableObject describing deltas applied based on target/context classification.
    /// </summary>
    [CreateAssetMenu(fileName = "ContextModifier", menuName = "PureDOTS/Stats/Context Modifier")]
    public sealed class ContextModifierAsset : ScriptableObject
    {
        [Serializable]
        public struct AxisDeltaEntry
        {
            public string axisId;
            public float delta;
        }

        [Tooltip("Context identifier (Innocent, Threat, Criminal, ManEatingBeast, etc.).")]
        public string contextId = "Context";

        [Tooltip("Axis deltas applied after intent modifiers.")]
        public AxisDeltaEntry[] axisDeltas = Array.Empty<AxisDeltaEntry>();

        public BlobAssetReference<ContextModifierBlob> BuildBlobAsset(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ContextModifierBlob>();

            var id = string.IsNullOrWhiteSpace(contextId) ? "Context" : contextId.Trim();
            root.ContextId = new FixedString32Bytes(id);

            var deltaArray = builder.Allocate(ref root.Deltas, axisDeltas?.Length ?? 0);
            for (int i = 0; i < deltaArray.Length; i++)
            {
                var entry = axisDeltas[i];
                var axisId = string.IsNullOrWhiteSpace(entry.axisId) ? $"Axis_{i}" : entry.axisId.Trim();
                deltaArray[i] = new TraitAxisDelta
                {
                    AxisId = new FixedString32Bytes(axisId),
                    Delta = entry.delta
                };
            }

            var blobAsset = builder.CreateBlobAssetReference<ContextModifierBlob>(allocator);
            builder.Dispose();
            return blobAsset;
        }
    }
}



