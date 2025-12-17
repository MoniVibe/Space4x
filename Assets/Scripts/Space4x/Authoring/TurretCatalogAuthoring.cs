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
    using Debug = UnityEngine.Debug;

        [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Turret Catalog")]
    public sealed class TurretCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class TurretSpecData
        {
            public string id;
            [Header("Traverse")]
            [Tooltip("Traverse arc limit in degrees")]
            [Range(0f, 360f)] public float arcLimitDeg = 360f;
            [Tooltip("Traverse speed in degrees per second")]
            [Min(0f)] public float traverseSpeedDegPerSec = 90f;
            [Header("Elevation")]
            [Tooltip("Minimum elevation in degrees")]
            [Range(-90f, 90f)] public float elevationMinDeg = -10f;
            [Tooltip("Maximum elevation in degrees")]
            [Range(-90f, 90f)] public float elevationMaxDeg = 45f;
            [Header("Recoil")]
            [Tooltip("Recoil force")]
            [Min(0f)] public float recoilForce = 0f;
            [Header("Socket")]
            [Tooltip("Socket name for muzzle binding (e.g., 'Socket_Muzzle')")]
            public string socketName = "Socket_Muzzle";
        }

        public List<TurretSpecData> turrets = new List<TurretSpecData>();

        public sealed class Baker : Unity.Entities.Baker<TurretCatalogAuthoring>
        {
            public override void Bake(TurretCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.turrets == null || authoring.turrets.Count == 0)
                {
                    UnityDebug.LogWarning("TurretCatalogAuthoring has no turrets defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<TurretCatalogBlob>();
                var turretArray = builder.Allocate(ref catalogBlob.Turrets, authoring.turrets.Count);

                for (int i = 0; i < authoring.turrets.Count; i++)
                {
                    var turretData = authoring.turrets[i];
                    turretArray[i] = new TurretSpec
                    {
                        Id = new FixedString64Bytes(turretData.id ?? string.Empty),
                        ArcLimitDeg = math.clamp(turretData.arcLimitDeg, 0f, 360f),
                        TraverseSpeedDegPerSec = math.max(0f, turretData.traverseSpeedDegPerSec),
                        ElevationMinDeg = math.clamp(turretData.elevationMinDeg, -90f, 90f),
                        ElevationMaxDeg = math.clamp(turretData.elevationMaxDeg, -90f, 90f),
                        RecoilForce = math.max(0f, turretData.recoilForce),
                        SocketName = new FixedString32Bytes(turretData.socketName ?? "Socket_Muzzle")
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<TurretCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new TurretCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

