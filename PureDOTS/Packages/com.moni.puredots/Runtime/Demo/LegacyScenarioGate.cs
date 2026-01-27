#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using System;

namespace PureDOTS.LegacyScenario
{
    internal static class LegacyScenarioGate
    {
        private const string LegacyScenarioEnvVar = "PURE_DOTS_LEGACY_SCENARIO";

        public static bool IsEnabled
        {
            get
            {
                var value = System.Environment.GetEnvironmentVariable(LegacyScenarioEnvVar);
                return IsTruthy(value);
            }
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            return trimmed == "1"
                   || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || trimmed.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
