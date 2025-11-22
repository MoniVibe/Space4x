using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Helper methods shared between authoring validation and runtime guards.
    /// </summary>
    public static class CrewGrowthSettingsUtility
    {
        public struct ValidationResult
        {
            public CrewGrowthSettings Settings;
            public bool HadError;
        }

        public static ValidationResult Sanitize(in CrewGrowthSettings input)
        {
            var settings = input;
            var hadError = false;

            settings.BreedingRatePerTick = math.max(0f, settings.BreedingRatePerTick);
            settings.CloningRatePerTick = math.max(0f, settings.CloningRatePerTick);
            settings.CloningResourceCost = math.max(0f, settings.CloningResourceCost);

            if (settings.BreedingEnabled != 0 && settings.BreedingRatePerTick <= 0f)
            {
                settings.BreedingEnabled = 0;
                hadError = true;
            }

            if (settings.CloningEnabled != 0 && settings.CloningRatePerTick <= 0f)
            {
                settings.CloningEnabled = 0;
                hadError = true;
            }

            return new ValidationResult
            {
                Settings = settings,
                HadError = hadError
            };
        }
    }
}
