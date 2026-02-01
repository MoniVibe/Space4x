using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Modules;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Catalogs/Engine Module Catalog")]
    public sealed class Space4XEngineModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class EngineModuleSpecData
        {
            public string moduleId;
            public EngineClass engineClass = EngineClass.Civilian;
            public EngineFuelType fuelType = EngineFuelType.Chemical;
            public EngineIntakeType intakeType = EngineIntakeType.None;
            public EngineVectoringMode vectoringMode = EngineVectoringMode.Fixed;
            [Range(0f, 1f)] public float techLevel;
            [Range(0f, 1f)] public float quality;
            [Min(0f)] public float thrustScalar;
            [Min(0f)] public float turnScalar;
            [Range(0f, 1f)] public float responseRating;
            [Range(0f, 1f)] public float efficiencyRating;
            [Range(0f, 1f)] public float boostRating;
            [Range(0f, 1f)] public float vectoringRating;
        }

        public List<EngineModuleSpecData> modules = new List<EngineModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XEngineModuleCatalogAuthoring>
        {
            public override void Bake(Space4XEngineModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XEngineModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<EngineModuleCatalogBlob>();
                var array = builder.Allocate(ref root.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var data = authoring.modules[i];
                    array[i] = new EngineModuleSpec
                    {
                        ModuleId = new FixedString64Bytes(data.moduleId ?? string.Empty),
                        EngineClass = data.engineClass,
                        FuelType = data.fuelType,
                        IntakeType = data.intakeType,
                        VectoringMode = data.vectoringMode,
                        TechLevel = math.saturate(data.techLevel),
                        Quality = math.saturate(data.quality),
                        ThrustScalar = math.max(0f, data.thrustScalar),
                        TurnScalar = math.max(0f, data.turnScalar),
                        ResponseRating = math.saturate(data.responseRating),
                        EfficiencyRating = math.saturate(data.efficiencyRating),
                        BoostRating = math.saturate(data.boostRating),
                        VectoringRating = math.saturate(data.vectoringRating)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<EngineModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EngineModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}
