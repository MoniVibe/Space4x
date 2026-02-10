using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Accumulated practice time for pilot domains.
    /// </summary>
    public struct Space4XPilotPracticeTime : IComponentData
    {
        public float NavigationSeconds;
        public float DogfightSeconds;
        public float GunnerySeconds;
    }

    /// <summary>
    /// Weighting used to compute aptitude from Physique/Finesse/Will.
    /// </summary>
    public struct Space4XProficiencyAptitudeWeights
    {
        public float Physique;
        public float Finesse;
        public float Will;
    }

    /// <summary>
    /// Tunables for pilot proficiency synthesis.
    /// </summary>
    public struct Space4XPilotProficiencyConfig : IComponentData
    {
        /// <summary>Seconds of effective practice to reach mastery (skill01 = 1).</summary>
        public float SecondsToMastery;

        /// <summary>Learning multiplier range derived from Wisdom (0..1).</summary>
        public float WisdomMultiplierMin;
        public float WisdomMultiplierMax;

        /// <summary>Aptitude multiplier range derived from Physique/Finesse/Will (0..1).</summary>
        public float AptitudeMultiplierMin;
        public float AptitudeMultiplierMax;

        public Space4XProficiencyAptitudeWeights NavigationAptitude;
        public Space4XProficiencyAptitudeWeights DogfightAptitude;
        public Space4XProficiencyAptitudeWeights GunneryAptitude;

        public float ControlMin;
        public float ControlMax;
        public float TurnMin;
        public float TurnMax;
        public float EnergyMin;
        public float EnergyMax;
        public float JitterMin;
        public float JitterMax;
        public float ReactionMin;
        public float ReactionMax;

        public static Space4XPilotProficiencyConfig Default => new Space4XPilotProficiencyConfig
        {
            SecondsToMastery = 3600f,
            WisdomMultiplierMin = 0.75f,
            WisdomMultiplierMax = 1.25f,
            AptitudeMultiplierMin = 0.75f,
            AptitudeMultiplierMax = 1.25f,
            NavigationAptitude = new Space4XProficiencyAptitudeWeights
            {
                Physique = 0.2f,
                Finesse = 0.6f,
                Will = 0.2f
            },
            DogfightAptitude = new Space4XProficiencyAptitudeWeights
            {
                Physique = 0.2f,
                Finesse = 0.7f,
                Will = 0.1f
            },
            GunneryAptitude = new Space4XProficiencyAptitudeWeights
            {
                Physique = 0.3f,
                Finesse = 0.6f,
                Will = 0.1f
            },
            ControlMin = 0.45f,
            ControlMax = 1.65f,
            TurnMin = 0.6f,
            TurnMax = 1.45f,
            EnergyMin = 0.6f,
            EnergyMax = 1.65f,
            JitterMin = 0.01f,
            JitterMax = 0.2f,
            ReactionMin = 0.08f,
            ReactionMax = 1.2f
        };
    }
}
