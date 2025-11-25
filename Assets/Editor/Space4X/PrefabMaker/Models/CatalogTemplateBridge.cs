using System.Collections.Generic;
using System.Linq;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.Models
{
    /// <summary>
    /// Bridge between catalog authoring components and editor template models.
    /// Loads catalog data and converts it to templates with derived properties.
    /// </summary>
    public static class CatalogTemplateBridge
    {
        /// <summary>
        /// Load all hull templates from the catalog.
        /// </summary>
        public static List<HullTemplate> LoadHullTemplates(string catalogPath)
        {
            var templates = new List<HullTemplate>();
            var catalogPrefabPath = $"{catalogPath}/HullCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<HullCatalogAuthoring>();
            if (catalog == null || catalog.hulls == null) return templates;
            
            foreach (var hullData in catalog.hulls)
            {
                var template = new HullTemplate
                {
                    id = hullData.id,
                    displayName = hullData.id, // Default to ID, can be overridden
                    baseMassTons = hullData.baseMassTons,
                    fieldRefitAllowed = hullData.fieldRefitAllowed,
                    category = hullData.category,
                    hangarCapacity = hullData.hangarCapacity,
                    presentationArchetype = hullData.presentationArchetype,
                    variant = hullData.variant,
                    builtInModuleLoadouts = hullData.builtInModuleLoadouts != null 
                        ? new List<string>(hullData.builtInModuleLoadouts) 
                        : new List<string>(),
                    palette = hullData.defaultPalette,
                    roughness = hullData.defaultRoughness,
                    pattern = hullData.defaultPattern
                };
                
                // Convert slots
                if (hullData.slots != null)
                {
                    template.slots = hullData.slots.Select(s => new HullSlotTemplate
                    {
                        type = s.type,
                        size = s.size
                    }).ToList();
                }
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all module templates from the catalog.
        /// </summary>
        public static List<ModuleTemplate> LoadModuleTemplates(string catalogPath)
        {
            var templates = new List<ModuleTemplate>();
            var catalogPrefabPath = $"{catalogPath}/ModuleCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<ModuleCatalogAuthoring>();
            if (catalog == null || catalog.modules == null) return templates;
            
            foreach (var moduleData in catalog.modules)
            {
                var template = new ModuleTemplate
                {
                    id = moduleData.id,
                    displayName = moduleData.id,
                    moduleClass = moduleData.moduleClass,
                    requiredMount = moduleData.requiredMount,
                    requiredSize = moduleData.requiredSize,
                    massTons = moduleData.massTons,
                    powerDrawMW = moduleData.powerDrawMW,
                    offenseRating = moduleData.offenseRating,
                    defenseRating = moduleData.defenseRating,
                    utilityRating = moduleData.utilityRating,
                    defaultEfficiency = moduleData.defaultEfficiency,
                    function = moduleData.function,
                    functionCapacity = moduleData.functionCapacity,
                    functionDescription = moduleData.functionDescription,
                    quality = moduleData.quality,
                    rarity = moduleData.rarity,
                    tier = moduleData.tier,
                    manufacturerId = moduleData.manufacturerId,
                    facilityArchetype = moduleData.facilityArchetype,
                    facilityTier = moduleData.facilityTier
                };
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all station templates from the catalog.
        /// </summary>
        public static List<StationTemplate> LoadStationTemplates(string catalogPath)
        {
            var templates = new List<StationTemplate>();
            var catalogPrefabPath = $"{catalogPath}/StationCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<StationCatalogAuthoring>();
            if (catalog == null || catalog.stations == null) return templates;
            
            foreach (var stationData in catalog.stations)
            {
                var template = new StationTemplate
                {
                    id = stationData.id,
                    displayName = stationData.id,
                    hasRefitFacility = stationData.hasRefitFacility,
                    facilityZoneRadius = stationData.facilityZoneRadius,
                    presentationArchetype = stationData.presentationArchetype,
                    palette = stationData.defaultPalette,
                    roughness = stationData.defaultRoughness,
                    pattern = stationData.defaultPattern
                };
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all resource templates from the catalog.
        /// </summary>
        public static List<ResourceTemplate> LoadResourceTemplates(string catalogPath)
        {
            var templates = new List<ResourceTemplate>();
            var catalogPrefabPath = $"{catalogPath}/ResourceCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<ResourceCatalogAuthoring>();
            if (catalog == null || catalog.resources == null) return templates;
            
            foreach (var resourceData in catalog.resources)
            {
                var template = new ResourceTemplate
                {
                    id = resourceData.id,
                    displayName = resourceData.id,
                    presentationArchetype = resourceData.presentationArchetype ?? string.Empty,
                    palette = resourceData.defaultPalette,
                    roughness = resourceData.defaultRoughness,
                    pattern = resourceData.defaultPattern
                };
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all product templates from the catalog.
        /// </summary>
        public static List<ProductTemplate> LoadProductTemplates(string catalogPath)
        {
            var templates = new List<ProductTemplate>();
            var catalogPrefabPath = $"{catalogPath}/ProductCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<ProductCatalogAuthoring>();
            if (catalog == null || catalog.products == null) return templates;
            
            foreach (var productData in catalog.products)
            {
                var template = new ProductTemplate
                {
                    id = productData.id,
                    displayName = productData.id,
                    presentationArchetype = productData.presentationArchetype ?? string.Empty,
                    requiredTechTier = productData.requiredTechTier,
                    palette = productData.defaultPalette,
                    roughness = productData.defaultRoughness,
                    pattern = productData.defaultPattern
                };
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all aggregate templates from the catalog.
        /// </summary>
        public static List<AggregateTemplate> LoadAggregateTemplates(string catalogPath)
        {
            var templates = new List<AggregateTemplate>();
            var catalogPrefabPath = $"{catalogPath}/AggregateCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<AggregateCatalogAuthoring>();
            if (catalog == null || catalog.aggregates == null) return templates;
            
            foreach (var aggregateData in catalog.aggregates)
            {
                var template = new AggregateTemplate
                {
                    id = aggregateData.id,
                    displayName = aggregateData.id,
                    useComposedProfiles = aggregateData.useComposedProfiles,
                    templateId = aggregateData.templateId ?? string.Empty,
                    outlookId = aggregateData.outlookId ?? string.Empty,
                    alignmentId = aggregateData.alignmentId ?? string.Empty,
                    personalityId = aggregateData.personalityId ?? string.Empty,
                    themeId = aggregateData.themeId ?? string.Empty,
                    palette = aggregateData.defaultPalette,
                    roughness = aggregateData.defaultRoughness,
                    pattern = aggregateData.defaultPattern
                };
                
                // Note: Policy fields are resolved from profile catalogs at runtime
                // We don't store them in the template, but could add them if needed
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all effect templates from the catalog.
        /// </summary>
        public static List<EffectTemplate> LoadEffectTemplates(string catalogPath)
        {
            var templates = new List<EffectTemplate>();
            var catalogPrefabPath = $"{catalogPath}/EffectCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<EffectCatalogAuthoring>();
            if (catalog == null || catalog.effects == null) return templates;
            
            foreach (var effectData in catalog.effects)
            {
                var template = new EffectTemplate
                {
                    id = effectData.id,
                    displayName = effectData.id,
                    presentationArchetype = effectData.presentationArchetype ?? string.Empty,
                    palette = effectData.defaultPalette,
                    roughness = effectData.defaultRoughness,
                    pattern = effectData.defaultPattern
                };
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all individual templates from the catalog.
        /// </summary>
        public static List<IndividualTemplate> LoadIndividualTemplates(string catalogPath)
        {
            var templates = new List<IndividualTemplate>();
            var catalogPrefabPath = $"{catalogPath}/IndividualCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<IndividualCatalogAuthoring>();
            if (catalog == null || catalog.individuals == null) return templates;
            
            foreach (var individualData in catalog.individuals)
            {
                var template = new IndividualTemplate
                {
                    id = individualData.id,
                    displayName = individualData.id,
                    role = ConvertRole(individualData.role),
                    command = individualData.command,
                    tactics = individualData.tactics,
                    logistics = individualData.logistics,
                    diplomacy = individualData.diplomacy,
                    engineering = individualData.engineering,
                    resolve = individualData.resolve,
                    physique = individualData.physique,
                    finesse = individualData.finesse,
                    will = individualData.will,
                    physiqueInclination = individualData.physiqueInclination,
                    finesseInclination = individualData.finesseInclination,
                    willInclination = individualData.willInclination,
                    law = individualData.law,
                    good = individualData.good,
                    integrity = individualData.integrity,
                    raceId = individualData.raceId,
                    cultureId = individualData.cultureId,
                    preordainTrack = individualData.preordainTrack,
                    lineageId = individualData.lineageId ?? string.Empty,
                    contractType = individualData.contractType,
                    employerId = individualData.employerId ?? string.Empty,
                    contractDurationYears = individualData.contractDurationYears
                };
                
                // Convert titles
                if (individualData.titles != null)
                {
                    template.titles = individualData.titles.Select(t => new TitleData
                    {
                        tier = (byte)t.tier,
                        type = ConvertTitleType(t.type),
                        level = (byte)t.level,
                        state = ConvertTitleState(t.state),
                        displayName = t.displayName ?? string.Empty,
                        colonyId = t.colonyId ?? string.Empty,
                        factionId = t.factionId ?? string.Empty,
                        empireId = t.empireId ?? string.Empty,
                        acquisitionReason = t.acquisitionReason ?? string.Empty,
                        lossReason = t.lossReason ?? string.Empty
                    }).ToList();
                }
                
                // Convert loyalty scores
                if (individualData.loyaltyScores != null)
                {
                    template.loyaltyScores = individualData.loyaltyScores.Select(ls => new LoyaltyEntry
                    {
                        targetType = ConvertLoyaltyTargetType(ls.targetType),
                        targetId = ls.targetId ?? string.Empty,
                        loyalty = ls.loyalty
                    }).ToList();
                }
                
                // Convert ownership stakes
                if (individualData.ownershipStakes != null)
                {
                    template.ownershipStakes = individualData.ownershipStakes.Select(os => new OwnershipStake
                    {
                        assetType = os.assetType ?? string.Empty,
                        assetId = os.assetId ?? string.Empty,
                        ownershipPercentage = os.ownershipPercentage
                    }).ToList();
                }
                
                // Convert mentorship
                template.mentorId = individualData.mentorId ?? string.Empty;
                if (individualData.menteeIds != null)
                {
                    template.menteeIds = new List<string>(individualData.menteeIds);
                }
                
                // Convert patronages
                if (individualData.patronages != null)
                {
                    template.patronages = individualData.patronages.Select(p => new PatronageEntry
                    {
                        aggregateType = ConvertAggregateType(p.aggregateType),
                        aggregateId = p.aggregateId ?? string.Empty,
                        role = p.role ?? string.Empty
                    }).ToList();
                }
                
                // Convert successors
                if (individualData.successors != null)
                {
                    template.successors = individualData.successors.Select(s => new SuccessorEntry
                    {
                        successorId = s.successorId ?? string.Empty,
                        inheritancePercentage = s.inheritancePercentage,
                        type = (SuccessorType)s.type
                    }).ToList();
                }
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        // Helper conversion methods
        private static IndividualRole ConvertRole(IndividualCatalogAuthoring.IndividualRole role)
        {
            return role switch
            {
                IndividualCatalogAuthoring.IndividualRole.Captain => IndividualRole.Captain,
                IndividualCatalogAuthoring.IndividualRole.Legend => IndividualRole.Legend,
                IndividualCatalogAuthoring.IndividualRole.AceOfficer => IndividualRole.AceOfficer,
                IndividualCatalogAuthoring.IndividualRole.JuniorOfficer => IndividualRole.JuniorOfficer,
                IndividualCatalogAuthoring.IndividualRole.CrewSpecialist => IndividualRole.CrewSpecialist,
                _ => IndividualRole.CrewSpecialist
            };
        }
        
        private static TitleType ConvertTitleType(Space4X.Registry.TitleType type)
        {
            return type switch
            {
                Space4X.Registry.TitleType.Hero => TitleType.Captain, // Map Hero to Captain for now
                Space4X.Registry.TitleType.Elite => TitleType.Admiral,
                Space4X.Registry.TitleType.Ruler => TitleType.Governor,
                _ => TitleType.Captain
            };
        }
        
        private static TitleState ConvertTitleState(Space4X.Registry.TitleState state)
        {
            return state switch
            {
                Space4X.Registry.TitleState.Active => TitleState.Active,
                Space4X.Registry.TitleState.Lost => TitleState.Inactive,
                Space4X.Registry.TitleState.Former => TitleState.Inactive,
                Space4X.Registry.TitleState.Revoked => TitleState.Revoked,
                _ => TitleState.Active
            };
        }
        
        private static LoyaltyTargetType ConvertLoyaltyTargetType(Space4X.Registry.AffiliationType type)
        {
            return type switch
            {
                Space4X.Registry.AffiliationType.Empire => LoyaltyTargetType.Empire,
                Space4X.Registry.AffiliationType.Guild => LoyaltyTargetType.Guild,
                _ => LoyaltyTargetType.Empire
            };
        }

        private static AggregateType ConvertAggregateType(Space4X.Registry.AffiliationType type)
        {
            return type switch
            {
                Space4X.Registry.AffiliationType.Guild => AggregateType.Guild,
                Space4X.Registry.AffiliationType.Corporation => AggregateType.Corporation,
                Space4X.Registry.AffiliationType.Army => AggregateType.Army,
                Space4X.Registry.AffiliationType.Band => AggregateType.Band,
                _ => AggregateType.Guild
            };
        }
        
        /// <summary>
        /// Load all weapon templates from the catalog.
        /// </summary>
        public static List<WeaponTemplate> LoadWeaponTemplates(string catalogPath)
        {
            var templates = new List<WeaponTemplate>();
            var catalogPrefabPath = $"{catalogPath}/WeaponCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<WeaponCatalogAuthoring>();
            if (catalog == null || catalog.weapons == null) return templates;
            
            foreach (var weaponData in catalog.weapons)
            {
                var template = new WeaponTemplate
                {
                    id = weaponData.id,
                    displayName = weaponData.id,
                    weaponClass = weaponData.weaponClass,
                    fireRate = weaponData.fireRate,
                    burstCount = weaponData.burstCount,
                    spreadDeg = weaponData.spreadDeg,
                    energyCost = weaponData.energyCost,
                    heatCost = weaponData.heatCost,
                    leadBias = weaponData.leadBias,
                    projectileId = weaponData.projectileId ?? string.Empty
                };
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all projectile templates from the catalog.
        /// </summary>
        public static List<ProjectileTemplate> LoadProjectileTemplates(string catalogPath)
        {
            var templates = new List<ProjectileTemplate>();
            var catalogPrefabPath = $"{catalogPath}/ProjectileCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<ProjectileCatalogAuthoring>();
            if (catalog == null || catalog.projectiles == null) return templates;
            
            foreach (var projData in catalog.projectiles)
            {
                var template = new ProjectileTemplate
                {
                    id = projData.id,
                    displayName = projData.id,
                    kind = projData.kind,
                    speed = projData.speed,
                    lifetime = projData.lifetime,
                    gravity = projData.gravity,
                    turnRateDeg = projData.turnRateDeg,
                    seekRadius = projData.seekRadius,
                    pierce = projData.pierce,
                    chainRange = projData.chainRange,
                    aoERadius = projData.aoERadius,
                    damage = new DamageModelTemplate
                    {
                        kinetic = projData.kineticDamage,
                        energy = projData.energyDamage,
                        explosive = projData.explosiveDamage
                    }
                };
                
                // Convert on-hit effects
                if (projData.onHitEffects != null)
                {
                    template.onHitEffects = projData.onHitEffects.Select(e => new EffectOpTemplate
                    {
                        kind = e.kind,
                        magnitude = e.magnitude,
                        duration = e.duration,
                        statusId = e.statusId
                    }).ToList();
                }
                
                templates.Add(template);
            }
            
            return templates;
        }
        
        /// <summary>
        /// Load all turret templates from the catalog.
        /// </summary>
        public static List<TurretTemplate> LoadTurretTemplates(string catalogPath)
        {
            var templates = new List<TurretTemplate>();
            var catalogPrefabPath = $"{catalogPath}/TurretCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            if (prefab == null) return templates;
            
            var catalog = prefab.GetComponent<TurretCatalogAuthoring>();
            if (catalog == null || catalog.turrets == null) return templates;
            
            foreach (var turretData in catalog.turrets)
            {
                var template = new TurretTemplate
                {
                    id = turretData.id,
                    displayName = turretData.id,
                    arcLimitDeg = turretData.arcLimitDeg,
                    traverseSpeedDegPerSec = turretData.traverseSpeedDegPerSec,
                    elevationMinDeg = turretData.elevationMinDeg,
                    elevationMaxDeg = turretData.elevationMaxDeg,
                    recoilForce = turretData.recoilForce,
                    socketName = turretData.socketName ?? "Socket_Muzzle"
                };
                
                templates.Add(template);
            }
            
            return templates;
        }
    }
}

