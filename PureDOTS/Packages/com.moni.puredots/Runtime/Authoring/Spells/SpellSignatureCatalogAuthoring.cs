#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Spells;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Spells
{
    /// <summary>
    /// Authoring ScriptableObject for spell signature catalog.
    /// </summary>
    public class SpellSignatureCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class SignatureDefinition
        {
            [Header("Identity")]
            public string signatureId;
            public string displayName;
            public string description;

            [Header("Configuration")]
            public SignatureType type = SignatureType.Multishot;
            [Range(0.1f, 10f)]
            public float modifierValue = 1.0f;
            public string targetSpellSchool = ""; // Empty = applies to all
        }

        public List<SignatureDefinition> signatures = new List<SignatureDefinition>();

        private void OnValidate()
        {
            // Validate signature IDs are unique
            var ids = new HashSet<string>();
            foreach (var sig in signatures)
            {
                if (string.IsNullOrEmpty(sig.signatureId))
                {
                    Debug.LogWarning($"Signature with empty ID found in {name}");
                    continue;
                }
                if (ids.Contains(sig.signatureId))
                {
                    Debug.LogWarning($"Duplicate signature ID '{sig.signatureId}' in {name}");
                }
                ids.Add(sig.signatureId);
            }
        }
    }

    /// <summary>
    /// Baker for SpellSignatureCatalogAuthoring.
    /// </summary>
    public class SpellSignatureCatalogBaker : Baker<SpellSignatureCatalogAuthoring>
    {
        public override void Bake(SpellSignatureCatalogAuthoring authoring)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SpellSignatureBlob>();

            var signaturesList = new List<SignatureEntry>();
            foreach (var sigDef in authoring.signatures)
            {
                signaturesList.Add(new SignatureEntry
                {
                    SignatureId = new FixedString64Bytes(sigDef.signatureId),
                    DisplayName = new FixedString64Bytes(sigDef.displayName),
                    Type = sigDef.type,
                    ModifierValue = sigDef.modifierValue,
                    TargetSpellSchool = new FixedString64Bytes(sigDef.targetSpellSchool),
                    Description = new FixedString128Bytes(sigDef.description)
                });
            }

            var signaturesArray = builder.Allocate(ref root.Signatures, signaturesList.Count);
            for (int i = 0; i < signaturesList.Count; i++)
            {
                signaturesArray[i] = signaturesList[i];
            }

            var blobAsset = builder.CreateBlobAssetReference<SpellSignatureBlob>(Allocator.Persistent);
            builder.Dispose();

            // Create singleton entity with catalog reference
            var entity = GetEntity(TransformUsageFlags.None);
            AddBlobAsset(ref blobAsset, out _);
            AddComponent(entity, new SpellSignatureCatalogRef { Blob = blobAsset });
        }
    }
}
#endif

