using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum Space4XWeaponNuanceArchetype : byte
    {
        Default = 0,
        Energy = 1,
        Kinetic = 2,
        GuidedMissile = 3,
        GuidedHeavy = 4,
        DissipativePlasma = 5,
        DissipativeFlame = 6
    }

    public readonly struct Space4XWeaponFamilyNuanceProfile
    {
        public readonly float HeatMultiplier;
        public readonly float AmmoMultiplier;
        public readonly float HitChanceMultiplier;
        public readonly float DamageNearMultiplier;
        public readonly float DamageFarMultiplier;
        public readonly float DamageFalloffStart01;
        public readonly float DamageFalloffEnd01;
        public readonly float HeatSeekSignatureGain;
        public readonly float HazardOcclusionResistance;

        public Space4XWeaponFamilyNuanceProfile(
            float heatMultiplier,
            float ammoMultiplier,
            float hitChanceMultiplier,
            float damageNearMultiplier,
            float damageFarMultiplier,
            float damageFalloffStart01,
            float damageFalloffEnd01,
            float heatSeekSignatureGain,
            float hazardOcclusionResistance)
        {
            HeatMultiplier = heatMultiplier;
            AmmoMultiplier = ammoMultiplier;
            HitChanceMultiplier = hitChanceMultiplier;
            DamageNearMultiplier = damageNearMultiplier;
            DamageFarMultiplier = damageFarMultiplier;
            DamageFalloffStart01 = damageFalloffStart01;
            DamageFalloffEnd01 = damageFalloffEnd01;
            HeatSeekSignatureGain = heatSeekSignatureGain;
            HazardOcclusionResistance = hazardOcclusionResistance;
        }
    }

    public static class Space4XWeaponFamilyNuance
    {
        public static Space4XWeaponNuanceArchetype ResolveArchetype(in Space4XWeapon weapon)
        {
            return weapon.Type switch
            {
                WeaponType.Plasma => Space4XWeaponNuanceArchetype.DissipativePlasma,
                WeaponType.Missile => Space4XWeaponNuanceArchetype.GuidedMissile,
                WeaponType.Torpedo => Space4XWeaponNuanceArchetype.GuidedHeavy,
                WeaponType.Laser or WeaponType.Ion => Space4XWeaponNuanceArchetype.Energy,
                WeaponType.Kinetic or WeaponType.PointDefense or WeaponType.Flak => Space4XWeaponNuanceArchetype.Kinetic,
                _ => Space4XWeaponNuanceArchetype.Default
            };
        }

        public static Space4XWeaponFamilyNuanceProfile ResolveProfile(Space4XWeaponNuanceArchetype archetype)
        {
            return archetype switch
            {
                Space4XWeaponNuanceArchetype.Energy => new Space4XWeaponFamilyNuanceProfile(
                    heatMultiplier: 1.2f,
                    ammoMultiplier: 0f,
                    hitChanceMultiplier: 1f,
                    damageNearMultiplier: 1.02f,
                    damageFarMultiplier: 0.95f,
                    damageFalloffStart01: 0.4f,
                    damageFalloffEnd01: 1f,
                    heatSeekSignatureGain: 0f,
                    hazardOcclusionResistance: 0.92f),
                Space4XWeaponNuanceArchetype.Kinetic => new Space4XWeaponFamilyNuanceProfile(
                    heatMultiplier: 0.8f,
                    ammoMultiplier: 1f,
                    hitChanceMultiplier: 1f,
                    damageNearMultiplier: 1f,
                    damageFarMultiplier: 0.9f,
                    damageFalloffStart01: 0.5f,
                    damageFalloffEnd01: 1f,
                    heatSeekSignatureGain: 0f,
                    hazardOcclusionResistance: 0.18f),
                Space4XWeaponNuanceArchetype.GuidedMissile => new Space4XWeaponFamilyNuanceProfile(
                    heatMultiplier: 0.95f,
                    ammoMultiplier: 1.8f,
                    hitChanceMultiplier: 0.82f,
                    damageNearMultiplier: 1f,
                    damageFarMultiplier: 1f,
                    damageFalloffStart01: 1f,
                    damageFalloffEnd01: 1f,
                    heatSeekSignatureGain: 0.28f,
                    hazardOcclusionResistance: 0.46f),
                Space4XWeaponNuanceArchetype.GuidedHeavy => new Space4XWeaponFamilyNuanceProfile(
                    heatMultiplier: 1.05f,
                    ammoMultiplier: 2.3f,
                    hitChanceMultiplier: 0.74f,
                    damageNearMultiplier: 1.08f,
                    damageFarMultiplier: 0.96f,
                    damageFalloffStart01: 0.7f,
                    damageFalloffEnd01: 1f,
                    heatSeekSignatureGain: 0.4f,
                    hazardOcclusionResistance: 0.62f),
                Space4XWeaponNuanceArchetype.DissipativePlasma => new Space4XWeaponFamilyNuanceProfile(
                    heatMultiplier: 1.35f,
                    ammoMultiplier: 0f,
                    hitChanceMultiplier: 1f,
                    damageNearMultiplier: 1.08f,
                    damageFarMultiplier: 0.42f,
                    damageFalloffStart01: 0.35f,
                    damageFalloffEnd01: 1f,
                    heatSeekSignatureGain: 0f,
                    hazardOcclusionResistance: 0.85f),
                Space4XWeaponNuanceArchetype.DissipativeFlame => new Space4XWeaponFamilyNuanceProfile(
                    heatMultiplier: 1.25f,
                    ammoMultiplier: 0f,
                    hitChanceMultiplier: 1f,
                    damageNearMultiplier: 1.15f,
                    damageFarMultiplier: 0.25f,
                    damageFalloffStart01: 0.2f,
                    damageFalloffEnd01: 0.8f,
                    heatSeekSignatureGain: 0f,
                    hazardOcclusionResistance: 0.88f),
                _ => new Space4XWeaponFamilyNuanceProfile(
                    heatMultiplier: 1f,
                    ammoMultiplier: 1f,
                    hitChanceMultiplier: 1f,
                    damageNearMultiplier: 1f,
                    damageFarMultiplier: 1f,
                    damageFalloffStart01: 1f,
                    damageFalloffEnd01: 1f,
                    heatSeekSignatureGain: 0f,
                    hazardOcclusionResistance: 0.3f)
            };
        }

        public static float ResolveHeatPerShot(float baseHeatPerShot, in Space4XWeaponFamilyNuanceProfile profile)
        {
            return math.max(0f, baseHeatPerShot * math.max(0f, profile.HeatMultiplier));
        }

        public static int ResolveAmmoPerShot(int baseAmmoPerShot, in Space4XWeaponFamilyNuanceProfile profile)
        {
            if (baseAmmoPerShot <= 0)
            {
                return 0;
            }

            if (profile.AmmoMultiplier <= 0f)
            {
                return 0;
            }

            return math.max(1, (int)math.ceil(baseAmmoPerShot * profile.AmmoMultiplier));
        }

        public static float ResolveHitChanceMultiplier(in Space4XWeaponFamilyNuanceProfile profile)
        {
            return math.clamp(profile.HitChanceMultiplier, 0.05f, 1.5f);
        }

        public static float ResolveHeatSeekHitChanceMultiplier(float targetHeatSignature01, in Space4XWeaponFamilyNuanceProfile profile)
        {
            var heat01 = math.saturate(targetHeatSignature01);
            var gain = math.max(0f, profile.HeatSeekSignatureGain);
            return math.clamp(1f + gain * heat01, 1f, 1.6f);
        }

        public static float ResolveHazardOcclusionHitChanceMultiplier(
            float laneOcclusion01,
            float projectedBlastRadius,
            in Space4XWeaponFamilyNuanceProfile profile)
        {
            var occlusion = math.saturate(laneOcclusion01);
            var blastAssist = math.saturate(math.max(0f, projectedBlastRadius) / 16f);
            var effectiveOcclusion = occlusion * (1f - blastAssist * 0.6f);
            var resistance = math.saturate(profile.HazardOcclusionResistance);
            var minMultiplier = math.lerp(0.45f, 1f, resistance);
            return math.lerp(1f, minMultiplier, effectiveOcclusion);
        }

        public static float ResolveDistanceDamageMultiplier(
            float distance,
            float optimalRange,
            float maxRange,
            in Space4XWeaponFamilyNuanceProfile profile)
        {
            if (distance <= 0f)
            {
                return math.max(0f, profile.DamageNearMultiplier);
            }

            var effectiveMax = math.max(1f, maxRange);
            var normalized = math.saturate(distance / effectiveMax);

            var start = math.clamp(profile.DamageFalloffStart01, 0f, 1f);
            var end = math.clamp(math.max(start + 0.0001f, profile.DamageFalloffEnd01), 0.0001f, 1f);

            if (optimalRange > 0f && effectiveMax > 0f)
            {
                var optimal01 = math.saturate(optimalRange / effectiveMax);
                start = math.max(start, optimal01 * 0.75f);
            }

            var t = math.saturate((normalized - start) / math.max(0.0001f, end - start));
            return math.lerp(profile.DamageNearMultiplier, profile.DamageFarMultiplier, t);
        }
    }
}
