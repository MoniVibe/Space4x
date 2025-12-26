using System;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Entities;

namespace Space4x.Scenario
{
    /// <summary>
    /// Seeds the authority craft claim toggle from environment or defaults.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(CoreSingletonBootstrapSystem))]
    public partial struct Space4XAuthorityCraftClaimToggleBootstrapSystem : ISystem
    {
        private const string ClaimsEnabledEnv = "SPACE4X_AUTHORITY_CLAIMS_ENABLED";
        private bool _initialized;

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                state.Enabled = false;
                return;
            }

            var enabled = AuthorityCraftClaimToggle.Default.Enabled;
            var envValue = Environment.GetEnvironmentVariable(ClaimsEnabledEnv);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                enabled = ParseEnabled(envValue, enabled);
            }

            if (SystemAPI.TryGetSingletonEntity<AuthorityCraftClaimToggle>(out var existingToggle))
            {
                _initialized = true;
                state.Enabled = false;
                return;
            }

            var toggleEntity = state.EntityManager.CreateEntity(typeof(AuthorityCraftClaimToggle));
            state.EntityManager.SetComponentData(toggleEntity, new AuthorityCraftClaimToggle { Enabled = enabled });

            _initialized = true;
            state.Enabled = false;
        }

        private static byte ParseEnabled(string value, byte fallback)
        {
            var trimmed = value.Trim();
            if (trimmed.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("disabled", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (trimmed.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("enabled", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return fallback;
        }
    }
}
