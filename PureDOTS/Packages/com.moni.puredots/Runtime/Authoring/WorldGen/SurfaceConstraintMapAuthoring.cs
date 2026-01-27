using PureDOTS.Runtime.WorldGen;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.WorldGen
{
    [DisallowMultipleComponent]
    public sealed class SurfaceConstraintMapAuthoring : MonoBehaviour
    {
        public SurfaceConstraintMapAsset constraintMap;
    }

    public sealed class SurfaceConstraintMapBaker : Baker<SurfaceConstraintMapAuthoring>
    {
        public override void Bake(SurfaceConstraintMapAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (authoring.constraintMap == null)
            {
                Debug.LogWarning("[SurfaceConstraintMapBaker] No constraintMap assigned; skipping.", authoring);
                return;
            }

            if (!authoring.constraintMap.TryBuildBlobAsset(out var blob, out var error))
            {
                Debug.LogError($"[SurfaceConstraintMapBaker] Failed to build constraint map: {error}", authoring);
                return;
            }

            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new SurfaceConstraintMapComponent { Map = blob });
        }
    }
}

