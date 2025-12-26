using System;
using PureDOTS.Runtime.Agency;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that configures authority seat projections onto subordinate craft.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XAuthorityCraftClaimAuthoring : MonoBehaviour
    {
        [Header("Base Claim Weights")]
        [Range(0f, 3f)] public float basePressure = 1.1f;
        [Range(0f, 1f)] public float baseLegitimacy = 0.75f;
        [Range(0f, 1f)] public float baseHostility = 0.05f;
        [Range(0f, 1f)] public float baseConsent = 0.6f;
        [Range(0f, 2f)] public float executePressureBonus = 0.35f;
        [Range(0f, 2f)] public float overridePressureBonus = 0.6f;
        [Range(0f, 1f)] public float executiveLegitimacyBonus = 0.1f;
        [Range(0.1f, 1f)] public float actingLegitimacyMultiplier = 0.75f;
        [Min(1)] public int claimDurationTicks = 2;

        [Header("Target Multipliers")]
        [Range(0f, 2f)] public float strikeCraftPressureMultiplier = 1f;
        [Range(0f, 2f)] public float strikeCraftLegitimacyMultiplier = 1f;
        [Range(0f, 2f)] public float miningVesselPressureMultiplier = 1f;
        [Range(0f, 2f)] public float miningVesselLegitimacyMultiplier = 1f;

        [Header("Seat Claims")]
        public SeatClaim[] seatClaims = new[]
        {
            new SeatClaim
            {
                roleId = "ship.captain",
                domains = AgencyDomain.Movement | AgencyDomain.Combat | AgencyDomain.FlightOps | AgencyDomain.Work | AgencyDomain.Logistics,
                targets = AuthorityCraftTarget.All,
                pressureMultiplier = 1.15f,
                legitimacyMultiplier = 1.1f,
                hostilityMultiplier = 1f,
                consentMultiplier = 1f
            },
            new SeatClaim
            {
                roleId = "ship.shipmaster",
                domains = AgencyDomain.Movement | AgencyDomain.Work | AgencyDomain.Logistics | AgencyDomain.FlightOps,
                targets = AuthorityCraftTarget.All,
                pressureMultiplier = 1.05f,
                legitimacyMultiplier = 1f,
                hostilityMultiplier = 1f,
                consentMultiplier = 1f
            },
            new SeatClaim
            {
                roleId = "ship.flight_commander",
                domains = AgencyDomain.FlightOps | AgencyDomain.Combat,
                targets = AuthorityCraftTarget.StrikeCraft,
                pressureMultiplier = 1.05f,
                legitimacyMultiplier = 1f,
                hostilityMultiplier = 1f,
                consentMultiplier = 1f
            },
            new SeatClaim
            {
                roleId = "ship.flight_director",
                domains = AgencyDomain.FlightOps | AgencyDomain.Communications,
                targets = AuthorityCraftTarget.StrikeCraft,
                pressureMultiplier = 1f,
                legitimacyMultiplier = 1f,
                hostilityMultiplier = 1f,
                consentMultiplier = 1f
            },
            new SeatClaim
            {
                roleId = "ship.hangar_deck_officer",
                domains = AgencyDomain.FlightOps,
                targets = AuthorityCraftTarget.StrikeCraft,
                pressureMultiplier = 0.95f,
                legitimacyMultiplier = 0.95f,
                hostilityMultiplier = 1f,
                consentMultiplier = 1f
            }
        };

        [Serializable]
        public struct SeatClaim
        {
            public string roleId;
            public AgencyDomain domains;
            public AuthorityCraftTarget targets;
            [Range(0f, 3f)] public float pressureMultiplier;
            [Range(0f, 2f)] public float legitimacyMultiplier;
            [Range(0f, 2f)] public float hostilityMultiplier;
            [Range(0f, 2f)] public float consentMultiplier;
        }

        private sealed class Baker : Baker<Space4XAuthorityCraftClaimAuthoring>
        {
            public override void Bake(Space4XAuthorityCraftClaimAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AuthorityCraftClaimConfig
                {
                    BasePressure = math.max(0f, authoring.basePressure),
                    BaseLegitimacy = math.saturate(authoring.baseLegitimacy),
                    BaseHostility = math.saturate(authoring.baseHostility),
                    BaseConsent = math.saturate(authoring.baseConsent),
                    ExecutePressureBonus = math.max(0f, authoring.executePressureBonus),
                    OverridePressureBonus = math.max(0f, authoring.overridePressureBonus),
                    ExecutiveLegitimacyBonus = math.saturate(authoring.executiveLegitimacyBonus),
                    ActingLegitimacyMultiplier = math.max(0.1f, authoring.actingLegitimacyMultiplier),
                    ClaimDurationTicks = (ushort)math.clamp(authoring.claimDurationTicks, 1, ushort.MaxValue),
                    StrikeCraftPressureMultiplier = math.max(0f, authoring.strikeCraftPressureMultiplier),
                    StrikeCraftLegitimacyMultiplier = math.max(0f, authoring.strikeCraftLegitimacyMultiplier),
                    MiningVesselPressureMultiplier = math.max(0f, authoring.miningVesselPressureMultiplier),
                    MiningVesselLegitimacyMultiplier = math.max(0f, authoring.miningVesselLegitimacyMultiplier)
                });

                var buffer = AddBuffer<AuthorityCraftSeatClaim>(entity);
                if (authoring.seatClaims == null || authoring.seatClaims.Length == 0)
                {
                    AuthorityCraftClaimDefaults.PopulateDefaultSeatClaims(ref buffer);
                    return;
                }

                foreach (var entry in authoring.seatClaims)
                {
                    if (string.IsNullOrWhiteSpace(entry.roleId))
                    {
                        continue;
                    }

                    buffer.Add(new AuthorityCraftSeatClaim
                    {
                        RoleId = new FixedString64Bytes(entry.roleId),
                        Domains = entry.domains,
                        Targets = entry.targets,
                        PressureMultiplier = math.max(0f, entry.pressureMultiplier),
                        LegitimacyMultiplier = math.max(0f, entry.legitimacyMultiplier),
                        HostilityMultiplier = math.max(0f, entry.hostilityMultiplier),
                        ConsentMultiplier = math.max(0f, entry.consentMultiplier)
                    });
                }
            }
        }
    }
}
