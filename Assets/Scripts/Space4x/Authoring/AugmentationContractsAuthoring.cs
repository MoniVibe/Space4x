using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for augmentation contracts (installer provenance, warranty, legal status).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Augmentation Contracts")]
    public sealed class AugmentationContractsAuthoring : MonoBehaviour
    {
        [Serializable]
        public class AugmentContract
        {
            [Tooltip("Augment ID")]
            public string augmentId = string.Empty;
            [Tooltip("Installer type (Doc = licensed medtech, Ripper = illicit surgeon)")]
            public InstallerType installerType = InstallerType.Doc;
            [Tooltip("Installer ID")]
            public string installerId = string.Empty;
            [Tooltip("Warranty duration in ticks (0 = no warranty)")]
            public uint warrantyDurationTicks = 0;
            [Tooltip("Legal status")]
            public AugmentationCatalogAuthoring.LegalStatus legalStatus = AugmentationCatalogAuthoring.LegalStatus.Licensed;
        }

        public enum InstallerType : byte
        {
            Doc = 0,      // Licensed medtech
            Ripper = 1    // Illicit surgeon
        }

        [Tooltip("Augmentation contracts (one per installed augment)")]
        public List<AugmentContract> contracts = new List<AugmentContract>();

        public sealed class Baker : Unity.Entities.Baker<AugmentationContractsAuthoring>
        {
            public override void Bake(AugmentationContractsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.AugmentationContract>(entity);

                if (authoring.contracts != null)
                {
                    foreach (var contract in authoring.contracts)
                    {
                        if (!string.IsNullOrWhiteSpace(contract.augmentId))
                        {
                            buffer.Add(new Registry.AugmentationContract
                            {
                                AugmentId = new FixedString64Bytes(contract.augmentId),
                                InstallerType = (Registry.InstallerType)contract.installerType,
                                InstallerId = new FixedString64Bytes(contract.installerId ?? string.Empty),
                                WarrantyDurationTicks = contract.warrantyDurationTicks,
                                LegalStatus = (Registry.LegalStatus)contract.legalStatus
                            });
                        }
                    }
                }
            }
        }
    }
}

