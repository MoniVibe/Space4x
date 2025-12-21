using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Baker for Space4XMutinyScenarioAuthoring - converts authoring data to ECS components.
    /// </summary>
    public class Space4XMutinyScenarioAuthoringBaker : Baker<Space4XMutinyScenarioAuthoring>
    {
        public override void Bake(Space4XMutinyScenarioAuthoring authoring)
        {
            if (authoring.Factions == null || authoring.Factions.Length == 0)
            {
                UnityDebug.LogWarning("[Space4XMutinyScenarioAuthoring] No factions defined!");
                return;
            }

            // Create faction entities with doctrine profiles
            var factionEntities = new NativeHashMap<FixedString64Bytes, Entity>(authoring.Factions.Length, Allocator.Temp);
            foreach (var faction in authoring.Factions)
            {
                var factionId = new FixedString64Bytes(faction.FactionId);
                var factionEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                
                AddComponent(factionEntity, LocalTransform.FromPositionRotationScale(
                    faction.Position,
                    quaternion.identity,
                    1f));

                var normalizedDoctrine = math.clamp(faction.DoctrineAlignment / 100f, new float3(-1f), new float3(1f));
                var alignmentWindow = new AlignmentWindow
                {
                    LawMin = (half)math.max(-1f, normalizedDoctrine.x - 0.1f),
                    LawMax = (half)math.min(1f, normalizedDoctrine.x + 0.1f),
                    GoodMin = (half)math.max(-1f, normalizedDoctrine.y - 0.1f),
                    GoodMax = (half)math.min(1f, normalizedDoctrine.y + 0.1f),
                    IntegrityMin = (half)math.max(-1f, normalizedDoctrine.z - 0.1f),
                    IntegrityMax = (half)math.min(1f, normalizedDoctrine.z + 0.1f)
                };

                AddComponent(factionEntity, new DoctrineProfile
                {
                    AlignmentWindow = alignmentWindow,
                    AxisTolerance = (half)0.1f,
                    OutlookTolerance = (half)0.1f,
                    ChaosMutinyThreshold = (half)math.clamp(faction.ChaosThreshold, 0f, 1f),
                    LawfulContractFloor = (half)math.clamp(faction.LawfulnessFloor, 0f, 1f),
                    SuspicionGain = (half)0.05f
                });

                factionEntities[factionId] = factionEntity;
            }

            // Create crew member entities
            if (authoring.CrewMembers != null)
            {
                foreach (var crew in authoring.CrewMembers)
                {
                    var entity = GetEntity(TransformUsageFlags.Dynamic);
                    
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(
                        crew.Position,
                        quaternion.identity,
                        1f));

                    // Add alignment (convert -100..100 authoring range into -1..1)
                    var normalizedAlignment = math.clamp(crew.Alignment / 100f, new float3(-1f), new float3(1f));
                    AddComponent(entity, new AlignmentTriplet
                    {
                        Law = (half)normalizedAlignment.x,
                        Good = (half)normalizedAlignment.y,
                        Integrity = (half)normalizedAlignment.z
                    });

                    // Add race and culture
                    AddComponent(entity, new RaceId { Value = ToUShortId(crew.RaceId) });
                    AddComponent(entity, new CultureId { Value = ToUShortId(crew.CultureId) });

                    // Add affiliation tag
                    var affiliationId = new FixedString64Bytes(crew.AffiliationId);
                    if (factionEntities.TryGetValue(affiliationId, out var factionEntity))
                    {
                        var affiliationBuffer = AddBuffer<AffiliationTag>(entity);
                        affiliationBuffer.Add(new AffiliationTag
                        {
                            Type = AffiliationType.Faction,
                            Target = factionEntity,
                            Loyalty = (half)0.5f // Start with neutral loyalty
                        });
                    }

                    // Add contract if specified
                    if (crew.ContractExpirationTick > 0)
                    {
                        AddComponent(entity, new ContractBinding
                        {
                            ExpirationTick = crew.ContractExpirationTick
                        });
                    }

                    // Add compliance breach and ticket buffers (will be populated by compliance system)
                    AddBuffer<ComplianceBreach>(entity);
                    AddBuffer<ComplianceTicket>(entity);
                }
            }

            factionEntities.Dispose();
        }

        private static ushort ToUShortId(string value)
        {
            if (ushort.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return (ushort)((value ?? string.Empty).GetHashCode() & 0xFFFF);
        }
    }
}
