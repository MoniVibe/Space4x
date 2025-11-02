using System;
using global::Godgame.Registry;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Godgame.Authoring
{
    /// <summary>
    /// Seeds sample villagers and storehouses so the registry bridge can exercise Godgame data flows in prototype scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GodgameSampleRegistryAuthoring : MonoBehaviour
    {
        [SerializeField]
        private VillagerDefinition[] villagers =
        {
            new VillagerDefinition
            {
                DisplayName = "Acolyte-001",
                VillagerId = 1,
                FactionId = 0,
                IsAvailable = true,
                HealthPercent = 92f,
                MoralePercent = 65f,
                EnergyPercent = 80f,
                JobType = VillagerJob.JobType.Gatherer,
                JobPhase = VillagerJob.JobPhase.Gathering,
                Discipline = VillagerDisciplineType.Forester,
                DisciplineLevel = 1,
                IsCombatReady = false,
                AIState = VillagerAIState.State.Working,
                AIGoal = VillagerAIState.Goal.Work,
                ActiveTicketId = 0,
                CurrentResourceTypeIndex = 0,
                Productivity = 0.75f,
                Position = new float3(0f, 0f, 2f)
            }
        };

        [SerializeField]
        private StorehouseDefinition[] storehouses =
        {
            new StorehouseDefinition
            {
                Label = "Sanctum Storehouse",
                StorehouseId = 1001,
                TotalCapacity = 500f,
                TotalStored = 120f,
                TotalReserved = 25f,
                PrimaryResourceTypeIndex = 0,
                ResourceSummaries = new[]
                {
                    new StorehouseResourceSummaryDefinition
                    {
                        ResourceTypeIndex = 0,
                        Capacity = 500f,
                        Stored = 120f,
                        Reserved = 25f
                    }
                },
                Position = new float3(6f, 0f, -4f)
            }
        };

        public VillagerDefinition[] Villagers => villagers;

        public StorehouseDefinition[] Storehouses => storehouses;

        [Serializable]
        public struct VillagerDefinition
        {
            public string DisplayName;
            public int VillagerId;
            public int FactionId;
            public bool IsAvailable;
            public bool IsReserved;
            [Range(0f, 100f)] public float HealthPercent;
            [Range(0f, 100f)] public float MoralePercent;
            [Range(0f, 100f)] public float EnergyPercent;
            public VillagerJob.JobType JobType;
            public VillagerJob.JobPhase JobPhase;
            public VillagerDisciplineType Discipline;
            [Range(0, 10)] public byte DisciplineLevel;
            public bool IsCombatReady;
            public VillagerAIState.State AIState;
            public VillagerAIState.Goal AIGoal;
            public uint ActiveTicketId;
            public ushort CurrentResourceTypeIndex;
            [Range(0f, 2f)] public float Productivity;
            public float3 Position;
        }

        [Serializable]
        public struct StorehouseDefinition
        {
            public string Label;
            public int StorehouseId;
            public float TotalCapacity;
            public float TotalStored;
            public float TotalReserved;
            public ushort PrimaryResourceTypeIndex;
            public uint LastMutationTick;
            public StorehouseResourceSummaryDefinition[] ResourceSummaries;
            public float3 Position;
        }

        [Serializable]
        public struct StorehouseResourceSummaryDefinition
        {
            public ushort ResourceTypeIndex;
            public float Capacity;
            public float Stored;
            public float Reserved;
        }

        private sealed class Baker : Unity.Entities.Baker<GodgameSampleRegistryAuthoring>
        {
            public override void Bake(GodgameSampleRegistryAuthoring authoring)
            {
                BakeVillagers(authoring);
                BakeStorehouses(authoring);
            }

            private void BakeVillagers(GodgameSampleRegistryAuthoring authoring)
            {
                var definitions = authoring.Villagers;
                if (definitions == null || definitions.Length == 0)
                {
                    return;
                }

                for (var i = 0; i < definitions.Length; i++)
                {
                    var definition = definitions[i];
                    var name = string.IsNullOrWhiteSpace(definition.DisplayName)
                        ? $"Villager-{definition.VillagerId}"
                        : definition.DisplayName;

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(definition.Position, quaternion.identity, 1f));
                    AddComponent<SpatialIndexedTag>(entity);
                    AddComponent(entity, new GodgameVillager
                    {
                        DisplayName = new FixedString64Bytes(name),
                        VillagerId = definition.VillagerId,
                        FactionId = definition.FactionId,
                        IsAvailable = definition.IsAvailable ? (byte)1 : (byte)0,
                        IsReserved = definition.IsReserved ? (byte)1 : (byte)0,
                        HealthPercent = math.clamp(definition.HealthPercent, 0f, 100f),
                        MoralePercent = math.clamp(definition.MoralePercent, 0f, 100f),
                        EnergyPercent = math.clamp(definition.EnergyPercent, 0f, 100f),
                        JobType = definition.JobType,
                        JobPhase = definition.JobPhase,
                        Discipline = definition.Discipline,
                        DisciplineLevel = definition.DisciplineLevel,
                        IsCombatReady = definition.IsCombatReady ? (byte)1 : (byte)0,
                        AIState = definition.AIState,
                        AIGoal = definition.AIGoal,
                        CurrentTarget = Entity.Null,
                        ActiveTicketId = definition.ActiveTicketId,
                        CurrentResourceTypeIndex = definition.CurrentResourceTypeIndex,
                        Productivity = math.max(0f, definition.Productivity)
                    });
                }
            }

            private void BakeStorehouses(GodgameSampleRegistryAuthoring authoring)
            {
                var definitions = authoring.Storehouses;
                if (definitions == null || definitions.Length == 0)
                {
                    return;
                }

                for (var i = 0; i < definitions.Length; i++)
                {
                    var definition = definitions[i];
                    var label = string.IsNullOrWhiteSpace(definition.Label)
                        ? $"Storehouse-{definition.StorehouseId}"
                        : definition.Label;

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(definition.Position, quaternion.identity, 1f));
                    AddComponent<SpatialIndexedTag>(entity);

                    var resourceSummaries = default(FixedList32Bytes<GodgameStorehouseResourceSummary>);
                    var resourceDefinitions = definition.ResourceSummaries;
                    if (resourceDefinitions != null)
                    {
                        for (var j = 0; j < resourceDefinitions.Length; j++)
                        {
                            var resourceDefinition = resourceDefinitions[j];
                            resourceSummaries.Add(new GodgameStorehouseResourceSummary
                            {
                                ResourceTypeIndex = resourceDefinition.ResourceTypeIndex,
                                Capacity = math.max(0f, resourceDefinition.Capacity),
                                Stored = math.max(0f, resourceDefinition.Stored),
                                Reserved = math.max(0f, resourceDefinition.Reserved)
                            });
                        }
                    }

                    AddComponent(entity, new GodgameStorehouse
                    {
                        Label = new FixedString64Bytes(label),
                        StorehouseId = definition.StorehouseId,
                        TotalCapacity = math.max(0f, definition.TotalCapacity),
                        TotalStored = math.max(0f, definition.TotalStored),
                        TotalReserved = math.max(0f, definition.TotalReserved),
                        PrimaryResourceTypeIndex = definition.PrimaryResourceTypeIndex,
                        LastMutationTick = definition.LastMutationTick,
                        ResourceSummaries = resourceSummaries
                    });
                }
            }
        }
    }
}
