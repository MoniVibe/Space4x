using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for setting up a mutiny/desertion scenario scene.
    /// Creates entities with alignment/affiliation that will trigger compliance breaches.
    /// </summary>
    [MovedFrom(true, "Space4X.Authoring", null, "Space4XMutinyDemoAuthoring")]
    public class Space4XMutinyScenarioAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class CrewMember
        {
            [Tooltip("Unique identifier for this crew member")]
            public string CrewId = "crew-1";

            [Tooltip("Starting position")]
            public Vector3 Position = Vector3.zero;

            [Tooltip("Alignment values (-100 to 100)")]
            public Vector3 Alignment = new Vector3(0, 0, 0);

            [Tooltip("Race ID")]
            public string RaceId = "human";

            [Tooltip("Culture ID")]
            public string CultureId = "terran";

            [Tooltip("Affiliation ID (entity that defines doctrine)")]
            public string AffiliationId = "faction-1";

            [Tooltip("Contract expiration tick (0 = no contract)")]
            public uint ContractExpirationTick = 0;
        }

        [System.Serializable]
        public class Faction
        {
            [Tooltip("Unique identifier for this faction")]
            public string FactionId = "faction-1";

            [Tooltip("Faction position")]
            public Vector3 Position = Vector3.zero;

            [Tooltip("Doctrine alignment expectations")]
            public Vector3 DoctrineAlignment = new Vector3(50, 0, 50);

            [Tooltip("Chaos threshold for mutiny (0-1)")]
            [Range(0f, 1f)]
            public float ChaosThreshold = 0.7f;

            [Tooltip("Lawfulness floor for compliance (0-1)")]
            [Range(0f, 1f)]
            public float LawfulnessFloor = 0.3f;
        }

        [Header("Crew Members")]
        [Tooltip("Crew members that may mutiny or desert")]
        public CrewMember[] CrewMembers = new CrewMember[]
        {
            new CrewMember
            {
                CrewId = "crew-1",
                Position = new Vector3(0, 0, 0),
                Alignment = new Vector3(-80, 20, -60), // High chaos, low law -> mutiny risk
                RaceId = "human",
                CultureId = "terran",
                AffiliationId = "faction-1"
            },
            new CrewMember
            {
                CrewId = "crew-2",
                Position = new Vector3(10, 0, 0),
                Alignment = new Vector3(70, -30, 40), // High war axis -> desertion risk
                RaceId = "human",
                CultureId = "terran",
                AffiliationId = "faction-1"
            },
            new CrewMember
            {
                CrewId = "crew-3",
                Position = new Vector3(20, 0, 0),
                Alignment = new Vector3(30, -50, 20), // Neutral -> independence risk
                RaceId = "human",
                CultureId = "terran",
                AffiliationId = "faction-1"
            }
        };

        [Header("Factions")]
        [Tooltip("Factions with doctrine expectations")]
        public Faction[] Factions = new Faction[]
        {
            new Faction
            {
                FactionId = "faction-1",
                Position = new Vector3(0, 0, 50),
                DoctrineAlignment = new Vector3(50, 0, 50), // Lawful neutral
                ChaosThreshold = 0.7f,
                LawfulnessFloor = 0.3f
            }
        };

        private void OnValidate()
        {
            if (CrewMembers == null || CrewMembers.Length == 0)
            {
                CrewMembers = new CrewMember[3];
            }

            if (Factions == null || Factions.Length == 0)
            {
                Factions = new Faction[1];
            }
        }
    }
}
