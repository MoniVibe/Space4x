using System;
using SystemEnv = System.Environment;
using Unity.Entities;
using UnityEngine.Scripting.APIUpdating;

namespace Space4X.Runtime
{
    /// <summary>
    /// Tag used to opt into legacy scenario systems.
    /// </summary>
    [MovedFrom(true, "Space4X.Runtime", null, "LegacySpace4XDemoTag")]
    public struct LegacySpace4XScenarioTag : IComponentData
    {
    }

    public static class Space4XLegacyScenarioGate
    {
        public const string ScenarioEnvVar = "SPACE4X_LEGACY_SCENARIO";

        public static bool IsEnabled
        {
            get
            {
                return IsEnabled(ScenarioEnvVar);
            }
        }

        private static bool IsEnabled(string envVar)
        {
            var value = SystemEnv.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
