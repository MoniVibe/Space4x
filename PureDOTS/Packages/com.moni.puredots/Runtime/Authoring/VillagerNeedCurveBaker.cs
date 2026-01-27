#if UNITY_EDITOR
using System.Collections.Generic;
using PureDOTS.Config;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;

namespace PureDOTS.Authoring
{
    public class VillagerNeedCurveCatalogAuthoring : MonoBehaviour
    {
        [Tooltip("Optional ScriptableObject asset to source need curves from.")]
        public VillagerNeedCurveCatalog catalogAsset;

        [Tooltip("Inline need curves (used if no asset provided).")]
        public List<VillagerNeedCurve> inlineCurves = new();

        public IReadOnlyList<VillagerNeedCurve> GetCurves()
        {
            if (catalogAsset != null && catalogAsset.curves != null && catalogAsset.curves.Count > 0)
            {
                return catalogAsset.curves;
            }

            return inlineCurves;
        }
    }

    /// <summary>
    /// Baker for VillagerNeedCurveCatalog data.
    /// Converts AnimationCurves into sampled blob asset.
    /// </summary>
    public class VillagerNeedCurveCatalogBaker : Baker<VillagerNeedCurveCatalogAuthoring>
    {
        public override void Bake(VillagerNeedCurveCatalogAuthoring authoring)
        {
            var curves = authoring.GetCurves();
            var entity = GetEntity(TransformUsageFlags.None);
            
            if (curves == null || curves.Count == 0)
            {
                // Create empty catalog
                var builder = new BlobBuilder(Allocator.Temp);
                ref var catalog = ref builder.ConstructRoot<VillagerNeedCurveCatalogBlob>();
                builder.Allocate(ref catalog.Curves, 0);
                var blobAsset = builder.CreateBlobAssetReference<VillagerNeedCurveCatalogBlob>(Allocator.Persistent);
                builder.Dispose();
                
                AddBlobAsset(ref blobAsset, out _);
                AddComponent(entity, new VillagerNeedCurveCatalogComponent { Catalog = blobAsset });
                return;
            }

            // Build blob asset from need curves
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref blobBuilder.ConstructRoot<VillagerNeedCurveCatalogBlob>();
            var curvesArray = blobBuilder.Allocate(ref catalogBlob.Curves, curves.Count);
            
            for (int i = 0; i < curves.Count; i++)
            {
                var curveAsset = curves[i];
                if (curveAsset == null)
                {
                    continue;
                }
                
                // Sample the AnimationCurve
                var sampledPoints = curveAsset.SampleCurve();
                var pointsArray = blobBuilder.Allocate(ref curvesArray[i].CurvePoints, sampledPoints.Count);
                
                for (int j = 0; j < sampledPoints.Count; j++)
                {
                    pointsArray[j] = sampledPoints[j];
                }
                
                curvesArray[i] = new NeedCurveData
                {
                    CurveName = new FixedString64Bytes(curveAsset.curveName),
                    NeedType = (byte)curveAsset.needType
                };
            }
            
            var catalogBlobAsset = blobBuilder.CreateBlobAssetReference<VillagerNeedCurveCatalogBlob>(Allocator.Persistent);
            blobBuilder.Dispose();
            
            AddBlobAsset(ref catalogBlobAsset, out _);
            AddComponent(entity, new VillagerNeedCurveCatalogComponent { Catalog = catalogBlobAsset });
        }
    }
}
#endif

