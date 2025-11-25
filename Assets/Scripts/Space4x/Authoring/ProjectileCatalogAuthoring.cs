using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Projectile Catalog")]
    public sealed class ProjectileCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class EffectOpData
        {
            [Tooltip("Effect operation type")]
            public byte kind;
            [Tooltip("Magnitude of effect")]
            public float magnitude;
            [Tooltip("Duration in seconds")]
            [Min(0f)] public float duration = 0f;
            [Tooltip("Status effect ID if applicable")]
            public uint statusId = 0;
        }

        [Serializable]
        public class ProjectileSpecData
        {
            public string id;
            public ProjectileKind kind;
            [Header("Motion")]
            [Tooltip("Speed in m/s (0 for hitscan beam)")]
            [Min(0f)] public float speed = 100f;
            [Tooltip("Lifetime in seconds")]
            [Min(0f)] public float lifetime = 5f;
            [Tooltip("Gravity in m/s^2 (0 for space)")]
            [Min(0f)] public float gravity = 0f;
            [Header("Homing (for missiles)")]
            [Tooltip("Turn rate in degrees per second")]
            [Min(0f)] public float turnRateDeg = 0f;
            [Tooltip("Homing acquisition radius")]
            [Min(0f)] public float seekRadius = 0f;
            [Header("Penetration & Chaining")]
            [Tooltip("How many targets can pass through")]
            [Min(0f)] public float pierce = 0f;
            [Tooltip("Chaining arc range (0 = none)")]
            [Min(0f)] public float chainRange = 0f;
            [Header("Area of Effect")]
            [Tooltip("Explosion radius")]
            [Min(0f)] public float aoERadius = 0f;
            [Header("Damage")]
            [Min(0f)] public float kineticDamage = 0f;
            [Min(0f)] public float energyDamage = 0f;
            [Min(0f)] public float explosiveDamage = 0f;
            [Header("On Hit Effects")]
            public List<EffectOpData> onHitEffects = new List<EffectOpData>();
        }

        public List<ProjectileSpecData> projectiles = new List<ProjectileSpecData>();

        public sealed class Baker : Unity.Entities.Baker<ProjectileCatalogAuthoring>
        {
            public override void Bake(ProjectileCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.projectiles == null || authoring.projectiles.Count == 0)
                {
                    Debug.LogWarning("ProjectileCatalogAuthoring has no projectiles defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<ProjectileCatalogBlob>();
                var projectileArray = builder.Allocate(ref catalogBlob.Projectiles, authoring.projectiles.Count);

                for (int i = 0; i < authoring.projectiles.Count; i++)
                {
                    var projData = authoring.projectiles[i];
                    
                    // Build OnHit effects array
                    var effectCount = projData.onHitEffects != null ? projData.onHitEffects.Count : 0;
                    var effectsArray = builder.Allocate(ref projectileArray[i].OnHit, effectCount);
                    for (int j = 0; j < effectCount; j++)
                    {
                        var effectData = projData.onHitEffects[j];
                        effectsArray[j] = new EffectOp
                        {
                            Kind = effectData.kind,
                            Magnitude = effectData.magnitude,
                            Duration = math.max(0f, effectData.duration),
                            StatusId = effectData.statusId
                        };
                    }

                    projectileArray[i] = new ProjectileSpec
                    {
                        Id = new FixedString64Bytes(projData.id ?? string.Empty),
                        Kind = projData.kind,
                        Speed = math.max(0f, projData.speed),
                        Lifetime = math.max(0f, projData.lifetime),
                        Gravity = math.max(0f, projData.gravity),
                        TurnRateDeg = math.max(0f, projData.turnRateDeg),
                        SeekRadius = math.max(0f, projData.seekRadius),
                        Pierce = math.max(0f, projData.pierce),
                        ChainRange = math.max(0f, projData.chainRange),
                        AoERadius = math.max(0f, projData.aoERadius),
                        Damage = new DamageModel
                        {
                            Kinetic = math.max(0f, projData.kineticDamage),
                            Energy = math.max(0f, projData.energyDamage),
                            Explosive = math.max(0f, projData.explosiveDamage)
                        }
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ProjectileCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ProjectileCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

