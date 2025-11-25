using System;
using System.Collections.Generic;
using Space4X.Registry;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.Models
{
    /// <summary>
    /// Base template class for all prefab categories.
    /// Provides common fields and derived properties for UI display.
    /// </summary>
    [Serializable]
    public abstract class PrefabTemplate
    {
        public string id;
        public string displayName;
        public string description;
        
        // Style tokens
        public byte palette;
        public byte roughness;
        public byte pattern;
        
        // Validation state
        public bool isValid = true;
        public List<string> validationIssues = new List<string>();
        
        // Derived properties (computed from catalog data)
        public abstract string GetSummary();
        public abstract string GetCategoryName();
    }
    
    /// <summary>
    /// Hull template with socket and variant information.
    /// </summary>
    [Serializable]
    public class HullTemplate : PrefabTemplate
    {
        public float baseMassTons;
        public bool fieldRefitAllowed;
        public HullCategory category;
        public float hangarCapacity;
        public string presentationArchetype;
        public HullVariant variant;
        public List<string> builtInModuleLoadouts = new List<string>();
        
        // Socket data
        public List<HullSlotTemplate> slots = new List<HullSlotTemplate>();
        
        // Derived properties
        public int TotalSocketCount => slots?.Count ?? 0;
        public Dictionary<string, int> SocketCountsByType
        {
            get
            {
                var counts = new Dictionary<string, int>();
                if (slots == null) return counts;
                
                foreach (var slot in slots)
                {
                    var key = $"{slot.type}_{slot.size}";
                    if (!counts.ContainsKey(key)) counts[key] = 0;
                    counts[key]++;
                }
                return counts;
            }
        }
        
        public override string GetSummary()
        {
            return $"{category} | {TotalSocketCount} sockets | Mass: {baseMassTons:F1}t | Hangar: {hangarCapacity:F1}";
        }
        
        public override string GetCategoryName() => "Hull";
    }
    
    [Serializable]
    public class HullSlotTemplate
    {
        public MountType type;
        public MountSize size;
    }
    
    /// <summary>
    /// Module template with mount requirements and facility metadata.
    /// </summary>
    [Serializable]
    public class ModuleTemplate : PrefabTemplate
    {
        public ModuleClass moduleClass;
        public MountType requiredMount;
        public MountSize requiredSize;
        public float massTons;
        public float powerDrawMW;
        public byte offenseRating;
        public byte defenseRating;
        public byte utilityRating;
        public float defaultEfficiency;
        
        // Function metadata
        public ModuleFunction function;
        public float functionCapacity;
        public string functionDescription;
        
        // Quality/rarity/tier/manufacturer
        public float quality;
        public ModuleRarity rarity;
        public byte tier;
        public string manufacturerId;
        
        // Facility metadata
        public FacilityArchetype facilityArchetype;
        public FacilityTier facilityTier;
        
        // Derived properties
        public string MountSummary => $"{requiredMount} {requiredSize}";
        public string QualitySummary => $"Q:{quality:F2} R:{rarity} T:{tier}";
        
        public override string GetSummary()
        {
            var summary = $"{moduleClass} | {MountSummary} | {QualitySummary}";
            if (function != ModuleFunction.None)
                summary += $" | {function}";
            return summary;
        }
        
        public override string GetCategoryName() => "Module";
    }
    
    /// <summary>
    /// Station template with refit facility information.
    /// </summary>
    [Serializable]
    public class StationTemplate : PrefabTemplate
    {
        public bool hasRefitFacility;
        public float facilityZoneRadius;
        public string presentationArchetype;
        
        public override string GetSummary()
        {
            var summary = "Station";
            if (hasRefitFacility)
                summary += $" | Refit Facility (R:{facilityZoneRadius:F1})";
            return summary;
        }
        
        public override string GetCategoryName() => "Station";
    }
    
    /// <summary>
    /// Resource template.
    /// </summary>
    [Serializable]
    public class ResourceTemplate : PrefabTemplate
    {
        public string presentationArchetype;
        
        public override string GetSummary()
        {
            return $"Resource | {presentationArchetype}";
        }
        
        public override string GetCategoryName() => "Resource";
    }
    
    /// <summary>
    /// Product template.
    /// </summary>
    [Serializable]
    public class ProductTemplate : PrefabTemplate
    {
        public string presentationArchetype;
        public byte requiredTechTier;
        
        public override string GetSummary()
        {
            return $"Product | Tier {requiredTechTier} | {presentationArchetype}";
        }
        
        public override string GetCategoryName() => "Product";
    }
    
    /// <summary>
    /// Aggregate template with outlook/alignment composition.
    /// </summary>
    [Serializable]
    public class AggregateTemplate : PrefabTemplate
    {
        public bool useComposedProfiles = false;
        public string templateId;
        public string outlookId;
        public string alignmentId;
        public string personalityId;
        public string themeId;
        
        // Resolved policy fields (optional, can be computed from profiles)
        public Dictionary<string, float> policyFields = new Dictionary<string, float>();
        
        public override string GetSummary()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(templateId)) parts.Add($"T:{templateId}");
            if (!string.IsNullOrEmpty(outlookId)) parts.Add($"O:{outlookId}");
            if (!string.IsNullOrEmpty(alignmentId)) parts.Add($"A:{alignmentId}");
            return string.Join(" | ", parts);
        }
        
        public override string GetCategoryName() => "Aggregate";
    }
    
    /// <summary>
    /// Effect template for VFX/visual effects.
    /// </summary>
    [Serializable]
    public class EffectTemplate : PrefabTemplate
    {
        public string presentationArchetype;
        
        public override string GetSummary()
        {
            return $"Effect | {presentationArchetype}";
        }
        
        public override string GetCategoryName() => "FX";
    }
    
    /// <summary>
    /// Individual entity template (captains, officers, crew).
    /// </summary>
    [Serializable]
    public class IndividualTemplate : PrefabTemplate
    {
        public IndividualRole role;
        
        // Core stats
        public float command;
        public float tactics;
        public float logistics;
        public float diplomacy;
        public float engineering;
        public float resolve;
        
        // Physique/Finesse/Will
        public float physique;
        public float finesse;
        public float will;
        public float physiqueInclination;
        public float finesseInclination;
        public float willInclination;
        
        // Alignment
        public float law;
        public float good;
        public float integrity;
        
        // Race/Culture
        public ushort raceId;
        public ushort cultureId;
        
        // Progression
        public Space4X.Registry.PreordainTrack preordainTrack;
        public List<TitleData> titles = new List<TitleData>();
        
        // Lineage
        public string lineageId;
        
        // Contract
        public Space4X.Registry.ContractType contractType;
        public string employerId;
        public float contractDurationYears;
        
        // Relations
        public List<LoyaltyEntry> loyaltyScores = new List<LoyaltyEntry>();
        public List<OwnershipStake> ownershipStakes = new List<OwnershipStake>();
        public string mentorId;
        public List<string> menteeIds = new List<string>();
        public List<PatronageEntry> patronages = new List<PatronageEntry>();
        public List<SuccessorEntry> successors = new List<SuccessorEntry>();
        
        // Derived properties
        public string StatsSummary => $"Cmd:{command:F0} Tac:{tactics:F0} Log:{logistics:F0} Dip:{diplomacy:F0} Eng:{engineering:F0} Res:{resolve:F0}";
        
        public override string GetSummary()
        {
            return $"{role} | {StatsSummary}";
        }
        
        public override string GetCategoryName() => "Individual";
    }
    
    [Serializable]
    public enum IndividualRole
    {
        Captain,
        Legend,
        AceOfficer,
        JuniorOfficer,
        CrewSpecialist
    }
    
    
    [Serializable]
    public class TitleData
    {
        public byte tier;
        public TitleType type;
        public byte level;
        public TitleState state;
        public string displayName;
        public string colonyId;
        public string factionId;
        public string empireId;
        public string acquisitionReason;
        public string lossReason;
    }
    
    [Serializable]
    public enum TitleType
    {
        Captain,
        Admiral,
        Governor,
        StellarLord,
        Stellarch
    }
    
    [Serializable]
    public enum TitleState
    {
        Active,
        Inactive,
        Revoked
    }
    
    [Serializable]
    public enum SuccessorType
    {
        Heir,
        Protege
    }
    
    [Serializable]
    public class LoyaltyEntry
    {
        public LoyaltyTargetType targetType;
        public string targetId;
        public float loyalty;
    }
    
    [Serializable]
    public enum LoyaltyTargetType
    {
        Empire,
        Lineage,
        Guild
    }
    
    [Serializable]
    public class OwnershipStake
    {
        public string assetType; // String-based for flexibility
        public string assetId;
        public float ownershipPercentage;
    }
    
    [Serializable]
    public class PatronageEntry
    {
        public AggregateType aggregateType;
        public string aggregateId;
        public string role;
    }
    
    [Serializable]
    public enum AggregateType
    {
        Dynasty,
        Guild,
        Corporation,
        Army,
        Band
    }
    
    [Serializable]
    public class SuccessorEntry
    {
        public string successorId;
        public float inheritancePercentage;
        public SuccessorType type;
    }
    
    /// <summary>
    /// Weapon template (data-driven, no GameObject logic).
    /// </summary>
    [Serializable]
    public class WeaponTemplate : PrefabTemplate
    {
        public Space4X.Registry.WeaponClass weaponClass;
        public float fireRate;
        public byte burstCount;
        public float spreadDeg;
        public float energyCost;
        public float heatCost;
        public float leadBias;
        public string projectileId;
        
        public override string GetSummary()
        {
            return $"{weaponClass} | {fireRate:F1} shots/s | Burst: {burstCount} | Spread: {spreadDeg:F1}°";
        }
        
        public override string GetCategoryName() => "Weapon";
    }
    
    /// <summary>
    /// Projectile template (data-driven, no GameObject logic).
    /// </summary>
    [Serializable]
    public class ProjectileTemplate : PrefabTemplate
    {
        public Space4X.Registry.ProjectileKind kind;
        public float speed;
        public float lifetime;
        public float gravity;
        public float turnRateDeg;
        public float seekRadius;
        public float pierce;
        public float chainRange;
        public float aoERadius;
        public DamageModelTemplate damage;
        public List<EffectOpTemplate> onHitEffects = new List<EffectOpTemplate>();
        
        public override string GetSummary()
        {
            var summary = $"{kind} | Speed: {speed:F0} m/s | Lifetime: {lifetime:F1}s";
            if (kind == Space4X.Registry.ProjectileKind.Missile && turnRateDeg > 0)
                summary += $" | Turn: {turnRateDeg:F0}°/s";
            if (aoERadius > 0)
                summary += $" | AoE: {aoERadius:F1}m";
            return summary;
        }
        
        public override string GetCategoryName() => "Projectile";
    }
    
    [Serializable]
    public class DamageModelTemplate
    {
        public float kinetic;
        public float energy;
        public float explosive;
    }
    
    [Serializable]
    public class EffectOpTemplate
    {
        public byte kind;
        public float magnitude;
        public float duration;
        public uint statusId;
    }
    
    /// <summary>
    /// Turret template (data-driven, no GameObject logic).
    /// </summary>
    [Serializable]
    public class TurretTemplate : PrefabTemplate
    {
        public float arcLimitDeg;
        public float traverseSpeedDegPerSec;
        public float elevationMinDeg;
        public float elevationMaxDeg;
        public float recoilForce;
        public string socketName;
        
        public override string GetSummary()
        {
            return $"Arc: {arcLimitDeg:F0}° | Traverse: {traverseSpeedDegPerSec:F0}°/s | Elevation: {elevationMinDeg:F0}° to {elevationMaxDeg:F0}°";
        }
        
        public override string GetCategoryName() => "Turret";
    }
}

