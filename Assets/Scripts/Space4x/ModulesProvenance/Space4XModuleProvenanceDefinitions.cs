using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.ModulesProvenance
{
    public struct ManufacturerId : IEquatable<ManufacturerId>
    {
        public ushort Value;

        public bool Equals(ManufacturerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ManufacturerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    public struct OrgId : IEquatable<OrgId>
    {
        public ushort Value;

        public bool Equals(OrgId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is OrgId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    public struct BlueprintId : IEquatable<BlueprintId>
    {
        public ushort Family;
        public byte Mark;
        public byte Variant;

        public bool Equals(BlueprintId other)
            => Family == other.Family && Mark == other.Mark && Variant == other.Variant;

        public override bool Equals(object obj) => obj is BlueprintId other && Equals(other);
        public override int GetHashCode() => ((Family << 16) | (Mark << 8) | Variant).GetHashCode();

        public uint StableHash()
        {
            return math.hash(new uint3(Family, Mark, Variant));
        }
    }

    public enum BlueprintProvenanceKind : byte
    {
        Original = 0,
        Licensed = 1,
        ReverseEngineered = 2,
        Iterated = 3
    }

    public struct BlueprintProvenance
    {
        public OrgId OriginOrgId;
        public BlueprintProvenanceKind ProvenanceKind;
        public float KnowledgeLevel;
        public float ProcessMaturity;

        public BlueprintProvenance Clamp01()
        {
            KnowledgeLevel = math.saturate(KnowledgeLevel);
            ProcessMaturity = math.saturate(ProcessMaturity);
            return this;
        }
    }

    public enum DivisionId : byte
    {
        Cooling = 0,
        Power = 1,
        Optics = 2,
        Mounting = 3,
        Firmware = 4
    }

    public struct LimbContribution
    {
        public float DamageWeight;
        public float FireRateWeight;
        public float HeatEfficiencyWeight;
        public float TrackingWeight;
        public float ReliabilityWeight;
    }

    public struct LimbInputRequirement
    {
        public FixedString64Bytes InputId;
        public float Quantity;
        public byte IsPart;
    }

    public struct PartLimbSpec
    {
        public DivisionId DivisionId;
        public byte Quantity;
        public FixedList512Bytes<LimbInputRequirement> RequiredInputs;
        public LimbContribution LimbContribution;
    }

    public struct ModuleBOMLimbRequirement
    {
        public DivisionId DivisionId;
        public byte Quantity;
        public float MinLimbQuality;
    }

    public struct ManufacturerMarkConstraint
    {
        public ManufacturerId ManufacturerId;
        public byte MinMark;
        public byte MaxMark;
    }

    public struct ModuleBOM
    {
        public BlueprintId BlueprintId;
        public FixedList512Bytes<ModuleBOMLimbRequirement> RequiredLimbs;
        public FixedList128Bytes<ManufacturerMarkConstraint> AllowedManufacturers;
    }

    public struct PartLimbSpecBlob
    {
        public DivisionId DivisionId;
        public byte Quantity;
        public LimbContribution LimbContribution;
        public BlobArray<LimbInputRequirement> RequiredInputs;
    }

    public struct ModuleBOMBlob
    {
        public BlueprintId BlueprintId;
        public BlobArray<PartLimbSpecBlob> LimbSpecs;
        public BlobArray<ModuleBOMLimbRequirement> RequiredLimbs;
        public BlobArray<ManufacturerMarkConstraint> AllowedManufacturers;
    }

    [Serializable]
    public class LimbInputAuthoringData
    {
        public string inputId;
        public float quantity = 1f;
        public bool isPart;
    }

    [Serializable]
    public class LimbContributionAuthoringData
    {
        public float damageWeight;
        public float fireRateWeight;
        public float heatEfficiencyWeight;
        public float trackingWeight;
        public float reliabilityWeight;
    }

    [Serializable]
    public class PartLimbSpecAuthoringData
    {
        public DivisionId divisionId;
        public int quantity = 1;
        public LimbContributionAuthoringData limbContribution = new LimbContributionAuthoringData();
        public List<LimbInputAuthoringData> requiredInputs = new List<LimbInputAuthoringData>();
    }

    [Serializable]
    public class ModuleBOMLimbRequirementAuthoringData
    {
        public DivisionId divisionId;
        public int quantity = 1;
        public float minLimbQuality = 0.5f;
    }

    [Serializable]
    public class ManufacturerMarkConstraintAuthoringData
    {
        public int manufacturerId;
        public int minMark = 1;
        public int maxMark = 1;
    }

    [Serializable]
    public class BlueprintProvenanceAuthoringData
    {
        public int originOrgId;
        public BlueprintProvenanceKind provenanceKind = BlueprintProvenanceKind.Original;
        [Range(0f, 1f)] public float knowledgeLevel = 1f;
        [Range(0f, 1f)] public float processMaturity = 1f;
    }

    [Serializable]
    public class ModuleBlueprintAuthoringData
    {
        public string blueprintName;
        public int family;
        public int mark = 1;
        public int variant;
        public BlueprintProvenanceAuthoringData provenance = new BlueprintProvenanceAuthoringData();
        public List<PartLimbSpecAuthoringData> limbSpecs = new List<PartLimbSpecAuthoringData>();
        public List<ModuleBOMLimbRequirementAuthoringData> requiredLimbs = new List<ModuleBOMLimbRequirementAuthoringData>();
        public List<ManufacturerMarkConstraintAuthoringData> allowedManufacturers = new List<ManufacturerMarkConstraintAuthoringData>();
    }

    [CreateAssetMenu(menuName = "Space4X/Modules/Module Blueprint v0", fileName = "Space4XModuleBlueprintV0")]
    public class ModuleBlueprintAuthoringAsset : ScriptableObject
    {
        public ModuleBlueprintAuthoringData data = new ModuleBlueprintAuthoringData();
    }

    public static class ModuleBlueprintBlobBuilder
    {
        public static ModuleBOM CreateModuleBom(ModuleBlueprintAuthoringData data)
        {
            var bom = new ModuleBOM
            {
                BlueprintId = new BlueprintId
                {
                    Family = (ushort)math.clamp(data?.family ?? 0, 0, ushort.MaxValue),
                    Mark = (byte)math.clamp(data?.mark ?? 0, 0, byte.MaxValue),
                    Variant = (byte)math.clamp(data?.variant ?? 0, 0, byte.MaxValue)
                }
            };

            if (data?.requiredLimbs != null)
            {
                for (var i = 0; i < data.requiredLimbs.Count; i++)
                {
                    var limb = data.requiredLimbs[i];
                    bom.RequiredLimbs.Add(new ModuleBOMLimbRequirement
                    {
                        DivisionId = limb.divisionId,
                        Quantity = (byte)math.clamp(limb.quantity, 0, byte.MaxValue),
                        MinLimbQuality = math.saturate(limb.minLimbQuality)
                    });
                }
            }

            if (data?.allowedManufacturers != null)
            {
                for (var i = 0; i < data.allowedManufacturers.Count; i++)
                {
                    var constraint = data.allowedManufacturers[i];
                    bom.AllowedManufacturers.Add(new ManufacturerMarkConstraint
                    {
                        ManufacturerId = new ManufacturerId { Value = (ushort)math.clamp(constraint.manufacturerId, 0, ushort.MaxValue) },
                        MinMark = (byte)math.clamp(constraint.minMark, 0, byte.MaxValue),
                        MaxMark = (byte)math.clamp(constraint.maxMark, 0, byte.MaxValue)
                    });
                }
            }

            return bom;
        }

        public static BlobAssetReference<ModuleBOMBlob> BuildBlob(ModuleBlueprintAuthoringData data, Allocator allocator = Allocator.Persistent)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ModuleBOMBlob>();

            root.BlueprintId = new BlueprintId
            {
                Family = (ushort)math.clamp(data?.family ?? 0, 0, ushort.MaxValue),
                Mark = (byte)math.clamp(data?.mark ?? 0, 0, byte.MaxValue),
                Variant = (byte)math.clamp(data?.variant ?? 0, 0, byte.MaxValue)
            };

            var limbSpecs = data?.limbSpecs ?? new List<PartLimbSpecAuthoringData>();
            var requiredLimbs = data?.requiredLimbs ?? new List<ModuleBOMLimbRequirementAuthoringData>();
            var allowedManufacturers = data?.allowedManufacturers ?? new List<ManufacturerMarkConstraintAuthoringData>();

            var limbSpecArray = builder.Allocate(ref root.LimbSpecs, limbSpecs.Count);
            for (var i = 0; i < limbSpecs.Count; i++)
            {
                var source = limbSpecs[i];
                limbSpecArray[i].DivisionId = source.divisionId;
                limbSpecArray[i].Quantity = (byte)math.clamp(source.quantity, 0, byte.MaxValue);
                limbSpecArray[i].LimbContribution = new LimbContribution
                {
                    DamageWeight = source.limbContribution?.damageWeight ?? 0f,
                    FireRateWeight = source.limbContribution?.fireRateWeight ?? 0f,
                    HeatEfficiencyWeight = source.limbContribution?.heatEfficiencyWeight ?? 0f,
                    TrackingWeight = source.limbContribution?.trackingWeight ?? 0f,
                    ReliabilityWeight = source.limbContribution?.reliabilityWeight ?? 0f
                };

                var inputs = source.requiredInputs ?? new List<LimbInputAuthoringData>();
                var inputArray = builder.Allocate(ref limbSpecArray[i].RequiredInputs, inputs.Count);
                for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
                {
                    var input = inputs[inputIndex];
                    inputArray[inputIndex] = new LimbInputRequirement
                    {
                        InputId = new FixedString64Bytes(input.inputId ?? string.Empty),
                        Quantity = math.max(0f, input.quantity),
                        IsPart = (byte)(input.isPart ? 1 : 0)
                    };
                }
            }

            var requiredArray = builder.Allocate(ref root.RequiredLimbs, requiredLimbs.Count);
            for (var i = 0; i < requiredLimbs.Count; i++)
            {
                var source = requiredLimbs[i];
                requiredArray[i] = new ModuleBOMLimbRequirement
                {
                    DivisionId = source.divisionId,
                    Quantity = (byte)math.clamp(source.quantity, 0, byte.MaxValue),
                    MinLimbQuality = math.saturate(source.minLimbQuality)
                };
            }

            var manufacturerArray = builder.Allocate(ref root.AllowedManufacturers, allowedManufacturers.Count);
            for (var i = 0; i < allowedManufacturers.Count; i++)
            {
                var source = allowedManufacturers[i];
                manufacturerArray[i] = new ManufacturerMarkConstraint
                {
                    ManufacturerId = new ManufacturerId { Value = (ushort)math.clamp(source.manufacturerId, 0, ushort.MaxValue) },
                    MinMark = (byte)math.clamp(source.minMark, 0, byte.MaxValue),
                    MaxMark = (byte)math.clamp(source.maxMark, 0, byte.MaxValue)
                };
            }

            var blob = builder.CreateBlobAssetReference<ModuleBOMBlob>(allocator);
            builder.Dispose();
            return blob;
        }
    }

    public static class ModuleBlueprintExamples
    {
        public static readonly BlueprintId LaserSMk1RapidBlueprintId = new BlueprintId
        {
            Family = 1101,
            Mark = 1,
            Variant = 2
        };

        public static ModuleBlueprintAuthoringData CreateLaserSMk1RapidAuthoring()
        {
            return new ModuleBlueprintAuthoringData
            {
                blueprintName = "Laser_S_Mk1_Rapid",
                family = 1101,
                mark = 1,
                variant = 2,
                provenance = new BlueprintProvenanceAuthoringData
                {
                    originOrgId = 17,
                    provenanceKind = BlueprintProvenanceKind.Original,
                    knowledgeLevel = 1f,
                    processMaturity = 0.74f
                },
                limbSpecs = new List<PartLimbSpecAuthoringData>
                {
                    CreateLimb(
                        DivisionId.Cooling,
                        1,
                        new LimbContributionAuthoringData
                        {
                            heatEfficiencyWeight = 0.45f,
                            fireRateWeight = 0.20f,
                            reliabilityWeight = 0.20f,
                            damageWeight = 0.10f,
                            trackingWeight = 0.05f
                        },
                        new LimbInputAuthoringData { inputId = "coolant_loop_s", quantity = 1f, isPart = true },
                        new LimbInputAuthoringData { inputId = "alloy_titanium", quantity = 2f, isPart = false }),
                    CreateLimb(
                        DivisionId.Power,
                        1,
                        new LimbContributionAuthoringData
                        {
                            damageWeight = 0.30f,
                            fireRateWeight = 0.35f,
                            heatEfficiencyWeight = 0.15f,
                            reliabilityWeight = 0.15f,
                            trackingWeight = 0.05f
                        },
                        new LimbInputAuthoringData { inputId = "capacitor_s", quantity = 1f, isPart = true },
                        new LimbInputAuthoringData { inputId = "conductor_crystal", quantity = 1f, isPart = false }),
                    CreateLimb(
                        DivisionId.Optics,
                        1,
                        new LimbContributionAuthoringData
                        {
                            trackingWeight = 0.45f,
                            damageWeight = 0.25f,
                            reliabilityWeight = 0.20f,
                            fireRateWeight = 0.05f,
                            heatEfficiencyWeight = 0.05f
                        },
                        new LimbInputAuthoringData { inputId = "lens_array_s", quantity = 1f, isPart = true },
                        new LimbInputAuthoringData { inputId = "optical_glass", quantity = 2f, isPart = false }),
                    CreateLimb(
                        DivisionId.Mounting,
                        1,
                        new LimbContributionAuthoringData
                        {
                            reliabilityWeight = 0.50f,
                            trackingWeight = 0.20f,
                            heatEfficiencyWeight = 0.15f,
                            damageWeight = 0.10f,
                            fireRateWeight = 0.05f
                        },
                        new LimbInputAuthoringData { inputId = "mount_ring_s", quantity = 1f, isPart = true },
                        new LimbInputAuthoringData { inputId = "alloy_structural", quantity = 2f, isPart = false }),
                    CreateLimb(
                        DivisionId.Firmware,
                        1,
                        new LimbContributionAuthoringData
                        {
                            fireRateWeight = 0.35f,
                            trackingWeight = 0.25f,
                            reliabilityWeight = 0.25f,
                            damageWeight = 0.10f,
                            heatEfficiencyWeight = 0.05f
                        },
                        new LimbInputAuthoringData { inputId = "firmware_pkg_rapid_mk1", quantity = 1f, isPart = true },
                        new LimbInputAuthoringData { inputId = "signal_processor_die", quantity = 1f, isPart = false })
                },
                requiredLimbs = new List<ModuleBOMLimbRequirementAuthoringData>
                {
                    new ModuleBOMLimbRequirementAuthoringData { divisionId = DivisionId.Cooling, quantity = 1, minLimbQuality = 0.45f },
                    new ModuleBOMLimbRequirementAuthoringData { divisionId = DivisionId.Power, quantity = 1, minLimbQuality = 0.50f },
                    new ModuleBOMLimbRequirementAuthoringData { divisionId = DivisionId.Optics, quantity = 1, minLimbQuality = 0.52f },
                    new ModuleBOMLimbRequirementAuthoringData { divisionId = DivisionId.Mounting, quantity = 1, minLimbQuality = 0.48f },
                    new ModuleBOMLimbRequirementAuthoringData { divisionId = DivisionId.Firmware, quantity = 1, minLimbQuality = 0.50f }
                },
                allowedManufacturers = new List<ManufacturerMarkConstraintAuthoringData>
                {
                    new ManufacturerMarkConstraintAuthoringData { manufacturerId = 17, minMark = 1, maxMark = 2 },
                    new ManufacturerMarkConstraintAuthoringData { manufacturerId = 29, minMark = 1, maxMark = 1 }
                }
            };
        }

        public static ModuleBOM CreateLaserSMk1RapidBom()
        {
            var bom = new ModuleBOM
            {
                BlueprintId = LaserSMk1RapidBlueprintId
            };

            bom.RequiredLimbs.Add(new ModuleBOMLimbRequirement { DivisionId = DivisionId.Cooling, Quantity = 1, MinLimbQuality = 0.45f });
            bom.RequiredLimbs.Add(new ModuleBOMLimbRequirement { DivisionId = DivisionId.Power, Quantity = 1, MinLimbQuality = 0.50f });
            bom.RequiredLimbs.Add(new ModuleBOMLimbRequirement { DivisionId = DivisionId.Optics, Quantity = 1, MinLimbQuality = 0.52f });
            bom.RequiredLimbs.Add(new ModuleBOMLimbRequirement { DivisionId = DivisionId.Mounting, Quantity = 1, MinLimbQuality = 0.48f });
            bom.RequiredLimbs.Add(new ModuleBOMLimbRequirement { DivisionId = DivisionId.Firmware, Quantity = 1, MinLimbQuality = 0.50f });

            bom.AllowedManufacturers.Add(new ManufacturerMarkConstraint
            {
                ManufacturerId = new ManufacturerId { Value = 17 },
                MinMark = 1,
                MaxMark = 2
            });
            bom.AllowedManufacturers.Add(new ManufacturerMarkConstraint
            {
                ManufacturerId = new ManufacturerId { Value = 29 },
                MinMark = 1,
                MaxMark = 1
            });
            return bom;
        }

        private static PartLimbSpecAuthoringData CreateLimb(
            DivisionId divisionId,
            int quantity,
            LimbContributionAuthoringData contribution,
            params LimbInputAuthoringData[] inputs)
        {
            var limb = new PartLimbSpecAuthoringData
            {
                divisionId = divisionId,
                quantity = quantity,
                limbContribution = contribution,
                requiredInputs = new List<LimbInputAuthoringData>()
            };

            if (inputs != null)
            {
                for (var i = 0; i < inputs.Length; i++)
                {
                    limb.requiredInputs.Add(inputs[i]);
                }
            }

            return limb;
        }
    }
}
