using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Payload data for fireball miracle (explosive damage + fire).
    /// </summary>
    public struct FireballPayload : IComponentData
    {
        public float ExplosionRadius;
        public float BurnDuration;
        public float IgnitionChance; // 0-1 probability
        public float BaseDamage;
        public float FireDamagePerSecond;
    }

    /// <summary>
    /// Payload data for heal miracle (area healing).
    /// </summary>
    public struct HealPayload : IComponentData
    {
        public float HealPerSecond;
        public float MaxHealPerTarget;
        public byte TargetTypes; // Bit flags: Villagers=1, Creatures=2, Buildings=4
        public float HealRadius;
    }

    /// <summary>
    /// Payload data for shield miracle (damage absorption buff).
    /// </summary>
    public struct ShieldPayload : IComponentData
    {
        public float AbsorptionAmount;
        public float ShieldDuration;
        public Entity ShieldedEntity; // Optional: specific entity, or Null for area shield
        public float ShieldRadius;
    }

    /// <summary>
    /// Payload data for lightning miracle (chain damage).
    /// </summary>
    public struct LightningPayload : IComponentData
    {
        public int ChainCount;
        public float ChainRadius;
        public float DamagePerBolt;
        public float StunDuration;
    }

    /// <summary>
    /// Payload data for earthquake miracle (structural damage, panic).
    /// </summary>
    public struct EarthquakePayload : IComponentData
    {
        public float DamageRadius;
        public float StructuralDamage;
        public float PanicDuration;
        public float TerrainDeformationRadius; // Affects terrain height
    }

    /// <summary>
    /// Payload data for forest miracle (instant tree spawning).
    /// </summary>
    public struct ForestPayload : IComponentData
    {
        public int TreeCount;
        public float SpawnRadius;
        public ushort TreeSpeciesIndex; // Index into vegetation catalog
        public float GrowthMultiplier; // Multiplier for initial growth stage
    }

    /// <summary>
    /// Payload data for freeze miracle (movement slow, ice).
    /// </summary>
    public struct FreezePayload : IComponentData
    {
        public float FreezeRadius;
        public float SlowMultiplier; // 0-1, where 0 = frozen, 1 = normal speed
        public float FreezeDuration;
        public float IceDamagePerSecond;
    }

    /// <summary>
    /// Payload data for food miracle (resource spawning).
    /// </summary>
    public struct FoodPayload : IComponentData
    {
        public ushort ResourceTypeIndex; // Index into resource catalog
        public float AmountPerSpawn;
        public int SpawnCount;
        public float SpawnRadius;
    }

    /// <summary>
    /// Payload data for meteor miracle (crater creation, terrain deformation).
    /// </summary>
    public struct MeteorPayload : IComponentData
    {
        public float ImpactRadius;
        public float CraterDepth;
        public float RimHeight;
        public float DamageAtCenter;
        public float DamageFalloff; // Damage multiplier at edge vs center
    }
}

