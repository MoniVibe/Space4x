using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Compliance
{
    /// <summary>
    /// Compliance rule specification - defines what actions are forbidden/required/taxed/subsidized.
    /// </summary>
    public struct ComplianceRule
    {
        public FixedString32Bytes Id;
        public byte Verb; // ComplianceVerb enum
        public uint TargetTags; // Bitmask of tags that trigger this rule
        public float Magnitude; // Fine amount, tax rate, subsidy amount, etc.
        public byte Enforcement; // EnforcementLevel enum
    }

    /// <summary>
    /// Compliance verb - what action the rule applies.
    /// </summary>
    public enum ComplianceVerb : byte
    {
        Forbid = 0,
        Require = 1,
        Tax = 2,
        Subsidize = 3
    }

    /// <summary>
    /// Enforcement level - how strictly the rule is enforced.
    /// </summary>
    public enum EnforcementLevel : byte
    {
        Warning = 0,
        Fine = 1,
        ReputationHit = 2,
        Interdiction = 3,
        Bounty = 4
    }

    /// <summary>
    /// Compliance tags - bitmask flags for rule targeting.
    /// </summary>
    [System.Flags]
    public enum ComplianceTags : uint
    {
        None = 0,
        Piracy = 1 << 0,
        Smuggling = 1 << 1,
        Collateral = 1 << 2,
        RestrictedWeapons = 1 << 3,
        SafeZoneViolation = 1 << 4,
        CargoScan = 1 << 5,
        Boarding = 1 << 6
    }

    /// <summary>
    /// Blob catalog for compliance rules.
    /// </summary>
    public struct ComplianceRuleCatalogBlob
    {
        public BlobArray<ComplianceRule> Rules;
    }

    /// <summary>
    /// Singleton component holding compliance rule catalog reference.
    /// </summary>
    public struct ComplianceRuleCatalog : IComponentData
    {
        public BlobAssetReference<ComplianceRuleCatalogBlob> Catalog;
    }

    /// <summary>
    /// Component marking an infraction event.
    /// </summary>
    public struct ComplianceInfraction : IComponentData
    {
        public FixedString32Bytes RuleId;
        public Entity OffenderEntity;
        public uint InfractionTick;
        public ComplianceTags TriggerTags;
        public float Severity; // 0-1, affects sanction magnitude
    }

    /// <summary>
    /// Component marking a sanction applied to an entity.
    /// </summary>
    public struct ComplianceSanction : IComponentData
    {
        public FixedString32Bytes RuleId;
        public EnforcementLevel Level;
        public float FineAmount;
        public float ReputationHit;
        public bool IsBountyFlagged;
        public uint SanctionTick;
    }
}

