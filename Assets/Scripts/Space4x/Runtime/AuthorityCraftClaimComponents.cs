using System;
using PureDOTS.Runtime.Agency;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Runtime
{
    [Flags]
    public enum AuthorityCraftTarget : byte
    {
        None = 0,
        StrikeCraft = 1 << 0,
        MiningVessel = 1 << 1,
        All = StrikeCraft | MiningVessel
    }

    /// <summary>
    /// Base tuning for authority seat claims projected onto subordinate craft.
    /// </summary>
    public struct AuthorityCraftClaimConfig : IComponentData
    {
        public float BasePressure;
        public float BaseLegitimacy;
        public float BaseHostility;
        public float BaseConsent;
        public float ExecutePressureBonus;
        public float OverridePressureBonus;
        public float ExecutiveLegitimacyBonus;
        public float ActingLegitimacyMultiplier;
        public ushort ClaimDurationTicks;
        public float StrikeCraftPressureMultiplier;
        public float StrikeCraftLegitimacyMultiplier;
        public float MiningVesselPressureMultiplier;
        public float MiningVesselLegitimacyMultiplier;

        public static AuthorityCraftClaimConfig Default => new AuthorityCraftClaimConfig
        {
            BasePressure = 1.1f,
            BaseLegitimacy = 0.75f,
            BaseHostility = 0.05f,
            BaseConsent = 0.6f,
            ExecutePressureBonus = 0.35f,
            OverridePressureBonus = 0.6f,
            ExecutiveLegitimacyBonus = 0.1f,
            ActingLegitimacyMultiplier = 0.75f,
            ClaimDurationTicks = 2,
            StrikeCraftPressureMultiplier = 1f,
            StrikeCraftLegitimacyMultiplier = 1f,
            MiningVesselPressureMultiplier = 1f,
            MiningVesselLegitimacyMultiplier = 1f
        };
    }

    /// <summary>
    /// Optional singleton toggle for enabling authority craft claims per scenario.
    /// </summary>
    public struct AuthorityCraftClaimToggle : IComponentData
    {
        public byte Enabled;

        public static AuthorityCraftClaimToggle Default => new AuthorityCraftClaimToggle
        {
            Enabled = 1
        };
    }

    /// <summary>
    /// Role-to-domain claim mapping for authority projections.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AuthorityCraftSeatClaim : IBufferElementData
    {
        public FixedString64Bytes RoleId;
        public AgencyDomain Domains;
        public AuthorityCraftTarget Targets;
        public float PressureMultiplier;
        public float LegitimacyMultiplier;
        public float HostilityMultiplier;
        public float ConsentMultiplier;
    }

    public static class AuthorityCraftClaimDefaults
    {
        public static void PopulateDefaultSeatClaims(ref DynamicBuffer<AuthorityCraftSeatClaim> buffer)
        {
            buffer.Clear();
            buffer.Add(new AuthorityCraftSeatClaim
            {
                RoleId = new FixedString64Bytes("ship.captain"),
                Domains = AgencyDomain.Movement | AgencyDomain.Combat | AgencyDomain.FlightOps | AgencyDomain.Work | AgencyDomain.Logistics,
                Targets = AuthorityCraftTarget.All,
                PressureMultiplier = 1.15f,
                LegitimacyMultiplier = 1.1f,
                HostilityMultiplier = 1f,
                ConsentMultiplier = 1f
            });

            buffer.Add(new AuthorityCraftSeatClaim
            {
                RoleId = new FixedString64Bytes("ship.shipmaster"),
                Domains = AgencyDomain.Movement | AgencyDomain.Work | AgencyDomain.Logistics | AgencyDomain.FlightOps,
                Targets = AuthorityCraftTarget.All,
                PressureMultiplier = 1.05f,
                LegitimacyMultiplier = 1f,
                HostilityMultiplier = 1f,
                ConsentMultiplier = 1f
            });

            buffer.Add(new AuthorityCraftSeatClaim
            {
                RoleId = new FixedString64Bytes("ship.logistics_officer"),
                Domains = AgencyDomain.Logistics | AgencyDomain.Work | AgencyDomain.FlightOps,
                Targets = AuthorityCraftTarget.MiningVessel,
                PressureMultiplier = 1.05f,
                LegitimacyMultiplier = 1f,
                HostilityMultiplier = 1f,
                ConsentMultiplier = 1f
            });

            buffer.Add(new AuthorityCraftSeatClaim
            {
                RoleId = new FixedString64Bytes("ship.flight_commander"),
                Domains = AgencyDomain.FlightOps | AgencyDomain.Combat,
                Targets = AuthorityCraftTarget.StrikeCraft,
                PressureMultiplier = 1.05f,
                LegitimacyMultiplier = 1f,
                HostilityMultiplier = 1f,
                ConsentMultiplier = 1f
            });

            buffer.Add(new AuthorityCraftSeatClaim
            {
                RoleId = new FixedString64Bytes("ship.flight_director"),
                Domains = AgencyDomain.FlightOps | AgencyDomain.Communications,
                Targets = AuthorityCraftTarget.StrikeCraft,
                PressureMultiplier = 1f,
                LegitimacyMultiplier = 1f,
                HostilityMultiplier = 1f,
                ConsentMultiplier = 1f
            });

            buffer.Add(new AuthorityCraftSeatClaim
            {
                RoleId = new FixedString64Bytes("ship.hangar_deck_officer"),
                Domains = AgencyDomain.FlightOps,
                Targets = AuthorityCraftTarget.StrikeCraft,
                PressureMultiplier = 0.95f,
                LegitimacyMultiplier = 0.95f,
                HostilityMultiplier = 1f,
                ConsentMultiplier = 1f
            });
        }
    }
}
