using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Profile;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Individual Profile Catalog")]
    public sealed class Space4XIndividualProfileCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class OutlookWeightData
        {
            public OutlookId outlookId = OutlookId.Neutral;
            [Range(-1f, 1f)] public float weight = 0.15f;
        }

        [Serializable]
        public class IndividualProfileData
        {
            public string id = "baseline";

            [Header("Alignment")]
            [Range(-1f, 1f)] public float law = 0f;
            [Range(-1f, 1f)] public float good = 0f;
            [Range(-1f, 1f)] public float integrity = 0f;

            [Header("Outlooks")]
            public List<OutlookWeightData> outlooks = new List<OutlookWeightData>();

            [Header("Behavior Disposition")]
            public bool overrideBehavior = true;
            [Range(0f, 1f)] public float compliance = 0.7f;
            [Range(0f, 1f)] public float caution = 0.6f;
            [Range(0f, 1f)] public float formationAdherence = 0.65f;
            [Range(0f, 1f)] public float riskTolerance = 0.45f;
            [Range(0f, 1f)] public float aggression = 0.4f;
            [Range(0f, 1f)] public float patience = 0.6f;

            [Header("Officer Stats (0-100)")]
            [Range(0f, 100f)] public float command = 65f;
            [Range(0f, 100f)] public float tactics = 60f;
            [Range(0f, 100f)] public float logistics = 60f;
            [Range(0f, 100f)] public float diplomacy = 55f;
            [Range(0f, 100f)] public float engineering = 50f;
            [Range(0f, 100f)] public float resolve = 60f;

            [Header("Physique / Finesse / Will (0-100)")]
            [Range(0f, 100f)] public float physique = 50f;
            [Range(0f, 100f)] public float finesse = 50f;
            [Range(0f, 100f)] public float will = 50f;
            [Range(1, 10)] public int physiqueInclination = 5;
            [Range(1, 10)] public int finesseInclination = 5;
            [Range(1, 10)] public int willInclination = 5;
            [Range(0f, 1000f)] public float generalXP = 0f;

            [Header("Derived Capacities")]
            [Range(0f, 2f)] public float sight = 1f;
            [Range(0f, 2f)] public float manipulation = 1f;
            [Range(0f, 2f)] public float consciousness = 1f;
            [Range(0f, 2f)] public float reactionTime = 1f;
            [Range(0f, 2f)] public float boarding = 1f;

            [Header("Personality Axes (-1..1)")]
            [Range(-1f, 1f)] public float boldness = 0f;
            [Range(-1f, 1f)] public float vengefulness = 0f;
            [Range(-1f, 1f)] public float riskToleranceAxis = 0f;
            [Range(-1f, 1f)] public float selflessness = 0f;
            [Range(-1f, 1f)] public float conviction = 0f;

            [Header("Morale")]
            public bool overrideMorale = false;
            [Range(-1f, 1f)] public float moraleBaseline = 0f;
            [Range(0f, 0.1f)] public float moraleDriftRate = 0.01f;

            [Header("Patriotism")]
            [Range(0, 100)] public int naturalLoyalty = 50;
            [Range(0f, 1f)] public float familyBias = 0.3f;
            [Range(0f, 1f)] public float ideologicalZeal = 0.2f;
            [Range(0f, 1f)] public float speciesPride = 0.4f;
            public BelongingTier primaryTier = BelongingTier.Faction;
        }

        [Tooltip("Profile id to use when no per-entity profile is specified.")]
        public string defaultProfileId = "baseline";

        public List<IndividualProfileData> profiles = new List<IndividualProfileData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XIndividualProfileCatalogAuthoring>
        {
            public override void Bake(Space4XIndividualProfileCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.profiles == null || authoring.profiles.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XIndividualProfileCatalogAuthoring has no profiles defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<IndividualProfileCatalogBlob>();
                var profileArray = builder.Allocate(ref catalogBlob.Profiles, authoring.profiles.Count);

                for (int i = 0; i < authoring.profiles.Count; i++)
                {
                    var data = authoring.profiles[i];
                    var outlookList = new FixedList64Bytes<OutlookWeight>();

                    if (data.outlooks != null)
                    {
                        for (int j = 0; j < data.outlooks.Count; j++)
                        {
                            if (outlookList.Length >= outlookList.Capacity)
                            {
                                break;
                            }

                            var entry = data.outlooks[j];
                            outlookList.Add(new OutlookWeight
                            {
                                OutlookId = entry.outlookId,
                                Weight = (half)math.clamp(entry.weight, -1f, 1f)
                            });
                        }
                    }

                    profileArray[i] = new IndividualProfileTemplate
                    {
                        Id = new FixedString64Bytes(data.id ?? string.Empty),
                        Alignment = Space4X.Registry.AlignmentTriplet.FromFloats(
                            math.clamp(data.law, -1f, 1f),
                            math.clamp(data.good, -1f, 1f),
                            math.clamp(data.integrity, -1f, 1f)),
                        Behavior = BehaviorDisposition.FromValues(
                            math.clamp(data.compliance, 0f, 1f),
                            math.clamp(data.caution, 0f, 1f),
                            math.clamp(data.formationAdherence, 0f, 1f),
                            math.clamp(data.riskTolerance, 0f, 1f),
                            math.clamp(data.aggression, 0f, 1f),
                            math.clamp(data.patience, 0f, 1f)),
                        Stats = new IndividualStats
                        {
                            Command = (half)math.clamp(data.command, 0f, 100f),
                            Tactics = (half)math.clamp(data.tactics, 0f, 100f),
                            Logistics = (half)math.clamp(data.logistics, 0f, 100f),
                            Diplomacy = (half)math.clamp(data.diplomacy, 0f, 100f),
                            Engineering = (half)math.clamp(data.engineering, 0f, 100f),
                            Resolve = (half)math.clamp(data.resolve, 0f, 100f)
                        },
                        Physique = new PhysiqueFinesseWill
                        {
                            Physique = (half)math.clamp(data.physique, 0f, 100f),
                            Finesse = (half)math.clamp(data.finesse, 0f, 100f),
                            Will = (half)math.clamp(data.will, 0f, 100f),
                            PhysiqueInclination = (byte)math.clamp(data.physiqueInclination, 1, 10),
                            FinesseInclination = (byte)math.clamp(data.finesseInclination, 1, 10),
                            WillInclination = (byte)math.clamp(data.willInclination, 1, 10),
                            GeneralXP = math.max(0f, data.generalXP)
                        },
                        Capacities = new DerivedCapacities
                        {
                            Sight = math.max(0f, data.sight),
                            Manipulation = math.max(0f, data.manipulation),
                            Consciousness = math.max(0f, data.consciousness),
                            ReactionTime = math.max(0f, data.reactionTime),
                            Boarding = math.max(0f, data.boarding)
                        },
                        Personality = PersonalityAxes.FromValues(
                            math.clamp(data.boldness, -1f, 1f),
                            math.clamp(data.vengefulness, -1f, 1f),
                            math.clamp(data.riskToleranceAxis, -1f, 1f),
                            math.clamp(data.selflessness, -1f, 1f),
                            math.clamp(data.conviction, -1f, 1f)),
                        Patriotism = new PatriotismProfile
                        {
                            NaturalLoyalty = (byte)math.clamp(data.naturalLoyalty, 0, 100),
                            FamilyBias = (half)math.clamp(data.familyBias, 0f, 1f),
                            IdeologicalZeal = (half)math.clamp(data.ideologicalZeal, 0f, 1f),
                            SpeciesPride = (half)math.clamp(data.speciesPride, 0f, 1f),
                            PrimaryTier = data.primaryTier,
                            HasConflict = 0,
                            OverallPatriotism = (half)0f
                        },
                        MoraleBaseline = math.clamp(data.moraleBaseline, -1f, 1f),
                        MoraleDriftRate = math.max(0f, data.moraleDriftRate),
                        BehaviorExplicit = data.overrideBehavior ? (byte)1 : (byte)0,
                        MoraleExplicit = data.overrideMorale ? (byte)1 : (byte)0,
                        Outlooks = outlookList
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<IndividualProfileCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                var defaultId = string.IsNullOrWhiteSpace(authoring.defaultProfileId)
                    ? (authoring.profiles[0].id ?? string.Empty)
                    : authoring.defaultProfileId;
                AddComponent(entity, new IndividualProfileCatalogSingleton
                {
                    Catalog = blobAsset,
                    DefaultProfileId = new FixedString64Bytes(defaultId)
                });
            }
        }
    }
}
