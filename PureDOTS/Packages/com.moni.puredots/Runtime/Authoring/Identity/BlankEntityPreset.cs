using System;
using UnityEngine;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Modularity;

namespace PureDOTS.Authoring.Identity
{
    [CreateAssetMenu(fileName = "BlankEntityPreset", menuName = "PureDOTS/Blank Entity Preset")]
    public sealed class BlankEntityPreset : ScriptableObject
    {
        [Header("Identity")]
        public string entityName;
        public string entityKind;
        public string stableKey;

        [Header("Capabilities")]
        public CapabilityEntry[] capabilityEntries = Array.Empty<CapabilityEntry>();

        [Header("Module Opt-Ins")]
        public bool enableNeeds;
        public bool enableRelations;
        public bool enableProfile;
        public bool enableAgency;
        public bool enableCommunication;
        public bool enableGroupKnowledge;

        [Header("Observability")]
        public bool enableEventLog = true;
        public ushort eventLogCapacity = 16;

        [Header("Intents")]
        public bool enableIntentQueue = true;
        public byte intentCapacity = 8;

        [Header("Spatial")]
        public bool enableSpatialIndexing = true;

        [Serializable]
        public struct CapabilityEntry
        {
            public string Id;
            [Range(0, 255)] public int Tier;
            [Range(0, 255)] public int Confidence;
        }
    }
}



