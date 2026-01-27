using Unity.Entities;

namespace PureDOTS.Runtime.Transport
{
    /// <summary>
    /// Tunables for opportunistic village hauling behaviour.
    /// </summary>
    public struct HaulingPolicyConfig : IComponentData
    {
        public float MaxUnitsPerHaul;
        public float MinUnitsPerHaul;
        public float EnergyCostPerUnit;
        public float MinEnergyToHaul;
        public uint CooldownTicks;
        public byte MaxSiteChecks;
        public float MinimumWillingness;

        public static HaulingPolicyConfig Default => new HaulingPolicyConfig
        {
            MaxUnitsPerHaul = 15f,
            MinUnitsPerHaul = 2f,
            EnergyCostPerUnit = 0.4f,
            MinEnergyToHaul = 25f,
            CooldownTicks = 90u,
            MaxSiteChecks = 8,
            MinimumWillingness = 0.25f
        };
    }
}





