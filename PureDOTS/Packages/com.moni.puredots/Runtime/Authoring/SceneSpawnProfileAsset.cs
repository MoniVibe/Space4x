using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(menuName = "PureDOTS/Scene Spawn Profile", fileName = "SceneSpawnProfile")]
    public sealed class SceneSpawnProfileAsset : ScriptableObject
    {
        [Tooltip("Deterministic seed used for placement randomization. Override per scene if needed.")]
        public uint seed = 1;

        [Tooltip("Ordered list of spawn instructions processed during scene bootstrap.")]
        public List<SceneSpawnEntryDefinition> entries = new();
    }

    [Serializable]
    public sealed class SceneSpawnEntryDefinition
    {
        [Header("Basics")]
        public SceneSpawnCategory category = SceneSpawnCategory.Generic;
        public GameObject prefab;
        [Min(1)] public int count = 1;
        public SpawnPlacementMode placement = SpawnPlacementMode.Point;
        public SpawnRotationMode rotation = SpawnRotationMode.Identity;

        [Header("Offsets & Spread")]
        public Vector3 localOffset;
        [Tooltip("Maximum radius used for RandomCircle / Ring placement.")]
        [Min(0f)] public float radius = 5f;
        [Tooltip("Inner radius for Ring placement. Must be <= Radius.")]
        [Min(0f)] public float innerRadius = 0f;
        [Tooltip("Rows (x) and columns (y) for grid placement.")]
        public Vector2Int gridDimensions = new(3, 3);
        [Tooltip("Spacing in metres between grid cells (X,Z).")]
        public Vector2 gridSpacing = new(2f, 2f);
        [Tooltip("Random Y range applied per spawn (min/max metres).")]
        public Vector2 heightRange = Vector2.zero;
        [Tooltip("Yaw rotation applied when Rotation Mode = FixedYaw.")]
        public float fixedYawDegrees;
        public bool randomizeSeedOffset;
        public uint explicitSeedOffset;

        [Header("Custom Points (optional)")]
        [Tooltip("When using CustomPoints placement, spawns at these local offsets relative to the spawn root.")]
        public List<Vector3> customPoints = new();

        [Header("Payload (domain-specific)")]
        [Tooltip("Identifier forwarded to the spawn system (profession id, species id, etc.).")]
        public string payloadId;
        [Tooltip("Optional numeric payload for domain-specific tuning (capacity, tier, etc.).")]
        public float payloadValue;

        [Header("Presentation (optional)")]
        [Tooltip("Automatically enqueue a presentation request when the spawn occurs.")]
        public bool spawnPresentation;
        [Tooltip("Descriptor key defined in the Presentation Registry asset.")]
        public string presentationDescriptorKey;
        public Vector3 presentationOffset;
        public Vector3 presentationEulerOffset;
        [Min(0.01f)] public float presentationScaleMultiplier = 1f;
        public Color presentationTint = Color.clear;
        [Tooltip("Optional explicit variant seed; leave zero to derive from spawn order.")]
        public uint presentationVariantSeed;
        [Tooltip("Additional spawn flags for the presentation request.")]
        public PresentationSpawnFlags presentationFlags = PresentationSpawnFlags.AllowPooling;

        public uint GetSeedOffset(uint entryIndex)
        {
            if (!randomizeSeedOffset)
            {
                return explicitSeedOffset;
            }

            // Simple deterministic hash using entry index to avoid editor-time randomness.
            return explicitSeedOffset ^ math.hash(new uint2((uint)entryIndex + 1u, 0x9E3779B9u));
        }
    }
}
