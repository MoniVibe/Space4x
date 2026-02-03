using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Profile;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public struct IndividualProfileId : IComponentData
    {
        public FixedString64Bytes Id;
    }

    public struct StanceWeight
    {
        public StanceId StanceId;
        public half Weight;
    }

    public struct IndividualProfileTemplate
    {
        public FixedString64Bytes Id;
        public AlignmentTriplet Alignment;
        public BehaviorDisposition Behavior;
        public IndividualStats Stats;
        public PhysiqueFinesseWill Physique;
        public DerivedCapacities Capacities;
        public PersonalityAxes Personality;
        public PatriotismProfile Patriotism;
        public float MoraleBaseline;
        public float MoraleDriftRate;
        public byte BehaviorExplicit;
        public byte MoraleExplicit;
        public byte Reserved0;
        public byte Reserved1;
        public FixedList64Bytes<StanceWeight> Stances;
    }

    public struct IndividualProfileCatalogBlob
    {
        public BlobArray<IndividualProfileTemplate> Profiles;
    }

    public struct IndividualProfileCatalogSingleton : IComponentData
    {
        public BlobAssetReference<IndividualProfileCatalogBlob> Catalog;
        public FixedString64Bytes DefaultProfileId;
    }
}

