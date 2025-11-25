using System.Collections.Generic;
using System.Linq;
using Space4X.Editor.PrefabMakerTool.Models;
using AggregateTemplate = Space4X.Editor.PrefabMakerTool.Models.AggregateTemplate;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.Validation
{
    /// <summary>
    /// Validates template models and populates validation issues.
    /// </summary>
    public static class TemplateValidator
    {
        /// <summary>
        /// Validate a template and populate its validation issues.
        /// </summary>
        public static void ValidateTemplate(PrefabTemplate template)
        {
            if (template.validationIssues == null)
                template.validationIssues = new List<string>();
            else
                template.validationIssues.Clear();
            
            template.isValid = true;
            
            // Common validations
            if (string.IsNullOrWhiteSpace(template.id))
            {
                template.validationIssues.Add("ID is required");
                template.isValid = false;
            }
            
            // Category-specific validations
            switch (template)
            {
                case HullTemplate hull:
                    ValidateHull(hull);
                    break;
                case ModuleTemplate module:
                    ValidateModule(module);
                    break;
                case StationTemplate station:
                    ValidateStation(station);
                    break;
                case AggregateTemplate aggregate:
                    ValidateAggregate(aggregate);
                    break;
                case IndividualTemplate individual:
                    ValidateIndividual(individual);
                    break;
                case WeaponTemplate weapon:
                    ValidateWeapon(weapon);
                    break;
                case ProjectileTemplate projectile:
                    ValidateProjectile(projectile);
                    break;
                case TurretTemplate turret:
                    ValidateTurret(turret);
                    break;
            }
            
            if (template.validationIssues.Count > 0)
                template.isValid = false;
        }
        
        private static void ValidateHull(HullTemplate hull)
        {
            if (hull.baseMassTons <= 0)
            {
                hull.validationIssues.Add("Base mass must be greater than 0");
            }
            
            if (hull.slots == null || hull.slots.Count == 0)
            {
                hull.validationIssues.Add("Hull has no sockets defined");
            }
            
            // Check for duplicate socket names
            var socketKeys = new HashSet<string>();
            foreach (var slot in hull.slots ?? new List<HullSlotTemplate>())
            {
                var key = $"{slot.type}_{slot.size}";
                if (socketKeys.Contains(key))
                {
                    // This is okay, multiple sockets of same type/size are allowed
                }
                socketKeys.Add(key);
            }
        }
        
        private static void ValidateModule(ModuleTemplate module)
        {
            if (module.massTons < 0)
            {
                module.validationIssues.Add("Mass cannot be negative");
            }
            
            if (module.quality < 0f || module.quality > 1f)
            {
                module.validationIssues.Add("Quality must be between 0 and 1");
            }
            
            // Facility validation
            if (module.facilityArchetype != Space4X.Registry.FacilityArchetype.None && module.function == Space4X.Registry.ModuleFunction.None)
            {
                module.validationIssues.Add("Facility archetype requires a module function");
            }
        }
        
        private static void ValidateStation(StationTemplate station)
        {
            if (station.hasRefitFacility && station.facilityZoneRadius <= 0)
            {
                station.validationIssues.Add("Refit facility requires a zone radius > 0");
            }
        }
        
        private static void ValidateAggregate(AggregateTemplate aggregate)
        {
            if (aggregate.useComposedProfiles)
            {
                if (string.IsNullOrWhiteSpace(aggregate.templateId) && 
                    string.IsNullOrWhiteSpace(aggregate.outlookId) && 
                    string.IsNullOrWhiteSpace(aggregate.alignmentId))
                {
                    aggregate.validationIssues.Add("Composed aggregate requires at least one profile ID (template, outlook, or alignment)");
                }
            }
        }
        
        private static void ValidateIndividual(IndividualTemplate individual)
        {
            if (individual.command < 0f || individual.command > 100f ||
                individual.tactics < 0f || individual.tactics > 100f ||
                individual.logistics < 0f || individual.logistics > 100f ||
                individual.diplomacy < 0f || individual.diplomacy > 100f ||
                individual.engineering < 0f || individual.engineering > 100f ||
                individual.resolve < 0f || individual.resolve > 100f)
            {
                individual.validationIssues.Add("All stats must be between 0 and 100");
            }
            
            if (individual.physiqueInclination < 1 || individual.physiqueInclination > 10 ||
                individual.finesseInclination < 1 || individual.finesseInclination > 10 ||
                individual.willInclination < 1 || individual.willInclination > 10)
            {
                individual.validationIssues.Add("Inclination values must be between 1 and 10");
            }
        }
        
        private static void ValidateWeapon(WeaponTemplate weapon)
        {
            if (weapon.fireRate < 0f)
            {
                weapon.validationIssues.Add("Fire rate must be >= 0");
            }
            
            if (weapon.burstCount < 1)
            {
                weapon.validationIssues.Add("Burst count must be >= 1");
            }
            
            if (weapon.spreadDeg < 0f)
            {
                weapon.validationIssues.Add("Spread must be >= 0");
            }
            
            if (weapon.energyCost < 0f)
            {
                weapon.validationIssues.Add("Energy cost must be >= 0");
            }
            
            if (weapon.heatCost < 0f)
            {
                weapon.validationIssues.Add("Heat cost must be >= 0");
            }
            
            if (weapon.leadBias < 0f || weapon.leadBias > 1f)
            {
                weapon.validationIssues.Add("Lead bias must be between 0 and 1");
            }
            
            if (string.IsNullOrWhiteSpace(weapon.projectileId))
            {
                weapon.validationIssues.Add("Projectile ID is required");
            }
        }
        
        private static void ValidateProjectile(ProjectileTemplate projectile)
        {
            if (projectile.speed < 0f)
            {
                projectile.validationIssues.Add("Speed must be >= 0");
            }
            
            if (projectile.lifetime < 0f)
            {
                projectile.validationIssues.Add("Lifetime must be >= 0");
            }
            
            if (projectile.gravity < 0f)
            {
                projectile.validationIssues.Add("Gravity must be >= 0");
            }
            
            if (projectile.turnRateDeg < 0f || projectile.turnRateDeg > 720f)
            {
                projectile.validationIssues.Add("Turn rate must be between 0 and 720 deg/s");
            }
            
            if (projectile.seekRadius < 0f)
            {
                projectile.validationIssues.Add("Seek radius must be >= 0");
            }
            
            if (projectile.pierce < 0f)
            {
                projectile.validationIssues.Add("Pierce must be >= 0");
            }
            
            if (projectile.chainRange < 0f)
            {
                projectile.validationIssues.Add("Chain range must be >= 0");
            }
            
            if (projectile.aoERadius < 0f)
            {
                projectile.validationIssues.Add("AoE radius must be >= 0");
            }
            
            // Beam validation: beam = Speed=0, ProjectileKind=BeamTick
            if (projectile.kind == Space4X.Registry.ProjectileKind.BeamTick && projectile.speed != 0f)
            {
                projectile.validationIssues.Add("Beam projectiles must have Speed = 0 (hitscan)");
            }
            
            // Missile validation: missiles require TurnRateDeg > 0
            if (projectile.kind == Space4X.Registry.ProjectileKind.Missile && projectile.turnRateDeg <= 0f)
            {
                projectile.validationIssues.Add("Missiles require TurnRateDeg > 0");
            }
            
            // Damage budget validation (prevent silly configs)
            if (projectile.damage != null)
            {
                var totalDamage = projectile.damage.kinetic + projectile.damage.energy + projectile.damage.explosive;
                if (totalDamage <= 0f)
                {
                    projectile.validationIssues.Add("Projectile must have at least some damage");
                }
            }
        }
        
        private static void ValidateTurret(TurretTemplate turret)
        {
            if (turret.arcLimitDeg < 0f || turret.arcLimitDeg > 360f)
            {
                turret.validationIssues.Add("Arc limit must be between 0 and 360 degrees");
            }
            
            if (turret.traverseSpeedDegPerSec < 0f)
            {
                turret.validationIssues.Add("Traverse speed must be >= 0");
            }
            
            if (turret.elevationMinDeg < -90f || turret.elevationMinDeg > 90f)
            {
                turret.validationIssues.Add("Min elevation must be between -90 and 90 degrees");
            }
            
            if (turret.elevationMaxDeg < -90f || turret.elevationMaxDeg > 90f)
            {
                turret.validationIssues.Add("Max elevation must be between -90 and 90 degrees");
            }
            
            if (turret.elevationMinDeg > turret.elevationMaxDeg)
            {
                turret.validationIssues.Add("Min elevation must be <= max elevation");
            }
            
            if (turret.recoilForce < 0f)
            {
                turret.validationIssues.Add("Recoil force must be >= 0");
            }
            
            if (string.IsNullOrWhiteSpace(turret.socketName))
            {
                turret.validationIssues.Add("Socket name is required");
            }
        }
        
        /// <summary>
        /// Validate all templates in a list.
        /// </summary>
        public static void ValidateAll<T>(List<T> templates) where T : PrefabTemplate
        {
            foreach (var template in templates)
            {
                ValidateTemplate(template);
            }
        }
        
        /// <summary>
        /// Validate cross-references (e.g., every WeaponSpec points to a valid ProjectileSpec).
        /// </summary>
        public static List<string> ValidateCrossReferences(
            List<WeaponTemplate> weapons,
            List<ProjectileTemplate> projectiles)
        {
            var issues = new List<string>();
            var projectileIds = new HashSet<string>(projectiles.Select(p => p.id));
            
            foreach (var weapon in weapons)
            {
                if (!string.IsNullOrWhiteSpace(weapon.projectileId) && !projectileIds.Contains(weapon.projectileId))
                {
                    issues.Add($"Weapon '{weapon.id}' references invalid projectile ID: '{weapon.projectileId}'");
                }
            }
            
            return issues;
        }
    }
}

