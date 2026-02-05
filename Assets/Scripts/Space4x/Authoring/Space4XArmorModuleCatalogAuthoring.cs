using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Catalogs/Armor Module Catalog")]
    public sealed class Space4XArmorModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class ArmorModuleSpecData
        {
            public string moduleId;
            [Min(0f)] public float hullBonus;
            [Range(0f, 1f)] public float damageReduction;
            [Range(0f, 1f)] public float kineticResist;
            [Range(0f, 1f)] public float energyResist;
            [Range(0f, 1f)] public float thermalResist = 1f;
            [Range(0f, 1f)] public float emResist = 1f;
            [Range(0f, 1f)] public float radiationResist = 1f;
            [Range(0f, 1f)] public float explosiveResist;
            [Range(0f, 1f)] public float causticResist = 1f;
            [Min(0f)] public float repairRateMultiplier = 1f;
            public Space4XDamageType hardenedType = Space4XDamageType.Unknown;
            [Range(0f, 1f)] public float hardenedBonus = 0f;
            [Range(0f, 1f)] public float hardenedPenalty = 0f;
        }

        public List<ArmorModuleSpecData> modules = new List<ArmorModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XArmorModuleCatalogAuthoring>
        {
            public override void Bake(Space4XArmorModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XArmorModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ArmorModuleCatalogBlob>();
                var array = builder.Allocate(ref root.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var data = authoring.modules[i];
                    array[i] = new ArmorModuleSpec
                    {
                        ModuleId = new FixedString64Bytes(data.moduleId ?? string.Empty),
                        HullBonus = math.max(0f, data.hullBonus),
                        DamageReduction = math.saturate(data.damageReduction),
                        KineticResist = math.saturate(data.kineticResist),
                        EnergyResist = math.saturate(data.energyResist),
                        ThermalResist = math.saturate(data.thermalResist),
                        EMResist = math.saturate(data.emResist),
                        RadiationResist = math.saturate(data.radiationResist),
                        ExplosiveResist = math.saturate(data.explosiveResist),
                        CausticResist = math.saturate(data.causticResist),
                        RepairRateMultiplier = math.max(0f, data.repairRateMultiplier),
                        HardenedType = data.hardenedType,
                        HardenedBonus = math.saturate(data.hardenedBonus),
                        HardenedPenalty = math.saturate(data.hardenedPenalty)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ArmorModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ArmorModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}
