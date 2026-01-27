#if UNITY_EDITOR
using System.Collections.Generic;
using PureDOTS.Config;
using PureDOTS.Runtime.Villagers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    public class VillagerArchetypeCatalogAuthoring : MonoBehaviour
    {
        [Tooltip("Optional ScriptableObject asset to source archetype definitions from.")]
        public VillagerArchetypeCatalog catalogAsset;

        [Tooltip("Inline archetype definitions (used if no asset provided).")]
        public List<VillagerArchetypeCatalog.ArchetypeDefinition> inlineArchetypes = new();

        public IReadOnlyList<VillagerArchetypeCatalog.ArchetypeDefinition> GetDefinitions()
        {
            if (catalogAsset != null && catalogAsset.archetypes != null && catalogAsset.archetypes.Count > 0)
            {
                return catalogAsset.archetypes;
            }

            return inlineArchetypes;
        }
    }

    /// <summary>
    /// Baker for VillagerArchetypeCatalog data.
    /// Converts archetype definitions into runtime blob asset.
    /// </summary>
    public class VillagerArchetypeCatalogBaker : Baker<VillagerArchetypeCatalogAuthoring>
    {
        public override void Bake(VillagerArchetypeCatalogAuthoring authoring)
        {
            var definitions = authoring.GetDefinitions();
            var entity = GetEntity(TransformUsageFlags.None);
            
            if (definitions == null || definitions.Count == 0)
            {
                // Create empty catalog
                var builder = new BlobBuilder(Allocator.Temp);
                ref var catalog = ref builder.ConstructRoot<VillagerArchetypeCatalogBlob>();
                builder.Allocate(ref catalog.Archetypes, 0);
                var blobAsset = builder.CreateBlobAssetReference<VillagerArchetypeCatalogBlob>(Allocator.Persistent);
                builder.Dispose();
                
                AddBlobAsset(ref blobAsset, out _);
                AddComponent(entity, new VillagerArchetypeCatalogComponent { Catalog = blobAsset });
                return;
            }

            // Build blob asset from archetype definitions
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref blobBuilder.ConstructRoot<VillagerArchetypeCatalogBlob>();
            var archetypesArray = blobBuilder.Allocate(ref catalogBlob.Archetypes, definitions.Count);
            
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                archetypesArray[i] = new VillagerArchetypeData
                {
                    ArchetypeName = new FixedString64Bytes(def.archetypeName),
                    BasePhysique = (byte)math.clamp(def.basePhysique, 0, 100),
                    BaseFinesse = (byte)math.clamp(def.baseFinesse, 0, 100),
                    BaseWillpower = (byte)math.clamp(def.baseWillpower, 0, 100),
                    HungerDecayRate = math.clamp(def.hungerDecayRate, 0f, 1f),
                    EnergyDecayRate = math.clamp(def.energyDecayRate, 0f, 1f),
                    MoraleDecayRate = math.clamp(def.moraleDecayRate, 0f, 1f),
                    GatherJobWeight = (byte)math.clamp(def.gatherJobWeight, 0, 100),
                    BuildJobWeight = (byte)math.clamp(def.buildJobWeight, 0, 100),
                    CraftJobWeight = (byte)math.clamp(def.craftJobWeight, 0, 100),
                    CombatJobWeight = (byte)math.clamp(def.combatJobWeight, 0, 100),
                    TradeJobWeight = (byte)math.clamp(def.tradeJobWeight, 0, 100),
                    MoralAxisLean = (sbyte)math.clamp(def.moralAxisLean, -100, 100),
                    OrderAxisLean = (sbyte)math.clamp(def.orderAxisLean, -100, 100),
                    PurityAxisLean = (sbyte)math.clamp(def.purityAxisLean, -100, 100),
                    BaseLoyalty = (byte)math.clamp(def.baseLoyalty, 0, 100)
                };
            }
            
            var catalogBlobAsset = blobBuilder.CreateBlobAssetReference<VillagerArchetypeCatalogBlob>(Allocator.Persistent);
            blobBuilder.Dispose();
            
            AddBlobAsset(ref catalogBlobAsset, out _);
            AddComponent(entity, new VillagerArchetypeCatalogComponent { Catalog = catalogBlobAsset });
        }
    }
}
#endif

