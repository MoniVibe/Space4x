#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;

namespace Space4X.Tests
{
    public sealed class Space4XWeaponFamilyNuanceTests
    {
        [Test]
        public void PlasmaProfile_UsesHeatBiasAndDistanceDissipation()
        {
            var weapon = new Space4XWeapon
            {
                Type = WeaponType.Plasma,
                Size = WeaponSize.Medium,
                OptimalRange = 300f,
                MaxRange = 800f
            };

            var profile = Space4XWeaponFamilyNuance.ResolveProfile(Space4XWeaponFamilyNuance.ResolveArchetype(weapon));
            var heat = Space4XWeaponFamilyNuance.ResolveHeatPerShot(1f, profile);
            var near = Space4XWeaponFamilyNuance.ResolveDistanceDamageMultiplier(120f, weapon.OptimalRange, weapon.MaxRange, profile);
            var far = Space4XWeaponFamilyNuance.ResolveDistanceDamageMultiplier(760f, weapon.OptimalRange, weapon.MaxRange, profile);

            Assert.Greater(heat, 1f);
            Assert.Greater(near, far);
        }

        [Test]
        public void GuidedWeapons_SpendMoreAmmoAndLoseHitChance()
        {
            var missile = Space4XWeapon.Missile(WeaponSize.Medium);
            var missileProfile = Space4XWeaponFamilyNuance.ResolveProfile(Space4XWeaponFamilyNuance.ResolveArchetype(missile));

            var torpedo = Space4XWeapon.Torpedo(WeaponSize.Medium);
            var torpedoProfile = Space4XWeaponFamilyNuance.ResolveProfile(Space4XWeaponFamilyNuance.ResolveArchetype(torpedo));

            var missileAmmo = Space4XWeaponFamilyNuance.ResolveAmmoPerShot(missile.AmmoPerShot, missileProfile);
            var torpedoAmmo = Space4XWeaponFamilyNuance.ResolveAmmoPerShot(torpedo.AmmoPerShot, torpedoProfile);
            var missileHitMul = Space4XWeaponFamilyNuance.ResolveHitChanceMultiplier(missileProfile);
            var torpedoHitMul = Space4XWeaponFamilyNuance.ResolveHitChanceMultiplier(torpedoProfile);

            Assert.GreaterOrEqual(missileAmmo, 2);
            Assert.GreaterOrEqual(torpedoAmmo, missileAmmo);
            Assert.Less(missileHitMul, 1f);
            Assert.Less(torpedoHitMul, missileHitMul);
        }

        [Test]
        public void EnergyWeapons_DoNotConsumeAmmo()
        {
            var laser = Space4XWeapon.Laser(WeaponSize.Small);
            var profile = Space4XWeaponFamilyNuance.ResolveProfile(Space4XWeaponFamilyNuance.ResolveArchetype(laser));
            var ammo = Space4XWeaponFamilyNuance.ResolveAmmoPerShot(3, profile);

            Assert.AreEqual(0, ammo);
        }

        [Test]
        public void GuidedWeapons_HeatSeekBonusScalesWithTargetHeat()
        {
            var missile = Space4XWeapon.Missile(WeaponSize.Medium);
            var missileProfile = Space4XWeaponFamilyNuance.ResolveProfile(Space4XWeaponFamilyNuance.ResolveArchetype(missile));

            var torpedo = Space4XWeapon.Torpedo(WeaponSize.Medium);
            var torpedoProfile = Space4XWeaponFamilyNuance.ResolveProfile(Space4XWeaponFamilyNuance.ResolveArchetype(torpedo));

            var kinetic = Space4XWeapon.Kinetic(WeaponSize.Medium);
            var kineticProfile = Space4XWeaponFamilyNuance.ResolveProfile(Space4XWeaponFamilyNuance.ResolveArchetype(kinetic));

            var missileCold = Space4XWeaponFamilyNuance.ResolveHeatSeekHitChanceMultiplier(0f, missileProfile);
            var missileHot = Space4XWeaponFamilyNuance.ResolveHeatSeekHitChanceMultiplier(1f, missileProfile);
            var torpedoHot = Space4XWeaponFamilyNuance.ResolveHeatSeekHitChanceMultiplier(1f, torpedoProfile);
            var kineticHot = Space4XWeaponFamilyNuance.ResolveHeatSeekHitChanceMultiplier(1f, kineticProfile);

            Assert.AreEqual(1f, missileCold, 1e-4f);
            Assert.Greater(missileHot, missileCold);
            Assert.Greater(torpedoHot, missileHot);
            Assert.AreEqual(1f, kineticHot, 1e-4f);
        }

        [Test]
        public void HazardOcclusion_BeamsResistAndBlastAssists()
        {
            var laserProfile = Space4XWeaponFamilyNuance.ResolveProfile(
                Space4XWeaponFamilyNuance.ResolveArchetype(Space4XWeapon.Laser(WeaponSize.Medium)));
            var kineticProfile = Space4XWeaponFamilyNuance.ResolveProfile(
                Space4XWeaponFamilyNuance.ResolveArchetype(Space4XWeapon.Kinetic(WeaponSize.Medium)));
            var torpedoProfile = Space4XWeaponFamilyNuance.ResolveProfile(
                Space4XWeaponFamilyNuance.ResolveArchetype(Space4XWeapon.Torpedo(WeaponSize.Medium)));

            var beamOccluded = Space4XWeaponFamilyNuance.ResolveHazardOcclusionHitChanceMultiplier(1f, 0f, laserProfile);
            var kineticOccluded = Space4XWeaponFamilyNuance.ResolveHazardOcclusionHitChanceMultiplier(1f, 0f, kineticProfile);
            var torpedoNoBlast = Space4XWeaponFamilyNuance.ResolveHazardOcclusionHitChanceMultiplier(1f, 0f, torpedoProfile);
            var torpedoBlast = Space4XWeaponFamilyNuance.ResolveHazardOcclusionHitChanceMultiplier(1f, 12f, torpedoProfile);

            Assert.Greater(beamOccluded, kineticOccluded);
            Assert.Greater(torpedoBlast, torpedoNoBlast);
            Assert.LessOrEqual(beamOccluded, 1f);
        }
    }
}
#endif
