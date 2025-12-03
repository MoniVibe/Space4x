using PureDOTS.Runtime.Combat;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring.Combat
{
    /// <summary>
    /// Authoring component for Space4x weapons (beams, missiles, torpedoes, flak).
    /// Bakes Weapon buffer entries for entities.
    /// </summary>
    public class Space4XWeaponAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class WeaponData
        {
            [Tooltip("Weapon name")]
            public string Name = "Weapon";

            [Tooltip("Attack range")]
            public float Range = 1000f;

            [Tooltip("Shots per second")]
            public float FireRate = 2f;

            [Tooltip("Base damage")]
            public float BaseDamage = 50f;

            [Tooltip("Damage type")]
            public DamageType DamageType = DamageType.Physical;

            [Tooltip("Fire arc (degrees, 0 = no constraint)")]
            public float FireArcDegrees = 0f;

            [Tooltip("Projectile type (beam, missile, torpedo, flak)")]
            public string ProjectileType = "beam";
        }

        [Tooltip("Weapons on this entity")]
        public WeaponData[] Weapons = new WeaponData[1] { new WeaponData() };
    }

    /// <summary>
    /// Baker for Space4XWeaponAuthoring.
    /// </summary>
    public class Space4XWeaponBaker : Baker<Space4XWeaponAuthoring>
    {
        public override void Bake(Space4XWeaponAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            if (authoring.Weapons == null || authoring.Weapons.Length == 0)
            {
                return;
            }

            // Add Weapon buffer
            var weaponBuffer = AddBuffer<WeaponComponent>(entity);

            for (byte i = 0; i < authoring.Weapons.Length && i < 8; i++) // Max 8 weapons for ships
            {
                var weaponData = authoring.Weapons[i];
                weaponBuffer.Add(new WeaponComponent
                {
                    Range = weaponData.Range,
                    FireRate = weaponData.FireRate,
                    BaseDamage = weaponData.BaseDamage,
                    DamageType = weaponData.DamageType,
                    ProjectileType = new Unity.Collections.FixedString32Bytes(weaponData.ProjectileType),
                    FireArcDegrees = weaponData.FireArcDegrees,
                    LastFireTime = 0f,
                    CooldownRemaining = 0f,
                    WeaponIndex = i
                });
            }

            // Add FireEvent buffer
            AddBuffer<FireEvent>(entity);
        }
    }
}

