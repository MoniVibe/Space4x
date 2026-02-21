using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum LeisureFacilityType : byte
    {
        None = 0,
        CrewQuarters = 1,
        Barracks = 2,
        Museum = 3,
        ViceHouse = 4,
        ContrabandDen = 5,
        CoercionPit = 6,
        Holotheater = 7,
        VRScape = 8,
        BioDeck = 9,
        ThemePark = 10,
        Casino = 11,
        Restaurant = 12,
        Arena = 13,
        OrbitalArena = 14,
        Temple = 15
    }

    public enum ArenaProgramTier : byte
    {
        Exhibition = 0,
        ThirdBlood = 1,
        SanguinisExtremis = 2
    }

    public enum LeisureOpportunityType : byte
    {
        None = 0,
        ArenaBout = 1,
        OrbitalWargame = 2,
        TempleRite = 3
    }

    public enum LeisureIncidentType : byte
    {
        None = 0,
        SpyRecruitment = 1,
        PoisonedSupply = 2,
        SleeperAssassin = 3,
        BriberyDemand = 4
    }

    /// <summary>
    /// Leisure-capable limb/facility profile attached to installed modules.
    /// Values are interpreted as per-tick rates in normalized [0,1] need-space.
    /// </summary>
    public struct LeisureFacilityLimb : IComponentData
    {
        public LeisureFacilityType Type;
        public ArenaProgramTier ArenaTier;
        public float HousingCapacity;
        public float EntertainmentRate;
        public float ComfortRate;
        public float SocialRate;
        public float NourishmentRate;
        public float AmbientLawBias;
        public float AmbientGoodBias;
        public float AmbientIntegrityBias;
        public float AmbientIntensity;
        public float EspionageRisk;
        public float PoisonRisk;
        public float AssassinationRisk;
        public float Illicitness;
        public float BriberyPressure;
        public float ParticipationPurseRate;
        public float LootYieldRate;
        public float SalvageRightsRate;
        public float ReputationYieldRate;

        public static LeisureFacilityLimb CrewQuarters(float housingSlots = 12f)
        {
            return new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.CrewQuarters,
                ArenaTier = ArenaProgramTier.Exhibition,
                HousingCapacity = math.max(0f, housingSlots),
                EntertainmentRate = 0.01f,
                ComfortRate = 0.03f,
                SocialRate = 0.01f,
                NourishmentRate = 0.01f,
                AmbientLawBias = 0.05f,
                AmbientGoodBias = 0f,
                AmbientIntegrityBias = 0.08f,
                AmbientIntensity = 0.03f,
                EspionageRisk = 0.01f,
                PoisonRisk = 0.01f,
                AssassinationRisk = 0.01f,
                Illicitness = 0f,
                BriberyPressure = 0f,
                ParticipationPurseRate = 0f,
                LootYieldRate = 0f,
                SalvageRightsRate = 0f,
                ReputationYieldRate = 0.002f
            };
        }

        public static LeisureFacilityLimb Barracks(float housingSlots = 24f)
        {
            return new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.Barracks,
                ArenaTier = ArenaProgramTier.Exhibition,
                HousingCapacity = math.max(0f, housingSlots),
                EntertainmentRate = 0.005f,
                ComfortRate = 0.01f,
                SocialRate = 0.02f,
                NourishmentRate = 0.01f,
                AmbientLawBias = 0.08f,
                AmbientGoodBias = -0.02f,
                AmbientIntegrityBias = 0.02f,
                AmbientIntensity = 0.035f,
                EspionageRisk = 0.02f,
                PoisonRisk = 0.02f,
                AssassinationRisk = 0.02f,
                Illicitness = 0f,
                BriberyPressure = 0.01f,
                ParticipationPurseRate = 0f,
                LootYieldRate = 0f,
                SalvageRightsRate = 0f,
                ReputationYieldRate = 0.003f
            };
        }

        public static LeisureFacilityLimb Restaurant(float nourishmentRate = 0.08f)
        {
            return new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.Restaurant,
                ArenaTier = ArenaProgramTier.Exhibition,
                HousingCapacity = 0f,
                EntertainmentRate = 0.01f,
                ComfortRate = 0.02f,
                SocialRate = 0.02f,
                NourishmentRate = math.max(0f, nourishmentRate),
                AmbientLawBias = 0.02f,
                AmbientGoodBias = 0.04f,
                AmbientIntegrityBias = 0.02f,
                AmbientIntensity = 0.02f,
                EspionageRisk = 0.03f,
                PoisonRisk = 0.08f,
                AssassinationRisk = 0.03f,
                Illicitness = 0f,
                BriberyPressure = 0.01f,
                ParticipationPurseRate = 0f,
                LootYieldRate = 0f,
                SalvageRightsRate = 0f,
                ReputationYieldRate = 0.004f
            };
        }

        public static LeisureFacilityLimb Arena(ArenaProgramTier tier = ArenaProgramTier.Exhibition)
        {
            var isThird = tier == ArenaProgramTier.ThirdBlood ? 1f : 0f;
            var isExtremis = tier == ArenaProgramTier.SanguinisExtremis ? 1f : 0f;

            return new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.Arena,
                ArenaTier = tier,
                HousingCapacity = 0f,
                EntertainmentRate = 0.14f + isThird * 0.03f + isExtremis * 0.05f,
                ComfortRate = 0.01f,
                SocialRate = 0.06f,
                NourishmentRate = 0f,
                AmbientLawBias = -0.08f - isThird * 0.04f - isExtremis * 0.07f,
                AmbientGoodBias = -0.1f - isExtremis * 0.2f,
                AmbientIntegrityBias = -0.08f - isExtremis * 0.2f,
                AmbientIntensity = 0.08f + isThird * 0.04f + isExtremis * 0.1f,
                EspionageRisk = 0.07f + isThird * 0.04f + isExtremis * 0.06f,
                PoisonRisk = 0.03f + isThird * 0.02f + isExtremis * 0.03f,
                AssassinationRisk = 0.05f + isThird * 0.03f + isExtremis * 0.1f,
                Illicitness = 0.1f + isThird * 0.2f + isExtremis * 0.5f,
                BriberyPressure = 0.03f + isThird * 0.08f + isExtremis * 0.14f,
                ParticipationPurseRate = 0.08f + isThird * 0.05f + isExtremis * 0.08f,
                LootYieldRate = 0.06f + isThird * 0.06f + isExtremis * 0.09f,
                SalvageRightsRate = 0.03f + isThird * 0.05f + isExtremis * 0.09f,
                ReputationYieldRate = 0.08f + isThird * 0.05f + isExtremis * 0.08f
            };
        }

        public static LeisureFacilityLimb OrbitalArena(ArenaProgramTier tier = ArenaProgramTier.ThirdBlood)
        {
            var facility = Arena(tier);
            facility.Type = LeisureFacilityType.OrbitalArena;
            facility.EntertainmentRate += 0.06f;
            facility.SocialRate += 0.02f;
            facility.ParticipationPurseRate += 0.05f;
            facility.LootYieldRate += 0.07f;
            facility.SalvageRightsRate += 0.08f;
            facility.ReputationYieldRate += 0.06f;
            facility.EspionageRisk += 0.03f;
            facility.AssassinationRisk += 0.03f;
            return facility;
        }

        public static LeisureFacilityLimb Temple()
        {
            return new LeisureFacilityLimb
            {
                Type = LeisureFacilityType.Temple,
                ArenaTier = ArenaProgramTier.Exhibition,
                HousingCapacity = 0f,
                EntertainmentRate = 0.05f,
                ComfortRate = 0.06f,
                SocialRate = 0.04f,
                NourishmentRate = 0.01f,
                AmbientLawBias = 0.12f,
                AmbientGoodBias = 0.1f,
                AmbientIntegrityBias = 0.18f,
                AmbientIntensity = 0.09f,
                EspionageRisk = 0.02f,
                PoisonRisk = 0.01f,
                AssassinationRisk = 0.03f,
                Illicitness = 0f,
                BriberyPressure = 0.01f,
                ParticipationPurseRate = 0f,
                LootYieldRate = 0f,
                SalvageRightsRate = 0f,
                ReputationYieldRate = 0.07f
            };
        }
    }

    /// <summary>
    /// Normalized leisure need bars [0..1] for a crew aggregate.
    /// </summary>
    public struct LeisureNeedState : IComponentData
    {
        public half Entertainment;
        public half Comfort;
        public half Social;
        public half Nourishment;
        public half EntertainmentDecay;
        public half ComfortDecay;
        public half SocialDecay;
        public half NourishmentDecay;
        public uint LastUpdateTick;

        public static LeisureNeedState Default => new LeisureNeedState
        {
            Entertainment = (half)0.65f,
            Comfort = (half)0.65f,
            Social = (half)0.65f,
            Nourishment = (half)0.75f,
            EntertainmentDecay = (half)0.012f,
            ComfortDecay = (half)0.01f,
            SocialDecay = (half)0.011f,
            NourishmentDecay = (half)0.015f,
            LastUpdateTick = 0u
        };
    }

    /// <summary>
    /// Preference profile for room/facility categories. Values in [0..1].
    /// </summary>
    public struct LeisurePreferenceProfile : IComponentData
    {
        public half Museum;
        public half Vice;
        public half Contraband;
        public half Coercion;
        public half Holotheater;
        public half VRScape;
        public half BioDeck;
        public half ThemePark;
        public half Casino;
        public half Restaurant;
        public half Arena;
        public half OrbitalArena;
        public half ThirdBlood;
        public half SanguinisExtremis;
        public half Temple;

        public static LeisurePreferenceProfile Neutral => new LeisurePreferenceProfile
        {
            Museum = (half)0.5f,
            Vice = (half)0.3f,
            Contraband = (half)0.2f,
            Coercion = (half)0f,
            Holotheater = (half)0.6f,
            VRScape = (half)0.6f,
            BioDeck = (half)0.6f,
            ThemePark = (half)0.5f,
            Casino = (half)0.45f,
            Restaurant = (half)0.65f,
            Arena = (half)0.55f,
            OrbitalArena = (half)0.5f,
            ThirdBlood = (half)0.35f,
            SanguinisExtremis = (half)0.1f,
            Temple = (half)0.5f
        };

        public static LeisurePreferenceProfile FromAlignment(in AlignmentTriplet alignment)
        {
            var law = AlignmentMath.Lawfulness(alignment);
            var chaos = AlignmentMath.Chaos(alignment);
            var integrity = AlignmentMath.IntegrityNormalized(alignment);
            var corruption = 1f - integrity;
            var good = math.saturate(0.5f * (1f + (float)alignment.Good));
            var evil = 1f - good;

            var profile = Neutral;
            profile.Museum = (half)math.saturate(0.2f + 0.45f * law + 0.25f * integrity + 0.1f * good);
            profile.Vice = (half)math.saturate(0.05f + 0.8f * law * corruption + 0.2f * evil);
            profile.Contraband = (half)math.saturate(0.05f + 0.7f * chaos * (0.5f + 0.5f * corruption));
            profile.Coercion = (half)math.saturate(chaos * corruption * evil);
            profile.Holotheater = (half)math.saturate(0.4f + 0.25f * (1f - corruption));
            profile.VRScape = (half)math.saturate(0.35f + 0.3f * chaos);
            profile.BioDeck = (half)math.saturate(0.35f + 0.25f * good + 0.2f * integrity);
            profile.ThemePark = (half)math.saturate(0.3f + 0.35f * good);
            profile.Casino = (half)math.saturate(0.2f + 0.35f * chaos + 0.25f * corruption);
            profile.Restaurant = (half)math.saturate(0.45f + 0.25f * good);
            profile.Arena = (half)math.saturate(0.45f + 0.15f * chaos + 0.12f * law + 0.1f * evil);
            profile.OrbitalArena = (half)math.saturate(0.35f + 0.2f * chaos + 0.1f * law + 0.1f * evil);
            profile.ThirdBlood = (half)math.saturate(0.1f + 0.75f * chaos + 0.15f * evil);
            profile.SanguinisExtremis = (half)math.saturate(chaos * corruption * (0.35f + 0.65f * evil));
            profile.Temple = (half)math.saturate(0.15f + 0.5f * integrity + 0.2f * law + 0.15f * good);
            return profile;
        }
    }

    /// <summary>
    /// Counter-intel and internal policy against leisure-related subterfuge.
    /// </summary>
    public struct LeisureSecurityPolicy : IComponentData
    {
        public half CounterIntelLevel;
        public half FoodSafetyLevel;
        public half InternalSecurityLevel;
        public half BriberyBudget;

        public static LeisureSecurityPolicy Default => new LeisureSecurityPolicy
        {
            CounterIntelLevel = (half)0.35f,
            FoodSafetyLevel = (half)0.35f,
            InternalSecurityLevel = (half)0.35f,
            BriberyBudget = (half)0.2f
        };
    }

    /// <summary>
    /// Snapshot aggregate so AI/UI/telemetry can inspect facility output and exposure.
    /// </summary>
    public struct LeisureFacilityAggregate : IComponentData
    {
        public float HousingCapacity;
        public float EntertainmentRate;
        public float ComfortRate;
        public float SocialRate;
        public float NourishmentRate;
        public float PreferenceFit;
        public float Overcrowding;
        public float AmbientLawBias;
        public float AmbientGoodBias;
        public float AmbientIntegrityBias;
        public float AmbientIntensity;
        public float ArenaOpportunityRate;
        public float OrbitalOpportunityRate;
        public float TempleOpportunityRate;
        public float PrizePurseRate;
        public float LootYieldRate;
        public float SalvageRightsRate;
        public float ReputationYieldRate;
        public float EspionageRisk;
        public float PoisonRisk;
        public float AssassinationRisk;
        public float BriberyRisk;
    }

    [InternalBufferCapacity(4)]
    public struct LeisureIncidentEvent : IBufferElementData
    {
        public LeisureIncidentType Type;
        public float Severity;
        public Entity SourceModule;
        public uint Tick;
    }

    [InternalBufferCapacity(4)]
    public struct LeisureOpportunityEvent : IBufferElementData
    {
        public LeisureOpportunityType Type;
        public float PrizePurse;
        public float LootYield;
        public float SalvageRights;
        public float ReputationGain;
        public Entity SourceModule;
        public uint Tick;
    }

    public static class LeisureFacilityUtility
    {
        public static float ResolvePreferenceWeight(in LeisurePreferenceProfile profile, LeisureFacilityType facilityType)
        {
            return facilityType switch
            {
                LeisureFacilityType.Museum => profile.Museum,
                LeisureFacilityType.ViceHouse => profile.Vice,
                LeisureFacilityType.ContrabandDen => profile.Contraband,
                LeisureFacilityType.CoercionPit => profile.Coercion,
                LeisureFacilityType.Holotheater => profile.Holotheater,
                LeisureFacilityType.VRScape => profile.VRScape,
                LeisureFacilityType.BioDeck => profile.BioDeck,
                LeisureFacilityType.ThemePark => profile.ThemePark,
                LeisureFacilityType.Casino => profile.Casino,
                LeisureFacilityType.Restaurant => profile.Restaurant,
                LeisureFacilityType.Arena => profile.Arena,
                LeisureFacilityType.OrbitalArena => profile.OrbitalArena,
                LeisureFacilityType.Temple => profile.Temple,
                LeisureFacilityType.CrewQuarters => math.max(profile.BioDeck, profile.Restaurant) * 0.5f,
                LeisureFacilityType.Barracks => math.max(profile.Holotheater, profile.Casino) * 0.35f,
                _ => 0.5f
            };
        }

        public static float ResolveArenaTierAffinity(in LeisurePreferenceProfile profile, ArenaProgramTier tier)
        {
            return tier switch
            {
                ArenaProgramTier.Exhibition => profile.Arena,
                ArenaProgramTier.ThirdBlood => profile.ThirdBlood,
                ArenaProgramTier.SanguinisExtremis => profile.SanguinisExtremis,
                _ => profile.Arena
            };
        }
    }
}
