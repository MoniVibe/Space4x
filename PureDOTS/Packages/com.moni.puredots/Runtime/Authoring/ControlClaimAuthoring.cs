using System;
using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Modularity;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Seeds ControlClaim entries on an entity for authoring-time control relationships.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControlClaimAuthoring : MonoBehaviour
    {
        [SerializeField] private bool autoEnableAgency = true;
        [SerializeField] private ClaimEntry[] claims = Array.Empty<ClaimEntry>();

        [Serializable]
        public struct ClaimEntry
        {
            public GameObject Controller;
            public AgencyDomain Domains;
            [Range(0f, 3f)] public float Pressure;
            [Range(0f, 1f)] public float Legitimacy;
            [Range(0f, 1f)] public float Hostility;
            [Range(0f, 1f)] public float Consent;
            public ControlClaimSourceKind SourceKind;
        }

        private sealed class Baker : Baker<ControlClaimAuthoring>
        {
            public override void Bake(ControlClaimAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                if (authoring.autoEnableAgency)
                {
                    AddComponent<AgencyModuleTag>(entity);
                }

                if (authoring.claims is not { Length: > 0 })
                {
                    return;
                }

                var buffer = AddBuffer<ControlClaim>(entity);
                foreach (var entry in authoring.claims)
                {
                    if (entry.Controller == null)
                    {
                        continue;
                    }

                    var controllerEntity = GetEntity(entry.Controller, TransformUsageFlags.Dynamic);
                    buffer.Add(new ControlClaim
                    {
                        Controller = controllerEntity,
                        SourceSeat = Entity.Null,
                        Domains = entry.Domains,
                        Pressure = math.max(0f, entry.Pressure),
                        Legitimacy = math.saturate(entry.Legitimacy),
                        Hostility = math.saturate(entry.Hostility),
                        Consent = math.saturate(entry.Consent),
                        EstablishedTick = 0u,
                        ExpireTick = 0u,
                        SourceKind = entry.SourceKind
                    });
                }
            }
        }
    }
}
