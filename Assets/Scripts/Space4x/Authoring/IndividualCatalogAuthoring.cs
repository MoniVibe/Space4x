using System;
using System.Collections.Generic;
using Space4X.Registry;
using UnityEngine;
using TitleType = Space4X.Registry.TitleType;

namespace Space4X.Authoring
{
    /// <summary>
    /// Catalog for individual entities (captains, officers, crew specialists).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Individual Catalog")]
    public sealed class IndividualCatalogAuthoring : MonoBehaviour
    {
        public enum IndividualRole : byte
        {
            CrewSpecialist = 0,
            JuniorOfficer = 1,
            AceOfficer = 2,
            Captain = 3,
            Legend = 4
        }

        [Serializable]
        public class IndividualSpecData
        {
            public string id;
            public IndividualRole role = IndividualRole.CrewSpecialist;
            
            [Header("Stats")]
            [Range(0f, 100f)] public float command = 50f;
            [Range(0f, 100f)] public float tactics = 50f;
            [Range(0f, 100f)] public float logistics = 50f;
            [Range(0f, 100f)] public float diplomacy = 50f;
            [Range(0f, 100f)] public float engineering = 50f;
            [Range(0f, 100f)] public float resolve = 50f;
            
            [Header("Physique/Finesse/Will")]
            [Range(0f, 100f)] public float physique = 50f;
            [Range(0f, 100f)] public float finesse = 50f;
            [Range(0f, 100f)] public float will = 50f;
            [Range(1, 10)] public int physiqueInclination = 5;
            [Range(1, 10)] public int finesseInclination = 5;
            [Range(1, 10)] public int willInclination = 5;
            
            [Header("Alignment")]
            [Range(-1f, 1f)] public float law = 0f;
            [Range(-1f, 1f)] public float good = 0f;
            [Range(-1f, 1f)] public float integrity = 0f;
            public ushort raceId = 0;
            public ushort cultureId = 0;
            
            [Header("Progression")]
            public PreordainTrack preordainTrack = PreordainTrack.None;
            public string lineageId = string.Empty;
            
            [Header("Titles (Deeds of Rulership)")]
            [Tooltip("Titles held by this individual. Titles can be acquired through founding colonies, defending them, renown, inheritance, etc.")]
            public List<TitleData> titles = new List<TitleData>();
            
            [Header("Contract")]
            public ContractType contractType = ContractType.Fleet;
            public string employerId = string.Empty;
            [Range(1, 5)] public int contractDurationYears = 1;
            
            [Header("Relations (Optional)")]
            [Tooltip("Loyalty scores (Empire, Lineage, Guild)")]
            public List<LoyaltyEntry> loyaltyScores = new List<LoyaltyEntry>();
            [Tooltip("Ownership stakes in facilities/manufacturers")]
            public List<OwnershipStakeEntry> ownershipStakes = new List<OwnershipStakeEntry>();
            [Tooltip("Mentor ID (high-expertise individual who trains this one)")]
            public string mentorId = string.Empty;
            [Tooltip("Mentee IDs (juniors being trained by this individual)")]
            public string[] menteeIds = new string[0];
            [Tooltip("Patronage memberships (aggregate memberships)")]
            public List<PatronageEntry> patronages = new List<PatronageEntry>();
            [Tooltip("Successors (heirs/proteges who inherit partial expertise)")]
            public List<SuccessorEntry> successors = new List<SuccessorEntry>();
        }

        [Serializable]
        public class TitleData
        {
            [Tooltip("Title tier (Captain, Admiral, Governor, StellarLord, etc.)")]
            public TitleTier tier = TitleTier.None;

            [Tooltip("Title type: Hero (founding/defending colony), Elite (renown-based), Ruler (rule over colony/worlds/systems)")]
            public TitleType type = TitleType.Hero;

            [Tooltip("Title level/hierarchy - from BandLeader to MultiEmpireRuler. Only the highest level title is presented, but entity is known by all titles.")]
            public TitleLevel level = TitleLevel.None;

            [Tooltip("Title state - Active titles are currently held. Lost/Former titles carry diminished prestige but are still remembered.")]
            public TitleState state = TitleState.Active;

            [Tooltip("Display name (culture-aware variant)")]
            public string displayName = string.Empty;

            [Tooltip("Associated colony ID (if title is colony-specific)")]
            public string colonyId = string.Empty;

            [Tooltip("Associated faction ID (if title is faction-specific)")]
            public string factionId = string.Empty;

            [Tooltip("Associated empire ID (if title is empire-specific)")]
            public string empireId = string.Empty;

            [Tooltip("Acquisition reason (e.g., 'Founded', 'Defended', 'Renown', 'Inherited')")]
            public string acquisitionReason = string.Empty;

            [Tooltip("Loss reason (e.g., 'Broken', 'Fallen', 'Usurped', 'Revoked', 'Disinherited') - only set if state is not Active")]
            public string lossReason = string.Empty;
        }

        [Serializable]
        public class LoyaltyEntry
        {
            public AffiliationType targetType;
            public string targetId = string.Empty;
            [Range(0f, 1f)] public float loyalty = 0.5f;
        }

        [Serializable]
        public class OwnershipStakeEntry
        {
            public string assetType = string.Empty;
            public string assetId = string.Empty;
            [Range(0f, 1f)] public float ownershipPercentage = 0f;
        }

        [Serializable]
        public class PatronageEntry
        {
            public AffiliationType aggregateType;
            public string aggregateId = string.Empty;
            public string role = string.Empty;
        }

        [Serializable]
        public class SuccessorEntry
        {
            public string successorId = string.Empty;
            [Range(0f, 1f)] public float inheritancePercentage = 0.5f;
            public SuccessionAuthoring.SuccessorType type = SuccessionAuthoring.SuccessorType.Heir;
        }

        public List<IndividualSpecData> individuals = new List<IndividualSpecData>();
    }
}

